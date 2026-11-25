using System.Text.Json;
using Kyuto.App.Models;
using Kyuto.App.Services;
using Kyuto.App.ViewModels.Base;
using Kyuto.Core;
using Kyuto.Core.Contracts;
using Kyuto.Core.Domain;

namespace Kyuto.App.ViewModels;

/// <summary>
/// The user's YouTube Music library: playlists, the open playlist's tracks, and the heart (likes) of the
/// playing track or of any song of a list. Background work leaves results in a queue that <see cref="Poll"/>
/// applies on the Qt thread.
/// </summary>
public class LibraryViewModel : ViewModelBase
{
    private const string LikedList = "LM"; // YouTube Music's automatic Liked Music playlist

    private readonly BackgroundWork _work = new();
    private IReadOnlyList<PlaylistInfo> _playlists = [];
    private IReadOnlyList<Track> _listTracks = [];
    private HashSet<string> _likedIds = [];
    private bool _libraryLoaded;
    private IReadOnlyList<Track> _shown = []; // what the list shows now (filtered and sorted); row indexes refer to it
    private string _filter = "", _sort = "default";

    public LibraryViewModel()
    {
        AppServices.EnsureStarted();
        _likedIds = [.. (SafeCache(() => AppServices.Db?.LoadTracks(LikedList)) ?? []).Select(t => t.Id)];
        PublishLiked();
        AppServices.Playback.Changed += UpdateLikeState;
        AppServices.AccountChanged += SyncAccount;
        AppServices.LanguageChanged += RefreshLibrary;
        SyncAccount();
    }

    /// <summary>The user's playlists (title, "N canciones", cover).</summary>
    public TrackListModel Library { get; } = new();
    /// <summary>The tracks of the playlist that is open.</summary>
    public TrackListModel Tracks { get; } = new();

    public string LibraryStatus { get; private set => Set(ref field, value); } = "";
    /// <summary>default, title, artist or duration.</summary>
    public string ListSort { get; private set => Set(ref field, value); } = "default";
    /// <summary>Id of the open playlist, "" while the playlist list is showing.</summary>
    public string OpenListId { get; private set => Set(ref field, value); } = "";
    public string OpenListTitle { get; private set => Set(ref field, value); } = "";
    public string ListStatus { get; private set => Set(ref field, value); } = "";
    public int ListCount { get; private set => Set(ref field, value); }
    public bool CanLike { get; private set => Set(ref field, value); }
    public bool Liked { get; private set => Set(ref field, value); }
    

    /// <summary>How the last playlist action went ("" when there is none). QML shows it in the snackbar.</summary>
    public string Notice { get; private set => Set(ref field, value); } = "";

    /// <summary>A JSON array with the id of every liked song. The ⋮ menus of the lists read it to show their heart.</summary>
    public string LikedJson { get; private set => Set(ref field, value); } = "[]";

    // The library page shows these when no account is connected. State is one of: unavailable, signedOut, signedIn, expired.
    public string AccountState { get; private set => Set(ref field, value); } = "unavailable";
    public string AccountStatus { get; private set => Set(ref field, value); } = "";
    public bool AccountBusy { get; private set => Set(ref field, value); }

    /// <summary>Anything in the background that the UI timer must keep polling for.</summary>
    public bool Working { get; private set => Set(ref field, value); }

    /// <summary>Applies finished background work and follows the account state. QML calls it from a Timer.</summary>
    public void Poll()
    {
        _work.Drain();
        SyncAccount();
        UpdateLikeState();
        Working = _work.Busy || AccountBusy;
    }

    /// <summary>Loads the playlist list the first time the library is shown.</summary>
    public void LoadLibrary()
    {
        if (AppServices.Account is not { } account || AccountState != "signedIn" || _libraryLoaded) return;
        LibraryStatus = Tr.T("library.loading");
        Run(Task.Run(() => account.GetLibraryAsync()),
            lists =>
            {
                _libraryLoaded = true;
                _playlists = lists;
                Library.Replace(lists.Select(Rows.FromPlaylist));
                LibraryStatus = lists.Count == 0 ? Tr.T("library.empty") : "";
            },
            e => LibraryStatus = ErrorText(e));
    }

    /// <summary>Opens a playlist: the cached tracks show at once, fresh ones replace them when they arrive.</summary>
    public void OpenPlaylist(int index)
    {
        if (AppServices.Account is not { } account || index < 0 || index >= _playlists.Count) return;
        var list = _playlists[index];
        var id = list.Id;
        OpenListId = id;
        OpenListTitle = id == LikedList ? Tr.T("library.liked") : list.Title;

        var cached = SafeCache(() => AppServices.Db?.LoadTracks(id)) ?? [];
        SetListTracks(cached);
        ListStatus = Tr.T(cached.Count == 0 ? "status.loading" : "list.updating");

        Run(Task.Run(async () =>
            {
                var tracks = await account.GetPlaylistAsync(id);
                SafeCache(() => { AppServices.Db?.SaveTracks(id, tracks); return 0; });
                return tracks;
            }),
            tracks =>
            {
                if (id == LikedList) ReplaceLiked(tracks);
                if (OpenListId != id) return; // the user moved on
                SetListTracks(tracks);
                ListStatus = tracks.Count == 0 ? Tr.T("list.empty") : "";
            },
            e =>
            {
                if (OpenListId != id) return;
                ListStatus = cached.Count == 0 ? ErrorText(e) : Tr.F("list.updateFailed", e.Message);
            });
    }

    public void CloseList()
    {
        OpenListId = "";
        OpenListTitle = "";
        ListStatus = "";
        _filter = "";
        _sort = "default";
        ListSort = "default";
        SetListTracks([]);
    }

    /// <summary>Replaces the queue with the open playlist, in order or shuffled.</summary>
    public void PlayList(bool shuffle) => AppServices.Playback.PlayList(_shown, shuffle);

    public void PlayListItem(int index) => AppServices.Playback.PlayFrom(_shown, index);

    public void AddListItem(int index) => AppServices.Playback.AddFrom(_shown, index);

    public void PlayNextListItem(int index) => AppServices.Playback.PlayNextFrom(_shown, index);

    /// <summary>The ticked rows (JSON array of indexes) go to the end of the queue.</summary>
    public void AddSelected(string indexes) => AppServices.Playback.AddMany(Selection.Pick(_shown, indexes), next: false);

    /// <summary>The ticked rows play right after the current song.</summary>
    public void PlayNextSelected(string indexes) => AppServices.Playback.AddMany(Selection.Pick(_shown, indexes), next: true);

    /// <summary>Hands the ticked rows to the playlist picker.</summary>
    public void StageSelected(string indexes) => AppServices.Staged = Selection.Pick(_shown, indexes);

    
    /// <summary>Likes or unlikes the playing track on YouTube Music.</summary>
    public void ToggleLike()
    {
        if (CanLike)
            ToggleLikeOf(AppServices.Playback.CurrentId);
    }

    /// <summary>
    /// Likes or unlikes any song by its YouTube id (the ⋮ menu and the right click of the lists). Nothing happens
    /// without a signed-in account or for a link or a file.
    /// </summary>
    public void ToggleLikeOf(string id)
    {
        if (AppServices.Account is not { } account || AccountState != "signedIn" || id.Length == 0 || TrackSource.IsDirect(id))
            return;

        var like = !_likedIds.Contains(id);
        SetLiked(id, like);

        Run(Task.Run(() => account.RateAsync(id, like)),
            _ => { },
            e =>
            {
                SetLiked(id, !like); // it did not go through: put the heart back
                AppServices.Status.Text = Tr.F("like.failed", e.Message);
            });
    }

    /// <summary>Adds a song to one of the user's playlists.</summary>
    public void AddToPlaylist(string playlistId, string videoId)
    {
        if (AppServices.Account is not { } account || playlistId.Length == 0 || videoId.Length == 0) return;

        var title = _playlists.FirstOrDefault(p => p.Id == playlistId)?.Title ?? "";

        Run(Task.Run(() => account.AddToPlaylistAsync(playlistId, videoId)),
            _ =>
            {
                _libraryLoaded = false; // the song count changed: the next visit to the library reads it again
                SayOnce(Tr.F("playlist.addedTo", title));
            },
            e => SayOnce(e.Message == "duplicate" ? Tr.T("playlist.exists") : Tr.F("playlist.failed", e.Message)));
    }

    private string[] StagedIds() =>
        [.. AppServices.Staged.Select(t => t.Id).Where(id => id.Length == 11 && !TrackSource.IsDirect(id)).Distinct()];

    /// <summary>Adds the ticked songs (see <see cref="AppServices.Staged"/>) to one of the user's playlists.</summary>
    public void AddStagedToPlaylist(string playlistId)
    {
        var ids = StagedIds();
        if (AppServices.Account is not { } account || playlistId.Length == 0 || ids.Length == 0) return;

        var title = _playlists.FirstOrDefault(p => p.Id == playlistId)?.Title ?? "";

        Run(Task.Run(async () =>
            {
                var added = 0;
                foreach (var id in ids)
                {
                    try
                    {
                        await account.AddToPlaylistAsync(playlistId, id);
                        added++;
                    }
                    catch (Exception e) when (e.Message == "duplicate") { /* it already had it */ }
                }
                return added;
            }),
            added =>
            {
                _libraryLoaded = false;
                SayOnce(added == 0 ? Tr.T("playlist.exists") : Tr.F("playlist.addedMany", added, title));
            },
            e => SayOnce(Tr.F("playlist.failed", e.Message)));
    }

    /// <summary>Makes a playlist with the ticked songs in it.</summary>
    public void CreatePlaylistWithStaged(string title)
    {
        title = title.Trim();
        var ids = StagedIds();
        if (AppServices.Account is not { } account || title.Length == 0 || ids.Length == 0) return;

        Run(Task.Run(async () =>
            {
                var made = await account.CreatePlaylistAsync(title, ids[0]);
                foreach (var id in ids.Skip(1))
                {
                    try { await account.AddToPlaylistAsync(made, id); }
                    catch (Exception e) when (e.Message == "duplicate") { /* the same song twice */ }
                }
                return made;
            }),
            _ =>
            {
                _libraryLoaded = false;
                LoadLibrary();
                SayOnce(Tr.F("playlist.created", title));
            },
            e => SayOnce(Tr.F("playlist.failed", e.Message)));
    }

    /// <summary>Makes a playlist, with the song in it when videoId is not empty.</summary>
    public void CreatePlaylist(string title, string videoId)
    {
        title = title.Trim();
        if (AppServices.Account is not { } account || title.Length == 0) return;

        Run(Task.Run(() => account.CreatePlaylistAsync(title, videoId)),
            _ =>
            {
                _libraryLoaded = false;
                LoadLibrary();
                SayOnce(Tr.F("playlist.created", title));
            },
            e => SayOnce(Tr.F("playlist.failed", e.Message)));
    }

    public void DeletePlaylist(string playlistId)
    {
        if (AppServices.Account is not { } account || playlistId.Length == 0 || playlistId == LikedList) return;

        Run(Task.Run(() => account.DeletePlaylistAsync(playlistId)),
            _ =>
            {
                SafeCache(() => { AppServices.Db?.SaveTracks(playlistId, []); return 0; });
                CloseList();
                _libraryLoaded = false;
                LoadLibrary();
                SayOnce(Tr.T("playlist.deleted"));
            },
            e => SayOnce(Tr.F("playlist.failed", e.Message)));
    }
    
    public void ChooseListFilter(string text)
    {
        _filter = text.Trim();
        ApplyView();
    }

    public void ChooseListSort(string key)
    {
        _sort = key is "title" or "artist" or "duration" ? key : "default";
        ListSort = _sort;
        ApplyView();
    }

    /// <summary>Sets the notice twice, so the same text twice in a row still reaches QML.</summary>
    private void SayOnce(string text)
    {
        Notice = "";
        Notice = text;
    }

    private void SetLiked(string id, bool liked)
    {
        if (liked) _likedIds.Add(id); else _likedIds.Remove(id);
        PublishLiked();
        UpdateLikeState();
    }

    /// <summary>Fetches Liked Music in the background so the heart knows every liked song.</summary>
    private void RefreshLiked()
    {
        if (AppServices.Account is not { } account) return;
        Run(Task.Run(async () =>
            {
                var tracks = await account.GetPlaylistAsync(LikedList);
                SafeCache(() => { AppServices.Db?.SaveTracks(LikedList, tracks); return 0; });
                return tracks;
            }),
            ReplaceLiked,
            e => Log.Warn("library", "liked songs", e));
    }

    private void ReplaceLiked(IReadOnlyList<Track> tracks)
    {
        _likedIds = [.. tracks.Select(t => t.Id)];
        PublishLiked();
        UpdateLikeState();
    }

    private void PublishLiked() => LikedJson = JsonSerializer.Serialize(_likedIds);

    private void UpdateLikeState()
    {
        var p = AppServices.Playback;
        CanLike = AccountState == "signedIn" && p.IsLoaded && p.CurrentId.Length > 0 && !TrackSource.IsDirect(p.CurrentId);
        Liked = CanLike && _likedIds.Contains(p.CurrentId);
    }

    private void SetListTracks(IReadOnlyList<Track> tracks)
    {
        _listTracks = tracks;
        ApplyView();
    }
    
    private void ApplyView()
    {
        IEnumerable<Track> view = _listTracks;
        var cmp = StringComparison.CurrentCultureIgnoreCase;

        if (_filter.Length > 0)
            view = view.Where(t => t.Title.Contains(_filter, cmp) || t.Artist.Contains(_filter, cmp) || t.Album.Contains(_filter, cmp));

        view = _sort switch
        {
            "title" => view.OrderBy(t => t.Title, StringComparer.CurrentCultureIgnoreCase),
            "artist" => view.OrderBy(t => t.Artist, StringComparer.CurrentCultureIgnoreCase).ThenBy(t => t.Title, StringComparer.CurrentCultureIgnoreCase),
            "duration" => view.OrderBy(t => t.DurationSeconds),
            _ => view,
        };

        _shown = [.. view];
        Tracks.Replace(_shown.Select(Rows.FromTrack));
        ListCount = _listTracks.Count;
    }

    /// <summary>The language changed: the rows that carry a text ("12 songs") are built again.</summary>
    private void RefreshLibrary()
    {
        Library.Replace(_playlists.Select(Rows.FromPlaylist));
        SyncAccount();
    }

    private void SyncAccount()
    {
        if (AppServices.Account is not { } account) return;
        var s = account.Snapshot;
        var state = AccountView.StateName(s.Stage);
        var wasSignedIn = AccountState == "signedIn";
        AccountState = state;
        AccountStatus = s.Status;
        AccountBusy = s.Busy;

        if (state == "signedIn" && !wasSignedIn)
        {
            _libraryLoaded = false;
            RefreshLiked();
        }
        else if (state != "signedIn" && wasSignedIn)
        {
            _libraryLoaded = false;
            _playlists = [];
            _likedIds = [];
            PublishLiked();
            Library.Replace([]);
            CloseList();
            LibraryStatus = "";
        }
        Working = _work.Busy || AccountBusy;
    }

    private void Run<T>(Task<T> task, Action<T> done, Action<Exception> failed)
    {
        _work.Run(task, done, failed);
        Working = true;
    }

    private static string ErrorText(Exception e) =>
        e is AuthExpiredException ? Tr.T("auth.reimport") : Tr.F("status.error", e.Message);

    private static T? SafeCache<T>(Func<T?> read)
    {
        try { return read(); }
        catch (Exception e)
        {
            Log.Warn("library", "cache", e);
            return default;
        }
    }
}
