using System.Diagnostics;
using System.Drawing.Drawing2D;
using WallpaperRotator.Services;

namespace WallpaperRotator;

/// <summary>Cache tab: thumbnails of every wallpaper still present in the cache folder.</summary>
public class HistoryForm : Form
{
    private readonly ListView _list = new();
    private readonly ImageList _thumbs = new();
    private readonly List<HistoryEntry> _shown = new();
    private readonly FluentButton _favBtn = new();
    private readonly FluentButton _blockBtn = new();
    private FluentButton _setBtn = null!;
    private FluentButton _viewBtn = null!;
    private readonly Label _statusLabel = new();

    public event Action<string>? StatusChanged;
    public event Action<string, string, string>? Applied;
    public event Action? FavoritesChanged;

    public HistoryForm()
    {
        ClientSize = new Size(960, 570);

        _thumbs.ImageSize = new Size(146, 88);
        _thumbs.ColorDepth = ColorDepth.Depth32Bit;

        _list.Location = new Point(12, 12);
        _list.Size = new Size(936, 500);
        _list.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        _list.View = View.LargeIcon;
        _list.LargeImageList = _thumbs;
        _list.MultiSelect = false;
        _list.HideSelection = false;
        Controls.Add(_list);

        var setBtn = new FluentButton { Text = "Set again", Icon = FluentGlyph.Photo, Location = new Point(12, 524), Size = new Size(110, 34) };
        _setBtn = setBtn;
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

        var viewBtn = new FluentButton { Text = "View in Wallhaven", Icon = FluentGlyph.Open, Location = new Point(132, 524), Size = new Size(158, 34) };
        _viewBtn = viewBtn;
        viewBtn.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
        viewBtn.Click += (s, e) =>
        {
            var sel = Selected();
            if (sel is null) return;
            Process.Start(new ProcessStartInfo($"https://wallhaven.cc/w/{sel.Id}") { UseShellExecute = true });
        };

        _favBtn.Text = "Favorite";
        _favBtn.Icon = FluentGlyph.Star;
        _favBtn.Location = new Point(298, 524);
        _favBtn.Size = new Size(112, 34);
        _favBtn.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
        _favBtn.Click += (s, e) => ToggleFavorite();

        _blockBtn.Text = "Block";
        _blockBtn.Icon = FluentGlyph.Block;
        _blockBtn.Location = new Point(418, 524);
        _blockBtn.Size = new Size(100, 34);
        _blockBtn.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
        _blockBtn.Click += (s, e) => ToggleBlock();

        var openBtn = new FluentButton { Text = "Open cache", Icon = FluentGlyph.Folder, Location = new Point(526, 524), Size = new Size(112, 34)};
        openBtn.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
        openBtn.Click += (s, e) => Process.Start("explorer.exe", CacheService.Folder);

        var delBtn = new FluentButton { Text = "Delete cache", Icon = FluentGlyph.Trash, Location = new Point(646, 524), Size = new Size(112, 34), Variant = FluentButtonVariant.Danger };
        delBtn.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
        delBtn.Click += (s, e) => ConfirmDeleteCache();

        _statusLabel.Location = new Point(766, 524);
        _statusLabel.Size = new Size(182, 34);
        _statusLabel.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
        _statusLabel.TextAlign = ContentAlignment.MiddleRight;
        _statusLabel.Text = "";
        Controls.Add(_statusLabel);

        Controls.Add(setBtn);
        Controls.Add(viewBtn);
        Controls.Add(_favBtn);
        Controls.Add(_blockBtn);
        Controls.Add(openBtn);
        Controls.Add(delBtn);

        setBtn.BringToFront();
        viewBtn.BringToFront();
        _favBtn.BringToFront();
        _blockBtn.BringToFront();
        openBtn.BringToFront();
        delBtn.BringToFront();

        _list.SelectedIndexChanged += (s, e) =>
        {
            UpdateFavButtonText();
            UpdateBlockButtonText();
            UpdateSelectionButtons();
        };

        void LayoutBottom()
        {
            int y = Math.Max(12, ClientSize.Height - 46);
            _list.SetBounds(12, 12, Math.Max(120, ClientSize.Width - 24), Math.Max(60, y - 24));

            int x = 12;
            setBtn.Location = new Point(x, y); x += setBtn.Width + 8;
            viewBtn.Location = new Point(x, y); x += viewBtn.Width + 8;
            _favBtn.Location = new Point(x, y); x += _favBtn.Width + 8;
            _blockBtn.Location = new Point(x, y); x += _blockBtn.Width + 8;
            openBtn.Location = new Point(x, y); x += openBtn.Width + 8;
            delBtn.Location = new Point(x, y);

            _statusLabel.SetBounds(Math.Max(x + delBtn.Width + 12, ClientSize.Width - 194), y,
                182, 34);
        }

        Resize += (s, e) => LayoutBottom();
        LayoutBottom();

        BlocklistService.Changed += RefreshList;

        Tips.For(setBtn, "Set the selected cached wallpaper as your desktop wallpaper now.");
        Tips.For(viewBtn, "Open the selected wallpaper's Wallhaven page.");
        Tips.For(_favBtn, "Favorite / unfavorite the selected wallpaper.");
        Tips.For(_blockBtn, "Block / unblock the selected wallpaper (blocked = never rotates again).");
        Tips.For(openBtn, "Open the cache folder in Explorer.");
        Tips.For(delBtn, "Delete every cached file except favorites — asks for confirmation first.");

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

    private void UpdateFavButtonText()
    {
        var sel = Selected();
        bool fav = sel is not null && sel.Favorite;
        _favBtn.Text = fav ? "Unfavorite" : "Favorite";
        _favBtn.Icon = fav ? FluentGlyph.StarFilled : FluentGlyph.Star;
        _favBtn.Invalidate();
    }

    private void UpdateBlockButtonText()
    {
        var sel = Selected();
        bool blocked = sel is not null && BlocklistService.IsBlocked(sel.Id);
        _blockBtn.Text = blocked ? "Unblock" : "Block";
        _blockBtn.Icon = blocked ? FluentGlyph.Check : FluentGlyph.Block;
        _blockBtn.Invalidate();
    }

    private void UpdateSelectionButtons()
    {
        bool has = _list.SelectedIndices.Count == 1;
        _setBtn.Enabled = has;
        _viewBtn.Enabled = has;
        _favBtn.Enabled = has;
        _blockBtn.Enabled = has;
    }

    private void ToggleFavorite()
    {
        var sel = Selected();
        if (sel is null) return;
        sel.Favorite = !sel.Favorite;
        HistoryService.Save();
        FavoritesChanged?.Invoke();
        Notify(sel.Favorite ? $"{sel.Id} added to favorites." : $"{sel.Id} removed from favorites.");
        RefreshList();
    }

    private void ToggleBlock()
    {
        var sel = Selected();
        if (sel is null) return;
        if (BlocklistService.IsBlocked(sel.Id))
        {
            BlocklistService.Remove(sel.Id);
            Notify($"{sel.Id} unblocked.");
        }
        else
        {
            BlocklistService.Add(sel.Id, sel.Resolution);
            Notify($"Blocked {sel.Id} — never again.");
        }
    }

    private void ConfirmDeleteCache()
    {
        var answer = MessageBox.Show(
            this,
            "Delete all cached wallpaper files?\n\nFavorites are kept — everything else is removed.\n(Anything you need again can be re-downloaded from the History tab.)",
            "Delete cache",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning,
            MessageBoxDefaultButton.Button2);

        if (answer != DialogResult.Yes) return;

        CacheService.Clear(CacheService.Folder, HistoryService.FavoriteIds());
        RefreshList();
        Notify("Cache deleted (favorites kept).");
    }

    public void RefreshList()
    {
        _list.Items.Clear();
        _thumbs.Images.Clear();
        _shown.Clear();

        foreach (var e in HistoryService.Entries)
        {
            if (!File.Exists(e.FilePath)) continue;

            try { _thumbs.Images.Add(MakeThumb(e.FilePath, 146, 88)); }
            catch { continue; }

            var prefix = (e.Favorite ? "★ " : "") + (BlocklistService.IsBlocked(e.Id) ? "🚫 " : "");
            var caption = prefix + e.Id;
            _list.Items.Add(new ListViewItem(caption) { ImageIndex = _shown.Count });
            _shown.Add(e);
        }
        UpdateFavButtonText();
        UpdateBlockButtonText();
        UpdateSelectionButtons();
        if (_list.IsHandleCreated && _list.Items.Count > 0)
            _list.EnsureVisible(0);
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