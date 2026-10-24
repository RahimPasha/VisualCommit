using VisualCommit.App.ViewModels.Workspaces;
using VisualCommit.Core.Operations;
using Xunit;

namespace VisualCommit.App.Tests;

/// <summary>
/// The window's slots of the bulk runs (docs/phase-6-design.md 4.3, 4.13, D210), without git: at
/// most four out at once, first come, first served, never two on one common git folder nor one on
/// a common folder another queue is fetching from (a fake busy queue), <see cref="BulkSlots.IsHeldFor"/>
/// while a slot is out, and a withdrawn request never granted.
/// </summary>
public class BulkSlotsTests
{
    [Fact]
    public void At_most_four_are_granted_and_the_fifth_gets_the_first_slot_released()
    {
        var slots = new BulkSlots();
        var requests = Enumerable.Range(0, 6).Select(i => slots.Request($"/repos/r{i}/.git")).ToList();

        Assert.Equal(BulkSlots.Count, slots.Held);
        Assert.Equal([true, true, true, true, false, false], requests.Select(Granted));
        Assert.Equal(2, slots.Waiting);

        requests[2].Release();
        Assert.Equal([true, true, false, true, true, false], requests.Select(request => request.IsGranted));
        Assert.True(Granted(requests[4]));
        Assert.False(requests[5].Granted.IsCompleted);
        Assert.Equal(4, slots.Held);

        // Released twice, it gives back one slot.
        requests[2].Release();
        Assert.False(requests[5].Granted.IsCompleted);
        requests[0].Release();
        Assert.True(Granted(requests[5]));
    }

    [Fact]
    public void A_request_on_a_common_folder_that_holds_a_slot_is_passed_over_and_served_when_it_is_free()
    {
        var slots = new BulkSlots();
        var first = slots.Request("/repos/api/.git");
        var sibling = slots.Request("/repos/api/.git");
        var other = slots.Request("/repos/docs/.git");

        Assert.True(Granted(first));
        Assert.False(sibling.Granted.IsCompleted);
        Assert.True(Granted(other));
        Assert.True(slots.IsHeldFor("/repos/api/.git"));

        first.Release();
        Assert.True(Granted(sibling));
        Assert.True(slots.IsHeldFor("/repos/api/.git"));
    }

    [Fact]
    public void Requests_are_served_first_come_first_served()
    {
        var slots = new BulkSlots();
        var held = Enumerable.Range(0, 4).Select(i => slots.Request($"/repos/h{i}/.git")).ToList();
        var (a, b, c) = (slots.Request("/repos/a/.git"), slots.Request("/repos/b/.git"), slots.Request("/repos/c/.git"));

        held[3].Release();
        Assert.Equal([true, false, false], new[] { a, b, c }.Select(Granted));
        held[0].Release();
        Assert.Equal([true, true, false], new[] { a, b, c }.Select(Granted));
    }

    [Fact]
    public void A_request_whose_common_folder_another_queue_fetches_from_waits_for_that_queues_change()
    {
        var slots = new BulkSlots();
        var fetching = new FakeQueue { Current = Network("Fetch") };
        var request = slots.Request("/repos/api/.git", () => fetching.Current is { Kind: OperationKind.Network } ? fetching : null);
        var other = slots.Request("/repos/docs/.git");

        Assert.False(request.Granted.IsCompleted);
        Assert.True(Granted(other));
        Assert.Equal(1, slots.Held);

        // A progress of the same fetch: still busy.
        fetching.Raise();
        Assert.False(request.Granted.IsCompleted);

        // The fetch ended: served at the queue's change.
        fetching.Current = null;
        fetching.Raise();
        Assert.True(Granted(request));
        Assert.Equal(0, fetching.Listeners);
    }

    [Fact]
    public void A_queue_that_went_idle_before_it_was_watched_frees_the_request_at_once()
    {
        var slots = new BulkSlots();
        var fetching = new FakeQueue { Current = Network("Fetch") };
        var asked = 0;

        // The fetch ends between the look and the watch: the request is served without a change.
        var request = slots.Request("/repos/api/.git", () =>
        {
            if (++asked == 1)
            {
                return fetching;
            }

            fetching.Current = null;
            return null;
        });

        Assert.True(Granted(request));
    }

    [Fact]
    public void Is_held_for_is_true_exactly_while_a_slot_is_out_on_the_folder()
    {
        var slots = new BulkSlots();
        Assert.False(slots.IsHeldFor("/repos/api/.git"));
        var request = slots.Request("/repos/api/.git");
        Assert.True(slots.IsHeldFor("/repos/api/.git"));
        Assert.False(slots.IsHeldFor("/repos/docs/.git"));
        request.Release();
        Assert.False(slots.IsHeldFor("/repos/api/.git"));
    }

    [Fact]
    public void A_withdrawn_request_is_never_granted_and_later_ones_are_served()
    {
        var slots = new BulkSlots();
        var held = Enumerable.Range(0, 4).Select(i => slots.Request($"/repos/h{i}/.git")).ToList();
        var (withdrawn, later) = (slots.Request("/repos/w/.git"), slots.Request("/repos/l/.git"));

        withdrawn.Withdraw();
        Assert.True(withdrawn.Granted.IsCompletedSuccessfully);
        Assert.False(Granted(withdrawn));
        held[1].Release();

        Assert.False(withdrawn.IsGranted);
        Assert.True(Granted(later));
        Assert.Equal(0, slots.Waiting);

        // A granted request withdrawn gives its slot back.
        later.Withdraw();
        Assert.Equal(3, slots.Held);
        Assert.False(slots.IsHeldFor("/repos/l/.git"));
    }

    private static bool Granted(BulkSlotRequest request) => request.Granted is { IsCompletedSuccessfully: true, Result: true };

    private static OperationSnapshot Network(string title) =>
        new(title, $"{title}ing…", OperationKind.Network, IsBackground: false, ErrorDisplay.Dialog, Progress: null, CanCancel: true);

    /// <summary>A queue whose running operation the test sets, and whose <see cref="Changed"/> it raises.</summary>
    private sealed class FakeQueue : IOperationQueue
    {
        private EventHandler? _changed;

        public OperationSnapshot? Current { get; set; }

        public int Listeners => _changed?.GetInvocationList().Length ?? 0;

        public int Waiting => 0;

        public string? FirstWaitingTitle => null;

        public OperationResult? LastResult => null;

        public event EventHandler? Changed
        {
            add => _changed += value;
            remove => _changed -= value;
        }

        public event EventHandler<OperationFinishedEventArgs>? Finished
        {
            add { }
            remove { }
        }

        public void Raise() => _changed?.Invoke(this, EventArgs.Empty);

        public Task<OperationResult> RunAsync(GitOperation operation) => throw new NotSupportedException();

        public bool TryStartInBackground(GitOperation operation) => throw new NotSupportedException();

        public Task WhenIdleAsync() => Task.CompletedTask;

        public void CancelCurrent()
        {
        }

        public void DropWaiting()
        {
        }

        public void Cancel(GitOperation operation)
        {
        }

        public void Dispose()
        {
        }
    }
}
