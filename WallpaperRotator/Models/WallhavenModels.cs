using System.Text.Json.Serialization;

namespace WallpaperRotator.Models;

public class WallhavenMeta
{
    [JsonPropertyName("last_page")] public int LastPage { get; set; } = 1;
}
public class WallhavenSearchResponse
{
    [JsonPropertyName("data")] public List<WallhavenWallpaper> Data { get; set; } = new();
    [JsonPropertyName("meta")] public WallhavenMeta Meta { get; set; } = new();
}

public class WallhavenWallpaper
{
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    [JsonPropertyName("path")] public string Path { get; set; } = "";
    [JsonPropertyName("resolution")] public string Resolution { get; set; } = "";
    [JsonPropertyName("dimension_x")] public int DimensionX { get; set; }
    [JsonPropertyName("dimension_y")] public int DimensionY { get; set; }
    [JsonPropertyName("file_type")] public string FileType { get; set; } = "";
    [JsonPropertyName("file_size")] public long FileSize { get; set; }
    [JsonPropertyName("colors")] public List<string> Colors { get; set; } = new();
}

public class WallhavenCollection
{
    [JsonPropertyName("id")] public int Id { get; set; }
    [JsonPropertyName("label")] public string Label { get; set; } = "";
    [JsonPropertyName("count")] public int Count { get; set; }
}