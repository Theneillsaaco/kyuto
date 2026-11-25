using Kyuto.App.Services;
using Kyuto.Core.Contracts;
using Kyuto.Core.Domain;

namespace Kyuto.App.Models;

/// <summary>Domain objects to the rows QML list models show.</summary>
internal static class Rows
{
    public static TrackRow FromTrack(Track t) => new()
    {
        VideoId = t.Id,
        Title = t.Title,
        Artist = t.Artist,
        Album = t.Album,
        Duration = TimeFormat.Clock(t.DurationSeconds),
        ArtUrl = t.ArtUrl
    };

    public static TrackRow FromPlaylist(PlaylistInfo p) => new()
    {
        VideoId = p.Id,
        Title = p.Id == "LM" ? Tr.T("library.liked") : p.Title,
        Artist = p.Count switch { 0 => "", 1 => Tr.T("playlist.songs.one"), var n => Tr.F("playlist.songs.many", n) },
        ArtUrl = p.ArtUrl
    };

    public static EntityRow FromEntity(Entity e) => FromEntity(e, withKind: false);

    /// <summary>With <paramref name="withKind"/>, the subtitle starts with what it is ("Artista · 1 M suscriptores").</summary>
    public static EntityRow FromEntity(Entity e, bool withKind)
    {
        var label = e.Kind switch
        {
            EntityKind.Artist => Tr.T("search.kind.artist"),
            EntityKind.Playlist => Tr.T("hero.playlist"),
            _ => Tr.T("hero.album")
        };

        return new()
        {
            Kind = e.Kind switch { EntityKind.Artist => "artist", EntityKind.Playlist => "playlist", _ => "album" },
            EntityId = e.Id,
            Title = e.Title,
            Subtitle = !withKind ? e.Subtitle : e.Subtitle.Length > 0 ? label + " · " + e.Subtitle : label,
            ArtUrl = e.ArtUrl
        };
    }

    /// <summary>A song inside a shelf of Home.</summary>
    public static EntityRow FromTrackEntity(Track t)
    {
        var duration = TimeFormat.Clock(t.DurationSeconds);

        return new()
        {
            Kind = "track",
            EntityId = t.Id,
            Title = t.Title,
            Subtitle = duration.Length > 0 ? t.Artist + " · " + duration : t.Artist,
            ArtUrl = t.ArtUrl
        };
    }

    public static EntityRow Header(string title) => new() { Kind = "header", Title = title };
}
