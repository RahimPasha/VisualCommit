using Xunit;

// The passes share one desktop and one mouse: two of them at once would click into each other's
// windows. Every test of this assembly runs alone.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
