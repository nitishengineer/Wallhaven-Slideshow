using System.Diagnostics;
using WallpaperRotator.Services;

namespace WallpaperRotator;

/// <summary>History tab: card-style rotation log with per-entry Set/Block/Copy/Open actions.</summary>
public class HistoryLogForm : Form
{
    private readonly FlowLayoutPanel _flow = new();
    private readonly Label _statusLabel = new();
    private readonly WallhavenClient _client = new();
    private readonly List<HistoryEntry> _shown = new();

    public event Action<string>? StatusChanged;
    public event Action<string, string, string>? Applied;

    public HistoryLogForm()
    {
        ClientSize = new Size(960, 570);

        var bottomBar = new Panel { Dock = DockStyle.Bottom, Height = 46 };
        _statusLabel.Dock = DockStyle.Fill;
        _statusLabel.TextAlign = ContentAlignment.MiddleRight;
        _statusLabel.Padding = new Padding(12, 0, 8, 0);
        _statusLabel.Text = "";
        bottomBar.Controls.Add(_statusLabel);

        _flow.Dock = DockStyle.Fill;
        _flow.FlowDirection = FlowDirection.TopDown;
        _flow.WrapContents = false;
        _flow.AutoScroll = true;
        _flow.Padding = new Padding(6);
        _flow.Resize += (s, e) => FitRowWidths();

        Controls.Add(_flow);
        Controls.Add(bottomBar);
        _flow.BringToFront();

        BlocklistService.Changed += RefreshList;

        Theme.Apply(this);
        RefreshList();
    }

    private void Notify(string msg)
    {
        _statusLabel.Text = msg;
        StatusChanged?.Invoke(msg);
    }

    private Panel MakeRow(HistoryEntry e, int index)
    {
        bool blocked = BlocklistService.IsBlocked(e.Id);

        var row = new Panel
        {
            Height = 46,
            Margin = new Padding(3),
            Name = e.Id,
            Tag = "card",
            BackColor = Theme.ControlBack,
            ForeColor = Theme.Fore
        };

        var lblNo = new Label { Text = $"{index}.", Location = new Point(10, 14), AutoSize = true, BackColor = Color.Transparent, ForeColor = Theme.DisabledFore, Tag = "onCardDim" };
        var lblName = new Label
        {
            Text = (e.Favorite ? "★ " : "") + (blocked ? "🚫 " : "") + e.Id,
            Location = new Point(52, 13),
            AutoSize = true,
            Font = new Font(Font, FontStyle.Bold),
            BackColor = Color.Transparent,
            ForeColor = Theme.Fore,
            Tag = "onCard"
        };
        var lblDate = new Label { Text = e.AppliedAt.ToString("yyyy-MM-dd  HH:mm"), Location = new Point(240, 14), AutoSize = true, BackColor = Color.Transparent, ForeColor = Theme.DisabledFore, Tag = "onCardDim" };
        var lblRes = new Label { Text = e.Resolution, Location = new Point(440, 14), AutoSize = true, BackColor = Color.Transparent, ForeColor = Theme.DisabledFore, Tag = "onCardDim" };

        var btnSet = new Button { Text = "Set as Wallpaper", Size = new Size(120, 28), Location = new Point(500, 9) };
        var btnBlock = new Button { Text = blocked ? "Unblock" : "🚫 Block", Size = new Size(80, 28), Location = new Point(626, 9) };
        var btnCopy = new Button { Text = "Copy", Size = new Size(68, 28), Location = new Point(712, 9) };
        var btnOpen = new Button { Text = "Open", Size = new Size(68, 28), Location = new Point(786, 9) };

        Tips.For(btnSet, "Apply again — re-downloads first if the file is no longer cached.");
        Tips.For(btnBlock, "Block / unblock this wallpaper.");
        Tips.For(btnCopy, "Copy the Wallhaven link to the clipboard.");
        Tips.For(btnOpen, "Open the Wallhaven page in your browser.");

        btnSet.Click += async (s, ev) => await ApplyEntryAsync(e);
        btnBlock.Click += (s, ev) =>
        {
            if (BlocklistService.IsBlocked(e.Id))
            {
                BlocklistService.Remove(e.Id);
                Notify($"{e.Id} unblocked.");
            }
            else
            {
                BlocklistService.Add(e.Id, e.Resolution);
                Notify($"Blocked {e.Id} — never again.");
            }
        };
        btnCopy.Click += (s, ev) =>
        {
            Clipboard.SetText($"https://wallhaven.cc/w/{e.Id}");
            Notify($"Link copied: wallhaven.cc/w/{e.Id}");
        };
        btnOpen.Click += (s, ev) =>
            Process.Start(new ProcessStartInfo($"https://wallhaven.cc/w/{e.Id}") { UseShellExecute = true });

        row.Controls.AddRange(new Control[] { lblNo, lblName, lblDate, lblRes, btnSet, btnBlock, btnCopy, btnOpen });
        return row;
    }

    private void FitRowWidths()
    {
        int w = _flow.ClientSize.Width - 16;
        foreach (Control c in _flow.Controls)
        {
            if (c is not Panel row) continue;
            row.Width = w;
            var buttons = row.Controls.OfType<Button>().ToList();
            if (buttons.Count == 4)
            {
                buttons[0].Location = new Point(w - 344, 9);   // Set as Wallpaper
                buttons[1].Location = new Point(w - 218, 9);   // Block/Unblock
                buttons[2].Location = new Point(w - 132, 9);   // Copy
                buttons[3].Location = new Point(w - 58, 9);    // Open
            }
        }
    }

    private async Task ApplyEntryAsync(HistoryEntry e)
    {
        try
        {
            var path = e.FilePath;
            if (!File.Exists(path))
            {
                Notify($"Re-downloading {e.Id} from Wallhaven…");
                var url = await ResolveDownloadUrlAsync(e.Id);
                if (url is null)
                {
                    Notify($"Could not locate {e.Id} on Wallhaven's servers.");
                    return;
                }
                path = await _client.DownloadUrlAsync(e.Id, url, CacheService.Folder);
                e.FilePath = path;
            }

            WallpaperSetter.Set(path);
            HistoryService.MarkApplied(e);
            Applied?.Invoke(e.Id, path, e.Resolution);
            Notify($"Set {e.Id} as wallpaper.");
            RefreshList();
        }
        catch (Exception ex)
        {
            Notify($"Could not set wallpaper: {ex.Message}");
        }
    }

    private async Task<string?> ResolveDownloadUrlAsync(string id)
    {
        try
        {
            var wp = await _client.GetWallpaperAsync(id);
            if (wp is not null && !string.IsNullOrWhiteSpace(wp.Path)) return wp.Path;
        }
        catch { }

        foreach (var ext in new[] { "jpg", "png" })
        {
            var url = $"https://w.wallhaven.cc/full/{id.Substring(0, 2)}/wallhaven-{id}.{ext}";
            try { if (await _client.UrlExistsAsync(url)) return url; } catch { }
        }
        return null;
    }

    public void RefreshList()
    {
        _flow.SuspendLayout();
        while (_flow.Controls.Count > 0)
        {
            var c = _flow.Controls[0];
            _flow.Controls.Remove(c);
            c.Dispose();
        }
        _shown.Clear();

        int i = 1;
        foreach (var e in HistoryService.Entries)
        {
            _shown.Add(e);
            _flow.Controls.Add(MakeRow(e, i));
            i++;
        }
        _flow.ResumeLayout();
        FitRowWidths();
    }
}