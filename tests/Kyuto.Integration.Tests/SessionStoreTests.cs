using Kyuto.App.Services;
using Kyuto.Core.Domain;
using Kyuto.Core.Playback;
using Kyuto.Integration.Tests.Base;

namespace Kyuto.Integration.Tests;

public class SessionStoreTests : IDisposable
{
    private readonly TempDb _temp = new();
    private readonly SessionStore _store;

    public SessionStoreTests() => _store = new SessionStore(_temp.Db);

    public void Dispose() => _temp.Dispose();

    private static QueueState Queue(int count, int start, RepeatMode? repeat = null, bool shuffle = false)
    {
        var tracks = Enumerable.Range(0, count).Select(i => Samples.Song(i.ToString()));
        var queue = PlayQueue.replace(1, tracks, start, PlayQueue.empty);
        queue = PlayQueue.setRepeat(repeat ?? RepeatMode.Off, queue);
        return PlayQueue.setShuffle(7, shuffle, queue);
    }

    private static string CurrentId(QueueState queue) => PlayQueue.current(queue)!.Value.Id;

    private void Mode(string setting) => _temp.Db.SetSetting(Resume.settingKey, setting);

    private static Random Rng() => new(42);

    [Fact]
    public void Nothing_saved_restores_nothing()
    {
        Assert.Null(_store.Restore(Rng()));
    }

    [Fact]
    public void Without_a_database_nothing_is_saved_or_restored_and_nothing_throws()
    {
        var store = new SessionStore(null);

        store.SaveQueue(Queue(3, 1), 10, 50);
        store.SaveProgress(10, 50);

        Assert.Null(store.Restore(Rng()));
    }

    [Fact]
    public void Continue_brings_back_queue_position_volume_repeat_and_shuffle()
    {
        Mode("continue");
        _store.SaveQueue(Queue(3, 1, RepeatMode.All, shuffle: true), 42.5, 70);

        var restored = _store.Restore(Rng())!;

        Assert.True(restored.HasQueue);
        Assert.Equal(3, PlayQueue.count(restored.Queue));
        Assert.Equal("1", CurrentId(restored.Queue));
        Assert.Equal(42.5, restored.Seconds);
        Assert.Equal(70, restored.Volume);
        Assert.Equal(RepeatMode.All, restored.Queue.Repeat);
        Assert.True(restored.Queue.Shuffle);
    }

    [Fact]
    public void The_default_keeps_the_queue_but_starts_the_track_from_zero()
    {
        _store.SaveQueue(Queue(3, 2), 99, 40);

        var restored = _store.Restore(Rng())!;

        Assert.True(restored.HasQueue);
        Assert.Equal("2", CurrentId(restored.Queue));
        Assert.Equal(0, restored.Seconds);
    }

    [Fact]
    public void Restoring_nothing_still_keeps_volume_repeat_and_shuffle()
    {
        Mode("none");
        _store.SaveQueue(Queue(3, 1, RepeatMode.One, shuffle: true), 30, 25);

        var restored = _store.Restore(Rng())!;

        Assert.False(restored.HasQueue);
        Assert.Equal(0, PlayQueue.count(restored.Queue));
        Assert.Equal(25, restored.Volume);
        Assert.Equal(RepeatMode.One, restored.Queue.Repeat);
        Assert.True(restored.Queue.Shuffle);
        Assert.Equal(0, restored.Seconds);
    }

    [Fact]
    public void A_saved_empty_queue_has_nothing_to_bring_back()
    {
        _temp.Db.SaveQueueState([], -1, 0, 60, "off", false);

        var restored = _store.Restore(Rng())!;

        Assert.False(restored.HasQueue);
        Assert.Equal(60, restored.Volume);
    }

    [Theory]
    [InlineData(99, "2")]
    [InlineData(-5, "0")]
    public void A_saved_index_out_of_range_is_clamped(int saved, string expected)
    {
        _temp.Db.SaveQueueState([Samples.Song("0"), Samples.Song("1"), Samples.Song("2")], saved, 0, 100, "off", false);

        var restored = _store.Restore(Rng())!;

        Assert.Equal(expected, CurrentId(restored.Queue));
    }

    [Fact]
    public void An_unknown_repeat_name_means_off()
    {
        _temp.Db.SaveQueueState([Samples.Song("0")], 0, 0, 100, "sideways", false);

        Assert.Equal(RepeatMode.Off, _store.Restore(Rng())!.Queue.Repeat);
    }

    [Fact]
    public void Progress_updates_the_position_without_touching_the_queue()
    {
        Mode("continue");
        _store.SaveQueue(Queue(3, 1), 5, 80);

        _store.SaveProgress(99, 10);
        var restored = _store.Restore(Rng())!;

        Assert.Equal(99, restored.Seconds);
        Assert.Equal(10, restored.Volume);
        Assert.Equal(3, PlayQueue.count(restored.Queue));
    }

    [Fact]
    public void A_broken_database_never_throws()
    {
        var temp = new TempDb();
        var store = new SessionStore(temp.Db);
        temp.Dispose(); // the connection is gone

        Assert.Null(Record.Exception(() => store.SaveQueue(Queue(2, 0), 1, 1)));
        Assert.Null(Record.Exception(() => store.SaveProgress(1, 1)));
        Assert.Null(Record.Exception(() => store.Restore(Rng())));
        Assert.Null(store.Restore(Rng()));
    }

}