using System.Collections.Concurrent;
using Kyuto.App.Services;
using Kyuto.Core.Contracts;
using Kyuto.Core.Domain;
using Kyuto.Storage.Database;

namespace Kyuto.Integration.Tests.Base;

internal sealed class LyricsHarness
{
    public ConcurrentDictionary<string, CachedLyrics> Cache { get; } = new();
    public ConcurrentDictionary<string, string> Settings { get; } = new();
    public ConcurrentQueue<(string Id, string Kind, string Source, string Body)> Writes { get; } = new();
    public long Now = 1_000_000;
    public LyricsService Service { get; }

    public LyricsHarness(IReadOnlyList<ILyricsSource> sources, IReadOnlyList<IRomajiSource>? romaji = null, ILineTranslator? translator = null) =>
        Service = new LyricsService(
            sources,
            id => Cache.TryGetValue(id, out var row) ? row : null,
            (id, kind, source, body) =>
            {
                Writes.Enqueue((id, kind, source, body));
                Cache[id] = new CachedLyrics(kind, source, body, Now);
            },
            key => Settings.TryGetValue(key, out var value) ? value : null,
            (key, value) => Settings[key] = value,
            key => Settings.TryRemove(key, out _),
            romaji,
            translator,
            () => Now);

    public Task Settled() => Wait.Until(() => Service.Snapshot.Stage != LyricsStage.Loading);

    public async Task<LyricsSnapshot> Ask(Track track, bool force = false)
    {
        Service.Request(track, force);
        await Settled();
        return Service.Snapshot;
    }
}