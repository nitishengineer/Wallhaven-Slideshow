using System.Reflection;
using System.Text.Json;

namespace WallpaperRotator.Services;

public record UpdateInfo(string Version, string ReleaseUrl, string DownloadUrl, string Notes);

/// <summary>Polls GitHub Releases for a newer version. Silent by design: never throws, never nags.</summary>
public static class UpdateChecker
{
    private const string Repo = "nitishengineer/Wallhaven-Slideshow";
    private const string AssetName = "WallpaperRotator-win-x64.exe";   // matches your release workflow
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(15) };

    /// <summary>Version embedded from the csproj &lt;Version&gt; at build time.</summary>
    public static Version CurrentVersion
    {
        get
        {
            var v = typeof(UpdateChecker).Assembly.GetName().Version!;
            return new Version(v.Major, v.Minor, Math.Max(0, v.Build));
        }
    }

    public static async Task<UpdateInfo?> CheckAsync(CancellationToken ct = default)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get,
                $"https://api.github.com/repos/{Repo}/releases/latest");
            req.Headers.UserAgent.ParseAdd("WallpaperRotator/1.0");      // GitHub API requires a UA
            req.Headers.Accept.ParseAdd("application/vnd.github+json");

            using var response = await Http.SendAsync(req, ct);
            if (!response.IsSuccessStatusCode) return null;              // 404 = no releases yet

            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            var root = doc.RootElement;

            var tag = root.GetProperty("tag_name").GetString() ?? "";
            var latest = ParseTag(tag);
            if (latest is null || latest <= CurrentVersion) return null;

            string download = "";
            if (root.TryGetProperty("assets", out var assets))
            {
                foreach (var a in assets.EnumerateArray())
                {
                    if (a.TryGetProperty("name", out var n) && n.GetString() == AssetName)
                    {
                        download = a.GetProperty("browser_download_url").GetString() ?? "";
                        break;
                    }
                }
            }

            return new UpdateInfo(
                tag.TrimStart('v', 'V'),
                root.GetProperty("html_url").GetString() ?? "",
                download,
                root.TryGetProperty("body", out var b) ? b.GetString() ?? "" : "");
        }
        catch
        {
            return null;   // offline / rate-limited / malformed -> stay silent
        }
    }

    private static Version? ParseTag(string tag)
    {
        var t = tag.TrimStart('v', 'V');
        return Version.TryParse(t, out var v)
            ? new Version(v.Major, v.Minor, Math.Max(0, v.Build))
            : null;
    }
}