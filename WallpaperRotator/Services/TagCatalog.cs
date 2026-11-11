using System.Net;
using System.Text.RegularExpressions;

namespace WallpaperRotator.Services;

/// <summary>A Wallhaven tag found by the public site's tag search.</summary>
public sealed record WallhavenTag(string Id, string Name, bool Nsfw);

/// <summary>
/// Reads Wallhaven's server-rendered tag pages.  There is no public JSON tag
/// endpoint (every /api/v1/tag* path returns 404), so suggestions come from
/// https://wallhaven.cc/tag/search?q= and the curated /tags/popular page.
/// Both return at most 50 rows and have no paging; results are cached for ten
/// minutes because the pages change slowly and typing can fire many queries.
/// </summary>
public static class TagCatalog
{
    private const string SearchUrl = "https://wallhaven.cc/tag/search?q=";
    private const string PopularUrl = "https://wallhaven.cc/tags/popular";
    private const int MaxResults = 50;
    private const int MaxCacheEntries = 64;

    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(10);

    private static readonly Regex TagRegex = new(
        "<a\\s+([^>]*?href=\"https://wallhaven\\.cc/tag/(\\d+)\"[^>]*)>([^<]*)</a>",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex NsfwClassRegex = new(
        "class=\"[^\"]*\\bnsfw\\b",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly HttpClient Http = CreateClient();
    private static readonly object Gate = new();
    private static readonly Dictionary<string, CacheEntry> Cache = new(StringComparer.Ordinal);

    private sealed class CacheEntry
    {
        public DateTime At { get; init; }
        public List<WallhavenTag> Tags { get; init; } = new();
    }

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 " +
            "(KHTML, like Gecko) Chrome/125.0.0.0 Safari/537.36");
        client.DefaultRequestHeaders.Accept.ParseAdd("text/html,application/xhtml+xml");
        return client;
    }

    /// <summary>
    /// Search Wallhaven tags. An empty query returns the Popular Now list so the
    /// suggestion popup still has something useful to show when first focused.
    /// NSFW tags are filtered out unless Sketchy purity is enabled.
    /// </summary>
    public static async Task<List<WallhavenTag>> SearchAsync(
        string query, bool allowNsfw, CancellationToken ct = default)
    {
        string url = string.IsNullOrWhiteSpace(query)
            ? PopularUrl
            : SearchUrl + Uri.EscapeDataString(query.Trim());

        var tags = await GetAsync(url, ct);
        if (allowNsfw) return tags;
        return tags.Where(t => !t.Nsfw).ToList();
    }

    /// <summary>Clears the in-memory cache (used by tests and by an explicit refresh).</summary>
    public static void ClearCache()
    {
        lock (Gate) Cache.Clear();
    }

    private static async Task<List<WallhavenTag>> GetAsync(string url, CancellationToken ct)
    {
        lock (Gate)
        {
            if (Cache.TryGetValue(url, out var cached) && DateTime.UtcNow - cached.At < CacheTtl)
                return cached.Tags;
        }

        try
        {
            using var response = await Http.GetAsync(url, ct);
            response.EnsureSuccessStatusCode();
            string html = await response.Content.ReadAsStringAsync(ct);
            var tags = Parse(html);

            lock (Gate)
            {
                if (Cache.Count >= MaxCacheEntries) PruneCache();
                Cache[url] = new CacheEntry { At = DateTime.UtcNow, Tags = tags };
            }

            return tags;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            // Offline, rate-limited, or the page layout changed: suggestions are
            // a convenience, and the user can still type any tag by hand.
            return new List<WallhavenTag>();
        }
    }

    private static void PruneCache()
    {
        var now = DateTime.UtcNow;
        var stale = Cache.Where(kv => now - kv.Value.At >= CacheTtl)
                         .Select(kv => kv.Key)
                         .ToList();
        foreach (var key in stale) Cache.Remove(key);

        if (Cache.Count < MaxCacheEntries) return;

        // Still full: drop the oldest entry.
        string? oldestKey = null;
        DateTime oldestAt = DateTime.MaxValue;
        foreach (var (key, value) in Cache)
        {
            if (value.At >= oldestAt) continue;
            oldestAt = value.At;
            oldestKey = key;
        }
        if (oldestKey is not null) Cache.Remove(oldestKey);
    }

    private static List<WallhavenTag> Parse(string html)
    {
        var tags = new List<WallhavenTag>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (Match match in TagRegex.Matches(html))
        {
            string attrs = match.Groups[1].Value;
            string id = match.Groups[2].Value;
            string name = WebUtility.HtmlDecode(match.Groups[3].Value).Trim();

            if (name.Length == 0 || !seen.Add(id)) continue;

            tags.Add(new WallhavenTag(id, name, NsfwClassRegex.IsMatch(attrs)));
            if (tags.Count >= MaxResults) break;
        }

        return tags;
    }
}
