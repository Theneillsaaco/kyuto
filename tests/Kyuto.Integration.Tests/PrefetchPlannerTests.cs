using Player.App.Services;
using Player.Core.Domain;
using Player.Core.Playback;
using Player.Integration.Tests.Base;

namespace Player.Integration.Tests;

public class PrefetchPlannerTests
{
    private static QueueState Loaded(int count, int start, RepeatMode? repeat = null)
    {
        var queue = PlayQueue.replace(1, Enumerable.Range(0, count).Select(i => Samples.Song(i.ToString())), start, PlayQueue.empty);
        return PlayQueue.setRepeat(repeat ?? RepeatMode.Off, queue);
    }

    [Fact]
    public void The_current_track_comes_first_then_the_next_three()
    {
        Assert.Equal(["1", "2", "3", "4"], PrefetchPlanner.IdsFor(Loaded(10, 1)));
    }

    [Fact]
    public void The_plan_stops_at_the_end_of_the_queue()
    {
        Assert.Equal(["2"], PrefetchPlanner.IdsFor(Loaded(3, 2)));
    }

    [Fact]
    public void A_track_that_repeats_is_listed_once()
    {
        Assert.Equal(["1"], PrefetchPlanner.IdsFor(Loaded(3, 1, RepeatMode.One)));
    }

    [Fact]
    public void Links_and_paths_are_not_resolved()
    {
        var queue = PlayQueue.replace(1, [Samples.Song("/music/a.mp3"), Samples.Song("abc"), Samples.Song("https://x/y")], 0, PlayQueue.empty);

        Assert.Equal(["abc"], PrefetchPlanner.IdsFor(queue));
    }

    [Fact]
    public void An_empty_queue_plans_nothing()
    {
        Assert.Empty(PrefetchPlanner.IdsFor(PlayQueue.empty));
    }
}
