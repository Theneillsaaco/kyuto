using Player.App.Services;
using Player.Core.Domain;
using Player.Integration.Tests.Base;

namespace Player.Integration.Tests;

public class MprisMapperTests
{
    private static Player.Mpris.MprisState Build(
        Track? track = null, bool loaded = false, bool paused = true, bool awaiting = false, double duration = 0,
        double position = 0, int volume = 100, double speed = 1.0, string repeat = "off", bool shuffle = false,
        bool canNext = false, bool canPrevious = false, int count = 0) =>
        MprisMapper.Build(track, loaded, paused, awaiting, duration, position, volume, speed, repeat, shuffle, canNext, canPrevious, count);

    [Fact]
    public void An_empty_player_is_stopped_with_no_track()
    {
        var state = Build();

        Assert.Equal("Stopped", state.Status);
        Assert.Equal("", state.TrackKey);
        Assert.Empty(state.Artists);
        Assert.False(state.CanPlay);
        Assert.False(state.CanSeek);
    }

    [Theory]
    [InlineData(true, false, "Playing")]
    [InlineData(true, true, "Paused")]
    public void A_loaded_track_plays_or_is_paused(bool loaded, bool paused, string expected) =>
        Assert.Equal(expected, Build(Samples.Song("a"), loaded: loaded, paused: paused).Status);

    [Fact]
    public void A_restored_track_that_is_not_loaded_reads_as_paused()
    {
        Assert.Equal("Paused", Build(Samples.Song("a"), loaded: false).Status);
    }

    [Fact]
    public void The_track_fields_are_copied()
    {
        var track = Samples.Song("abc");
        var modifiedTrack = new Track(
            track.Id,
            track.Title,
            track.Artist,
            track.Artists,
            "Al",
            track.DurationSeconds,
            track.ArtUrl,
            track.Kind
        );

        var state = Build(modifiedTrack);

        Assert.Equal("abc", state.TrackKey);
        Assert.Equal("title abc", state.Title);
        Assert.Equal("Al", state.Album);
        Assert.Equal("art-abc", state.ArtUrl);
        Assert.Equal(["artist"], state.Artists);
    }

    [Fact]
    public void The_artist_text_stands_in_when_the_list_is_empty()
    {
        var track = Samples.Song("a");

        var trackWithSolo = new Track(
            track.Id,
            track.Title,
            "Solo",
            [],
            track.Album,
            track.DurationSeconds,
            track.ArtUrl,
            track.Kind
        );

        var trackEmptyArtist = new Track(
            track.Id,
            track.Title,
            "",
            [],
            track.Album,
            track.DurationSeconds,
            track.ArtUrl,
            track.Kind
        );

        Assert.Equal(["Solo"], Build(trackWithSolo).Artists);
        Assert.Empty(Build(trackEmptyArtist).Artists);
    }

    [Fact]
    public void Times_are_in_microseconds_and_the_volume_is_zero_to_one()
    {
        var state = Build(Samples.Song("a"), duration: 200.5, position: 3.25, volume: 40, speed: 1.5);

        Assert.Equal(200_500_000, state.LengthMicros);
        Assert.Equal(3_250_000, state.PositionMicros);
        Assert.Equal(0.4, state.Volume);
        Assert.Equal(1.5, state.Rate);
    }

    [Fact]
    public void Seeking_is_only_possible_once_the_stream_is_loaded()
    {
        Assert.True(Build(Samples.Song("a"), loaded: true).CanSeek);
        Assert.False(Build(Samples.Song("a"), loaded: true, awaiting: true).CanSeek);
        Assert.False(Build(Samples.Song("a"), loaded: false).CanSeek);
    }

    [Fact]
    public void Controls_follow_the_queue()
    {
        var state = Build(Samples.Song("a"), canNext: true, canPrevious: true, count: 3, shuffle: true);

        Assert.True(state.CanGoNext);
        Assert.True(state.CanGoPrevious);
        Assert.True(state.CanPlay);
        Assert.True(state.Shuffle);
    }

    [Theory]
    [InlineData("off", "None")]
    [InlineData("one", "Track")]
    [InlineData("all", "Playlist")]
    [InlineData("junk", "None")]
    public void Repeat_names_map_to_loop_statuses(string repeat, string loop)
    {
        Assert.Equal(loop, MprisMapper.LoopOf(repeat));
        Assert.Equal(loop, Build(repeat: repeat).Loop);
    }

    [Fact]
    public void Loop_statuses_map_back_to_repeat_modes()
    {
        Assert.Equal(RepeatMode.One, MprisMapper.RepeatOf("Track"));
        Assert.Equal(RepeatMode.All, MprisMapper.RepeatOf("Playlist"));
        Assert.Equal(RepeatMode.Off, MprisMapper.RepeatOf("None"));
        Assert.Equal(RepeatMode.Off, MprisMapper.RepeatOf("whatever"));
    }
}