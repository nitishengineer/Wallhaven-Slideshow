namespace WallpaperRotator.Services;

/// <summary>
/// Picks the next included tag for a rotation.  Ordered mode walks the list and
/// wraps; shuffle mode draws from a bag that is refilled only after every tag
/// has been used, so a small pool cannot show the same tag twice in a row.
/// </summary>
public static class TagPool
{
    public static (string Tag, string Info) Pick(AppSettings settings)
    {
        var tags = (settings.PoolTags ?? new List<string>())
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .ToList();

        settings.PoolBag ??= new List<string>();
        if (tags.Count == 0) return ("", "");

        if (string.Equals(settings.TagOrderMode, "shuffle", StringComparison.OrdinalIgnoreCase))
        {
            var valid = new HashSet<string>(tags, StringComparer.OrdinalIgnoreCase);
            settings.PoolBag.RemoveAll(t => !valid.Contains(t));

            if (settings.PoolBag.Count == 0)
            {
                settings.PoolBag = tags.OrderBy(_ => Random.Shared.Next()).ToList();

                // A fresh bag may otherwise start with the tag that just played,
                // which reads as a duplicate even though the bag is correct.
                if (settings.PoolBag.Count > 1 &&
                    string.Equals(settings.PoolBag[0], settings.PoolLast, StringComparison.OrdinalIgnoreCase))
                {
                    (settings.PoolBag[0], settings.PoolBag[^1]) =
                        (settings.PoolBag[^1], settings.PoolBag[0]);
                }

                LogService.Info($"Tag pool: refilled shuffle bag ({settings.PoolBag.Count} tags).");
            }

            string tag = settings.PoolBag[0];
            settings.PoolBag.RemoveAt(0);
            settings.PoolLast = tag;
            return (tag, $"shuffle · {settings.PoolBag.Count} left");
        }

        int index = Math.Clamp(settings.PoolCursor, 0, tags.Count - 1);
        string ordered = tags[index];
        settings.PoolCursor = (index + 1) % tags.Count;
        settings.PoolLast = ordered;
        return (ordered, $"{index + 1}/{tags.Count}");
    }
}
