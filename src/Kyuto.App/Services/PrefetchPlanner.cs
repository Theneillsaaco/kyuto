using Kyuto.Core.Playback;

namespace Kyuto.App.Services;

internal static class PrefetchPlanner
{
    /// <summary>
    /// The ids to resolve and cache, most urgent first: the current track and the ones that follow it. Links and paths
    /// need no resolving and a track that repeats is listed once.
    /// </summary>
    public static string[] IdsFor(QueueState queue) =>
        [..Prefetch.plan(queue).Select(t => t.Id).Where(id => !TrackSource.IsDirect(id)).Distinct()];
}