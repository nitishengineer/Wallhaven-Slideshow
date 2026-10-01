using System.Diagnostics;
using System.Drawing.Drawing2D;
using WallpaperRotator.Services;

namespace WallpaperRotator;

/// <summary>Favorite tab: only wallpapers marked ★ (that still exist in cache).</summary>
public class FavoriteForm : Form
{
    private readonly ListView _list = new();
    private readonly ImageList _thumbs = new();
    private readonly List<HistoryEntry> _shown = new();
    private readonly Label _statusLabel = new();

    public event Action<string>? StatusChanged;
    public event Action<string, string, string>? Applied;
    public event Action? FavoritesChanged;

    public FavoriteForm()
    {
        ClientSize = new Size(960, 570);   // MUST stay the first line (anchor snapshot rule)

        _thumbs.ImageSize = new Size(150, 90);
        _thumbs.ColorDepth = ColorDepth.Depth32Bit;

        _list.Location = new Point(12, 12);
        _list.Size = new Size(936, 500);
        _list.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        _list.View = View.LargeIcon;
        _list.LargeImageList = _thumbs;
        _list.MultiSelect = false;
        _list.HideSelection = false;
        Controls.Add(_list);

        var setBtn = new Button { Text = "Set again", Location = new Point(12, 524), Size = new Size(110, 34) };
        setBtn.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
        setBtn.Click += (s, e) =>
        {
            var sel = Selected();
            if (sel is null || !File.Exists(sel.FilePath)) return;
            WallpaperSetter.Set(sel.FilePath);
            HistoryService.MarkApplied(sel);
            Applied?.Invoke(sel.Id, sel.FilePath, sel.Resolution);
            Notify($"Set {sel.Id} as wallpaper.");
            RefreshList();
        };

        var viewBtn = new Button { Text = "View in Wallhaven", Location = new Point(132, 524), Size = new Size(150, 34) };
        viewBtn.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
        viewBtn.Click += (s, e) =>
        {
            var sel = Selected();
            if (sel is null) return;
            Process.Start(new ProcessStartInfo($"https://wallhaven.cc/w/{sel.Id}") { UseShellExecute = true });
        };

        var unfavBtn = new Button { Text = "Unfavorite", Location = new Point(292, 524), Size = new Size(110, 34) };
        unfavBtn.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
        unfavBtn.Click += (s, e) =>
        {
            var sel = Selected();
            if (sel is null) return;
            sel.Favorite = false;
            HistoryService.Save();
            FavoritesChanged?.Invoke();
            Notify($"{sel.Id} removed from favorites.");
            RefreshList();
        };

        _statusLabel.Location = new Point(412, 524);
        _statusLabel.Size = new Size(536, 34);
        _statusLabel.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
        _statusLabel.TextAlign = ContentAlignment.MiddleRight;
        _statusLabel.Text = "";
        Controls.Add(_statusLabel);

        Controls.Add(setBtn);
        Controls.Add(viewBtn);
        Controls.Add(unfavBtn);
        setBtn.BringToFront();
        viewBtn.BringToFront();
        unfavBtn.BringToFront();

        Tips.For(setBtn, "Set the selected favorite as your desktop wallpaper now.");
        Tips.For(viewBtn, "Open the selected wallpaper's Wallhaven page.");
        Tips.For(unfavBtn, "Remove the favorite mark — the wallpaper leaves this tab (its file stays cached).");

        Theme.Apply(this);
        RefreshList();
    }

    private void Notify(string msg)
    {
        _statusLabel.Text = msg;
        StatusChanged?.Invoke(msg);
    }

    private HistoryEntry? Selected() =>
        _list.SelectedIndices.Count == 1 ? _shown[_list.SelectedIndices[0]] : null;

    public void RefreshList()
    {
        _list.Items.Clear();
        _thumbs.Images.Clear();
        _shown.Clear();

        foreach (var e in HistoryService.Entries)
        {
            if (!e.Favorite || !File.Exists(e.FilePath)) continue;

            try { _thumbs.Images.Add(MakeThumb(e.FilePath, 150, 90)); }
            catch { continue; }

            _list.Items.Add(new ListViewItem(e.Id) { ImageIndex = _shown.Count });
            _shown.Add(e);
        }
    }

    private static Bitmap MakeThumb(string path, int w, int h)
    {
        using var src = Image.FromFile(path);
        var bmp = new Bitmap(w, h);
        using var g = Graphics.FromImage(bmp);
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.Clear(Color.FromArgb(25, 25, 28));
        var ratio = Math.Min((float)w / src.Width, (float)h / src.Height);
        var dw = src.Width * ratio;
        var dh = src.Height * ratio;
        g.DrawImage(src, (w - dw) / 2, (h - dh) / 2, dw, dh);
        return bmp;
    }
}