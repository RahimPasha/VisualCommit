namespace VisualCommit.Testing;

/// <summary>
/// A clock that moves only when the test moves it (<see cref="Advance"/>), with timers that fire
/// against it: for code that waits for a period of time to pass (a debounce, a quiet period),
/// so that a slow or busy machine cannot change what a test sees.
/// <para>
/// <see cref="Advance"/> fires every timer that falls due on the way, in the order they fall due
/// (those due at the same time in the order they were armed), each with the clock set to its due
/// time, and synchronously on the thread that calls it. A timer that a callback arms again fires
/// again within the same advance if it falls due in it. A timer armed with a due time of zero
/// fires at the next <see cref="Advance"/>, also one of <see cref="TimeSpan.Zero"/>.
/// </para>
/// <para>
/// The clock's own lock is never held while a callback runs, so the code under test may arm or
/// dispose its timer while it holds a lock of its own, from any thread, and its callback may take
/// that lock: the order is always the code's lock, then the clock's. Advance it from one thread
/// at a time.
/// </para>
/// </summary>
public sealed class ManualTimeProvider : TimeProvider
{
    private readonly Lock _gate = new();
    private readonly DateTimeOffset _start;
    private readonly List<ManualTimer> _timers = [];
    private long _ticks;
    private long _nextArming;

    /// <param name="start">What <see cref="GetUtcNow"/> says before the clock moves. Default 2026-01-01 12:00 UTC.</param>
    public ManualTimeProvider(DateTimeOffset? start = null) =>
        _start = start ?? new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    /// <summary>How far the clock has moved since it was made.</summary>
    public TimeSpan Elapsed
    {
        get
        {
            lock (_gate)
            {
                return TimeSpan.FromTicks(_ticks);
            }
        }
    }

    /// <summary>How many timers are armed: made, not disposed, and due at some time.</summary>
    public int ArmedTimers
    {
        get
        {
            lock (_gate)
            {
                return _timers.Count(timer => timer.DueAt is not null);
            }
        }
    }

    public override long TimestampFrequency => TimeSpan.TicksPerSecond;

    public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;

    public override long GetTimestamp()
    {
        lock (_gate)
        {
            return _ticks;
        }
    }

    public override DateTimeOffset GetUtcNow()
    {
        lock (_gate)
        {
            return _start + TimeSpan.FromTicks(_ticks);
        }
    }

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        ArgumentNullException.ThrowIfNull(callback);
        var timer = new ManualTimer(this, callback, state);
        lock (_gate)
        {
            _timers.Add(timer);
            ArmLocked(timer, dueTime, period);
        }

        return timer;
    }

    /// <summary>
    /// Moves the clock forward by <paramref name="by"/>, firing the timers that fall due on the
    /// way (see the class's remarks), and returns once their callbacks have returned.
    /// </summary>
    public void Advance(TimeSpan by)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(by, TimeSpan.Zero);
        long target;
        lock (_gate)
        {
            target = _ticks + by.Ticks;
        }

        while (true)
        {
            ManualTimer? due = null;
            lock (_gate)
            {
                foreach (var timer in _timers)
                {
                    if (timer.DueAt is { } at && at <= target
                        && (due is null || at < due.DueAt || (at == due.DueAt && timer.Arming < due.Arming)))
                    {
                        due = timer;
                    }
                }

                if (due is null)
                {
                    _ticks = Math.Max(_ticks, target);
                    return;
                }

                _ticks = Math.Max(_ticks, due.DueAt!.Value);
                if (due.Period > 0)
                {
                    due.DueAt += due.Period;
                    due.Arming = _nextArming++;
                }
                else
                {
                    due.DueAt = null;
                }
            }

            // Outside the lock: the callback may arm or dispose timers, and take locks of its own
            // that another thread holds while it arms its timer.
            due.Callback(due.State);
        }
    }

    private void ArmLocked(ManualTimer timer, TimeSpan dueTime, TimeSpan period)
    {
        if (dueTime != Timeout.InfiniteTimeSpan)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(dueTime, TimeSpan.Zero);
        }

        if (period != Timeout.InfiniteTimeSpan)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(period, TimeSpan.Zero);
        }

        timer.DueAt = dueTime == Timeout.InfiniteTimeSpan ? null : _ticks + dueTime.Ticks;
        timer.Period = period == Timeout.InfiniteTimeSpan ? 0 : period.Ticks;
        timer.Arming = _nextArming++;
    }

    private sealed class ManualTimer(ManualTimeProvider clock, TimerCallback callback, object? state) : ITimer
    {
        private bool _disposed;

        public TimerCallback Callback { get; } = callback;

        public object? State { get; } = state;

        /// <summary>When it next fires, in the clock's ticks; null while disarmed or disposed. Under the clock's lock.</summary>
        public long? DueAt { get; set; }

        /// <summary>Its period in ticks; 0 for a timer that fires once. Under the clock's lock.</summary>
        public long Period { get; set; }

        /// <summary>The order in which timers were armed, which orders those due at the same time. Under the clock's lock.</summary>
        public long Arming { get; set; }

        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            lock (clock._gate)
            {
                if (_disposed)
                {
                    return false;
                }

                clock.ArmLocked(this, dueTime, period);
                return true;
            }
        }

        public void Dispose()
        {
            lock (clock._gate)
            {
                _disposed = true;
                DueAt = null;
                clock._timers.Remove(this);
            }
        }

        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
