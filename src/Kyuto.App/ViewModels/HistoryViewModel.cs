using System.Globalization;
using Kyuto.App.Models;
using Kyuto.App.Services;
using Kyuto.App.ViewModels.Base;
using Kyuto.Core;
using Kyuto.Core.Domain;
using Kyuto.Storage.Database;

namespace Kyuto.App.ViewModels;

/// <summary>
/// The history page: the songs that counted as a listen, newest first. Instantiated once from Main.qml as
/// <c>HistoryViewModel { id: historyVm }</c>; Main.qml calls <see cref="Load"/> when the page is opened.
/// </summary>
public class HistoryViewModel : ViewModelBase
{
    private const int Limit = 300;

    private IReadOnlyList<Track> _tracks = [];
    private IReadOnlyList<HistoryEntry> _cleared = [];
    
    public HistoryViewModel()
    {
        AppServices.EnsureStarted();
        AppServices.LanguageChanged += Load; // the dates are written in the language of the interface
    }

    public TrackListModel Items { get; } = new();
    public int Count { get; private set => Set(ref field, value); }

    public void Load()
    {
        IReadOnlyList<HistoryEntry> entries;

        try { entries = AppServices.Db?.GetHistory(Limit) ?? []; }
        catch (Exception e)
        {
            Log.Warn("history", "read", e);
            entries = [];
        }

        _tracks = [.. entries.Select(ToTrack)];
        Items.Replace(entries.Select(ToRow));
        Count = entries.Count;
    }

    public void ClearAll()
    {
        try
        {
            _cleared = AppServices.Db?.GetHistory(int.MaxValue) ?? [];
            AppServices.Db?.ClearHistory();
        }
        catch (Exception e) { Log.Warn("history", "clear", e); }

        Load();
    }

    public void UndoClear()
    {
        if (AppServices.Db is not { } db)
            return;

        try
        {
            foreach (var e in _cleared.Reverse()) // oldest first, so the ids keep their order
                db.AddHistory(new Listen(e.StartedAt, e.Artist, e.Title, e.Album, e.Duration, true), e.Listened, e.TrackId);
        }
        catch (Exception e) { Log.Warn("history", "undo", e); }

        _cleared = [];
        Load();
    }
    
    public void PlayItem(int index) => AppServices.Playback.PlayFrom(_tracks, index);

    public void AddItem(int index) => AppServices.Playback.AddFrom(_tracks, index);

    public void PlayNextItem(int index) => AppServices.Playback.PlayNextFrom(_tracks, index);

    /// <summary>The ticked rows (JSON array of indexes) go to the end of the queue.</summary>
    public void AddSelected(string indexes) => AppServices.Playback.AddMany(Selection.Pick(_tracks, indexes), next: false);

    /// <summary>The ticked rows play right after the current song.</summary>
    public void PlayNextSelected(string indexes) => AppServices.Playback.AddMany(Selection.Pick(_tracks, indexes), next: true);

    /// <summary>Hands the ticked rows to the playlist picker.</summary>
    public void StageSelected(string indexes) => AppServices.Staged = Selection.Pick(_tracks, indexes);

    
    private static string ArtOf(string id) => id.Length == 11 ? "https://i.ytimg.com/vi/" + id + "/mqdefault.jpg" : "";

    private static Track ToTrack(HistoryEntry e)
    {
        string[] artists = e.Artist.Length > 0 ? [e.Artist] : [];
        return new Track(e.TrackId, e.Title, e.Artist, artists, e.Album, e.Duration, ArtOf(e.TrackId), TrackKind.Song);
    }

    private static TrackRow ToRow(HistoryEntry e)
    {
        var when = DateTimeOffset.FromUnixTimeSeconds(e.StartedAt).ToLocalTime().ToString("d MMM, HH:mm", CultureInfo.CurrentCulture);

        return new TrackRow
        {
            VideoId = e.TrackId,
            Title = e.Title,
            Artist = e.Artist.Length > 0 ? e.Artist + " · " + when : when,
            Album = e.Album,
            Duration = TimeFormat.Clock(e.Duration),
            ArtUrl = ArtOf(e.TrackId)
        };
    }
}
