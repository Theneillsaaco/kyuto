using Player.Core.Domain;
using Player.Mpris;

namespace Player.App.Services;

/// <summary>What the player is doing, turned into what the desktop is told. No state, so it is tested alone.</summary>
internal static class MprisMapper
{
    /// <param name="track">The queue's current track (the playing one, or the restored one while paused).</param>
    /// <param name="awaitingStream">The chosen track is still waiting for its stream: nothing can be seeked yet.</param>
    /// <param name="repeat">"off", "one" or "all".</param>
    public static MprisState Build(
        Track? track, bool isLoaded, bool paused, bool awaitingStream, double duration, double position,
        int volume, double speed, string repeat, bool shuffle, bool canNext, bool canPrevious, int queueCount
    )
    {
        string[] artists = track is null ? []
            : track.Artists is { Length: > 0 } list ? list
            : track.Artist.Length > 0 ? [track.Artist] : [];
        
        return new MprisState(
            Status: isLoaded ? (paused ? "Paused" : "Playing") : track is null ? "Stopped" : "Paused",
            TrackKey: track?.Id ?? "",
            Title: track?.Title ?? "",
            Artists: artists,
            Album: track?.Album ?? "",
            ArtUrl: track?.ArtUrl ?? "",
            LengthMicros: (long)(duration * 1e6),
            PositionMicros: (long)(position * 1e6),
            Volume: volume / 100.0,
            Rate: speed,
            Loop: LoopOf(repeat),
            Shuffle: shuffle,
            CanGoNext: canNext,
            CanGoPrevious: canPrevious,
            CanPlay: queueCount > 0,
            CanSeek: isLoaded && !awaitingStream
        );
    }
    
    /// <summary>The player's repeat name as MPRIS says it.</summary>
    public static string LoopOf(string repeat) => repeat switch
    {
        "one" => "Track",
        "all" => "Playlist",
        _ => "None",
    };
    
    /// <summary>MPRIS's loop status as the queue's repeat mode; anything unknown means off.</summary>
    public static RepeatMode RepeatOf(string loop) => loop switch
    {
        "Track" => RepeatMode.One,
        "Playlist" => RepeatMode.All,
        _ => RepeatMode.Off,
    };
}