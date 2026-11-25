using System.Text.Json;
using Kyuto.Core.Domain;

namespace Kyuto.App.Services;

/// <summary>The rows a user ticked in a list: QML sends their indexes as a JSON array of numbers.</summary>
internal static class Selection
{
    public static int[] Parse(string json)
    {
        try { return JsonSerializer.Deserialize<int[]>(json) ?? []; }
        catch (JsonException) { return []; }
    }

    public static Track[] Pick(IReadOnlyList<Track> all, string json) =>
        [.. Parse(json).Where(i => i >= 0 && i < all.Count).Distinct().Order().Select(i => all[i])];
}
