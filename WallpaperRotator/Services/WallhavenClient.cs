using System.Text.Json;
using WallpaperRotator.Models;

namespace WallpaperRotator.Services;

public class WallhavenQuery
{
    public string Tag { get; set; } = "";
    public int Width { get; set; } = 1920;
    public int Height { get; set; } = 1080;
    public bool Exact { get; set; }
    public string Sorting { get; set; } = "random";
    public string Categories { get; set; } = "110";
    public string Purity { get; set; } = "100";
    public string Ratio { get; set; } = "";
    public string ColorHex { get; set; } = "";
    public int PageCap { get; set; } = 10;
}

public class WallhavenClient
{
    private const string SearchEndpoint = "https://wallhaven.cc/api/v1/search";
    private const string CollectionsEndpoint = "https://wallhaven.cc/api/v1/collections";
    private const string WallpaperEndpoint = "https://wallhaven.cc/api/v1/wallpaper/";
    private static readonly HttpClient Http = CreateClient();

    /// <summary>Backoff schedule for transient failures (server 5xx / connection errors).</summary>
    private static readonly int[] RetryDelaysMs = { 20_000, 60_000 };

    public string ApiKey { get; set; } = "";

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("WallpaperRotator/1.0 (personal wallpaper app)");
        return client;
    }

    private string Auth => string.IsNullOrEmpty(ApiKey) ? "" : $"&apikey={Uri.EscapeDataString(ApiKey)}";

    // ---------- retry core ----------
    private static bool IsTransientNetwork(Exception ex, CancellationToken ct)
    {
        if (ct.IsCancellationRequested) return false;   // real cancellation: never retry
        return ex is HttpRequestException
            or System.Net.Sockets.SocketException
            or TimeoutException
            or TaskCanceledException;                   // HttpClient timeout surfaces as TaskCanceled
    }

    private async Task<HttpResponseMessage> GetWithRetryAsync(string url, CancellationToken ct)
    {
        int attempt = 0;
        while (true)
        {
            HttpResponseMessage? response = null;
            int code = 0;
            Exception? networkError = null;
            bool rateLimited = false;

            try
            {
                response = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
                code = (int)response.StatusCode;
                rateLimited = code == 429;
                if (rateLimited) response.Dispose();
            }
            catch (Exception ex) when (IsTransientNetwork(ex, ct))
            {
                networkError = ex;
            }

            if (rateLimited)
            {
                LogService.Warn($"HTTP 429 rate limit hit: {url}");
                throw new HttpRequestException("Wallhaven rate limit hit. Slow down the interval.");
            }

            bool transient = networkError is not null || code >= 500;
            if (transient && attempt < RetryDelaysMs.Length)
            {
                response?.Dispose();
                var why = networkError is not null ? networkError.GetType().Name : $"HTTP {code}";
                LogService.Warn($"{why} — retry {attempt + 1}/{RetryDelaysMs.Length} in {RetryDelaysMs[attempt] / 1000}s: {url}");
                await Task.Delay(RetryDelaysMs[attempt], ct);
                attempt++;
                continue;
            }

            if (networkError is not null) throw networkError;   // retries exhausted
            return response!;   // success, or final 5xx for the caller to EnsureSuccessStatusCode
        }
    }

    // ---------- endpoints ----------
    public async Task<List<WallhavenWallpaper>> SearchAsync(WallhavenQuery q, CancellationToken ct = default)
    {
        var res = q.Exact ? $"resolutions={q.Width}x{q.Height}" : $"atleast={q.Width}x{q.Height}";
        var ratios = string.IsNullOrEmpty(q.Ratio) ? "" : $"&ratios={Uri.EscapeDataString(q.Ratio)}";
        var colors = string.IsNullOrEmpty(q.ColorHex) ? "" : $"&colors={Uri.EscapeDataString(q.ColorHex.TrimStart('#'))}";
        var baseUrl = $"{SearchEndpoint}?q={Uri.EscapeDataString(q.Tag)}" +
                      $"&categories={q.Categories}&purity={q.Purity}" +
                      $"&sorting={Uri.EscapeDataString(q.Sorting)}" +
                      $"&{res}{ratios}{colors}" + Auth;

        LogService.Info($"Search: {baseUrl}&page=1");
        var firstPage = await GetPageAsync(baseUrl + "&page=1", ct);
        if (firstPage.Items.Count == 0)
        {
            LogService.Warn("Search returned 0 items on page 1.");
            return firstPage.Items;
        }

        var maxPage = (int)Math.Min(firstPage.LastPage, Math.Max(1, q.PageCap));
        if (maxPage <= 1) return firstPage.Items;

        var page = Random.Shared.Next(1, maxPage + 1);
        if (page == 1) return firstPage.Items;

        var chosenPage = await GetPageAsync(baseUrl + $"&page={page}", ct);
        LogService.Info($"Page roll: {page}/{maxPage} -> {chosenPage.Items.Count} items");
        return chosenPage.Items.Count > 0 ? chosenPage.Items : firstPage.Items;
    }

    public async Task<List<WallhavenWallpaper>> GetCollectionAsync(int collectionId, CancellationToken ct = default)
    {
        var url = $"{CollectionsEndpoint}/{collectionId}?apikey={Uri.EscapeDataString(ApiKey)}";
        LogService.Info($"Collection fetch: id={collectionId}");
        var page = await GetPageAsync(url, ct);
        return page.Items;
    }

    public async Task<WallhavenWallpaper?> GetWallpaperAsync(string id, CancellationToken ct = default)
    {
        using var response = await Http.GetAsync($"{WallpaperEndpoint}{Uri.EscapeDataString(id)}", ct);
        if ((int)response.StatusCode == 404) return null;
        response.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        return doc.RootElement.TryGetProperty("data", out var data)
            ? JsonSerializer.Deserialize<WallhavenWallpaper>(data.GetRawText())
            : null;
    }

    public async Task<bool> UrlExistsAsync(string url, CancellationToken ct = default)
    {
        using var resp = await Http.SendAsync(new HttpRequestMessage(HttpMethod.Head, url), ct);
        return resp.IsSuccessStatusCode;
    }

    public async Task<List<WallhavenCollection>> ListCollectionsAsync(CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(ApiKey))
            throw new InvalidOperationException("enter your API key first");
        var url = $"{CollectionsEndpoint}?apikey={Uri.EscapeDataString(ApiKey)}";
        using var response = await Http.GetAsync(url, ct);
        response.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        var items = doc.RootElement.ValueKind == JsonValueKind.Array
            ? doc.RootElement.EnumerateArray().ToList()
            : doc.RootElement.TryGetProperty("data", out var data)
                ? data.EnumerateArray().ToList()
                : new List<JsonElement>();
        var result = new List<WallhavenCollection>();
        foreach (var el in items)
        {
            result.Add(new WallhavenCollection
            {
                Id = el.TryGetProperty("id", out var id) ? id.GetInt32() : 0,
                Label = el.TryGetProperty("label", out var lb) ? lb.GetString() ?? "" : "",
                Count = el.TryGetProperty("count", out var c) ? c.GetInt32() : 0
            });
        }
        return result;
    }

    private async Task<(List<WallhavenWallpaper> Items, int LastPage)> GetPageAsync(string url, CancellationToken ct)
    {
        using var response = await GetWithRetryAsync(url, ct);
        if (!response.IsSuccessStatusCode)
            LogService.Warn($"HTTP {(int)response.StatusCode} for {url} (retries exhausted)");
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync(ct);
        var result = JsonSerializer.Deserialize<WallhavenSearchResponse>(json);
        return (result?.Data ?? new List<WallhavenWallpaper>(), result?.Meta.LastPage ?? 1);
    }

    public async Task<string> DownloadAsync(WallhavenWallpaper wallpaper, string cacheFolder, CancellationToken ct = default)
    {
        Directory.CreateDirectory(cacheFolder);
        var extension = wallpaper.FileType.Split('/').Last();
        if (extension == "jpeg") extension = "jpg";
        var filePath = Path.Combine(cacheFolder, $"wallhaven-{wallpaper.Id}.{extension}");
        if (File.Exists(filePath))
            return filePath;
        using var response = await GetWithRetryAsync(wallpaper.Path, ct);
        response.EnsureSuccessStatusCode();
        await using var fileStream = File.Create(filePath);
        await response.Content.CopyToAsync(fileStream, ct);
        LogService.Info($"Downloaded {wallpaper.Id} ({new FileInfo(filePath).Length} bytes)");
        return filePath;
    }

    public async Task<string> DownloadUrlAsync(string id, string url, string cacheFolder, CancellationToken ct = default)
    {
        Directory.CreateDirectory(cacheFolder);
        var ext = url.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ? "png" : "jpg";
        var filePath = Path.Combine(cacheFolder, $"wallhaven-{id}.{ext}");
        if (File.Exists(filePath)) return filePath;
        using var response = await GetWithRetryAsync(url, ct);
        response.EnsureSuccessStatusCode();
        await using var fs = File.Create(filePath);
        await response.Content.CopyToAsync(fs, ct);
        LogService.Info($"Downloaded {id} from CDN ({new FileInfo(filePath).Length} bytes)");
        return filePath;
    }
}