using System.Drawing.Drawing2D;
using WallpaperRotator.Services;

namespace WallpaperRotator;

/// <summary>
/// Rounded tag search field with live Wallhaven suggestions.  Enter adds
/// whatever was typed (custom tags are allowed); clicking a suggestion adds
/// Wallhaven's canonical spelling.
/// </summary>
public class TagPicker : FluentField
{
    private readonly TextBox _edit = new();
    private readonly System.Windows.Forms.Timer _debounce = new() { Interval = 320 };

    private TagSuggestionPopup? _popup;
    private CancellationTokenSource? _cts;
    private string _placeholder = "";
    private bool _suppress;
    private bool _focused;
    private bool _skipFocusSearch;
    private bool _plusHot;

    /// <summary>Raised once for every tag the user commits (typed or picked).</summary>
    public event Action<string>? TagChosen;

    public TagPicker()
    {
        _edit.BorderStyle = BorderStyle.None;
        _edit.Font = Theme.UiFont;
        _edit.BackColor = BackColor;
        _edit.ForeColor = ForeColor;
        _edit.TextChanged += (s, e) => { if (!_suppress) KickSearch(); };
        _edit.GotFocus += (s, e) => OnEditGotFocus();
        _edit.LostFocus += (s, e) => OnEditLostFocus();
        _edit.KeyDown += OnEditKeyDown;
        Controls.Add(_edit);

        Height = 34;
        _debounce.Tick += async (s, e) =>
        {
            _debounce.Stop();
            await SearchAsync();
        };
    }

    public string PlaceholderText
    {
        get => _placeholder;
        set
        {
            _placeholder = value ?? "";
            _edit.PlaceholderText = _placeholder;
        }
    }

    /// <summary>Passed through from the Sketchy purity toggle.</summary>
    public bool AllowNsfw { get; set; }

    public override void ApplyTheme()
    {
        base.ApplyTheme();
        _edit.Font = Theme.UiFont;
        _edit.BackColor = BackColor;
        _edit.ForeColor = ForeColor;
        _edit.PlaceholderText = _placeholder;
        PerformLayout();
        Invalidate();
    }

    protected override bool ContainsFocusedChild() => _edit.Focused || Focused;

    protected override void OnLayout(LayoutEventArgs levent)
    {
        base.OnLayout(levent);
        int top = Math.Max(0, (Height - _edit.Height) / 2);
        _edit.SetBounds(11, top, Math.Max(0, Width - 46), _edit.Height);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);

        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        var plusBox = new RectangleF(Width - 29, (Height - 16) / 2f, 16, 16);
        FluentGlyphPainter.Draw(g, FluentGlyph.Plus, plusBox,
            _plusHot ? Theme.Accent : Theme.ForeSecondary, BackColor, 1.6f);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left) return;

        if (e.X >= Width - 36)
        {
            CommitTyped();
            return;
        }

        _edit.Focus();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        bool hot = e.X >= Width - 36;
        if (hot != _plusHot) { _plusHot = hot; Invalidate(); }
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        if (_plusHot) { _plusHot = false; Invalidate(); }
    }

    // ---------- focus ----------
    private void OnEditGotFocus()
    {
        _focused = true;
        Invalidate();

        // Returning focus after a commit should not immediately re-open the
        // suggestion list the user just picked from.
        if (_skipFocusSearch)
        {
            _skipFocusSearch = false;
            return;
        }

        KickSearch(immediate: true);
    }

    private void OnEditLostFocus()
    {
        // Focus can briefly move to the parent field while a layout runs; only
        // treat it as a real focus loss once the message queue settles.
        if (IsDisposed) return;

        BeginInvoke(new Action(() =>
        {
            if (IsDisposed || ContainsFocus) return;

            _focused = false;
            Invalidate();
            ClosePopup();
        }));
    }

    // ---------- keyboard ----------
    private void OnEditKeyDown(object? sender, KeyEventArgs e)
    {
        switch (e.KeyCode)
        {
            case Keys.Down:
                if (_popup is { Visible: true })
                {
                    _popup.MoveSelection(1);
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                }
                else
                {
                    KickSearch(immediate: true);
                }
                break;

            case Keys.Up:
                if (_popup is { Visible: true })
                {
                    _popup.MoveSelection(-1);
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                }
                break;

            case Keys.Enter:
                if (_popup is { Visible: true } && _popup.HasSelection)
                    _popup.CommitSelection();
                else
                    CommitTyped();
                e.Handled = true;
                e.SuppressKeyPress = true;
                break;

            case Keys.Escape:
                if (_popup is { Visible: true })
                {
                    ClosePopup();
                }
                else
                {
                    _suppress = true;
                    _edit.Clear();
                    _suppress = false;
                }
                e.Handled = true;
                e.SuppressKeyPress = true;
                break;
        }
    }

    private string CurrentQuery()
    {
        string text = _edit.Text.Trim();
        int comma = text.LastIndexOf(',');
        if (comma >= 0) text = text[(comma + 1)..].Trim();
        return text;
    }

    private void KickSearch(bool immediate = false)
    {
        if (!Enabled || !_focused) return;

        _debounce.Stop();
        _cts?.Cancel();

        if (immediate) _ = SafeSearchAsync();
        else _debounce.Start();
    }

    private async Task SafeSearchAsync()
    {
        try { await SearchAsync(); }
        catch (Exception ex) { LogService.Warn($"Tag suggestion search failed: {ex.Message}"); }
    }

    private async Task SearchAsync()
    {
        var cts = new CancellationTokenSource();
        _cts = cts;

        List<WallhavenTag> tags;
        try
        {
            tags = await TagCatalog.SearchAsync(CurrentQuery(), AllowNsfw, cts.Token);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch
        {
            tags = new List<WallhavenTag>();
        }

        if (cts.IsCancellationRequested || !_focused || !Enabled) return;
        OnTagsReady(tags, cts);
    }

    private void OnTagsReady(List<WallhavenTag> tags, CancellationTokenSource cts)
    {
        // The HTTP continuation can land on a pool thread when the form's
        // synchronization context is not installed yet (first focus), so make
        // sure the popup is created on the UI thread.
        if (IsDisposed || !IsHandleCreated) return;

        if (InvokeRequired)
        {
            BeginInvoke(new Action(() => OnTagsReady(tags, cts)));
            return;
        }

        if (cts.IsCancellationRequested || !_focused || !Enabled) return;
        ShowSuggestions(tags);
    }

    private void ShowSuggestions(List<WallhavenTag> tags)
    {
        if (tags.Count == 0)
        {
            ClosePopup();
            return;
        }

        if (_popup is null || _popup.IsDisposed)
        {
            // A child control of the tab is used instead of a floating window:
            // it paints above the tag rows without touching window activation.
            var host = FindForm();
            if (host is null) return;

            var popup = new TagSuggestionPopup();
            popup.Chosen += OnSuggestionChosen;
            host.Controls.Add(popup);
            _popup = popup;
        }

        _popup.SetItems(tags);
        PositionPopup();
        _popup.Visible = true;
        _popup.BringToFront();
    }

    private void PositionPopup()
    {
        if (_popup is null || _popup.IsDisposed) return;

        var host = _popup.Parent;
        if (host is null) return;

        int width = Math.Max(Width, 260);
        int height = _popup.PreferredHeight;

        var origin = PointToScreen(new Point(0, Height + 3));
        var hostOrigin = host.PointToScreen(Point.Empty);

        int x = origin.X - hostOrigin.X;
        int y = origin.Y - hostOrigin.Y;

        // A child control cannot draw outside its parent, so keep it on-screen
        // and flip above the field when there is not enough room below.
        x = Math.Clamp(x, 4, Math.Max(4, host.ClientSize.Width - width - 4));

        int maxY = host.ClientSize.Height - height - 4;
        if (y > maxY)
        {
            int above = PointToScreen(Point.Empty).Y - hostOrigin.Y - height - 3;
            y = above >= 4 ? above : Math.Max(4, maxY);
        }

        _popup.SetBounds(x, y, width, height);
    }

    private void OnSuggestionChosen(WallhavenTag tag)
    {
        CommitValues(new[] { tag.Name });
    }

    private void CommitTyped()
    {
        var values = _edit.Text
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(CleanTag)
            .Where(t => t.Length > 0)
            .ToList();

        if (values.Count == 0)
        {
            ClosePopup();
            return;
        }

        CommitValues(values);
    }

    private void CommitValues(IEnumerable<string> values)
    {
        var tags = values.Select(CleanTag).Where(t => t.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        _suppress = true;
        _edit.Clear();
        _suppress = false;
        ClosePopup();

        foreach (var tag in tags)
            TagChosen?.Invoke(tag);

        _skipFocusSearch = !_edit.Focused;
        if (!_edit.Focused) _edit.Focus();
    }

    private static string CleanTag(string tag) => (tag ?? "").Trim().Trim('"').Trim();

    private void ClosePopup()
    {
        if (_popup is { IsDisposed: false }) _popup.Visible = false;
    }

    /// <summary>Hides the suggestion list (called when the tab scrolls or resizes).</summary>
    public void CloseSuggestions()
    {
        ClosePopup();
        _debounce.Stop();
        _cts?.Cancel();
    }

    protected override void OnEnabledChanged(EventArgs e)
    {
        base.OnEnabledChanged(e);
        _edit.Enabled = Enabled;
        if (!Enabled) ClosePopup();
    }

    protected override void OnSizeChanged(EventArgs e)
    {
        base.OnSizeChanged(e);
        if (_popup is { Visible: true }) PositionPopup();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _debounce.Dispose();

            _popup?.Dispose();
            _popup = null;
            _edit.Dispose();
        }
        base.Dispose(disposing);
    }
}

/// <summary>
/// Suggestion list drawn as a child of the Tags tab.  It is deliberately not
/// selectable, so clicking a row leaves the keyboard focus in the text box.
/// </summary>
internal sealed class TagSuggestionPopup : Control
{
    private const int ItemHeight = 34;
    private const int MaxVisible = 8;
    private const int PadY = 6;

    private List<WallhavenTag> _items = new();
    private int _selected = -1;
    private int _scroll;
    private int _hot = -1;

    public event Action<WallhavenTag>? Chosen;

    public TagSuggestionPopup()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                 ControlStyles.SupportsTransparentBackColor, true);
        SetStyle(ControlStyles.Selectable, false);

        TabStop = false;
        Visible = false;
        BackColor = Theme.Card;
        ForeColor = Theme.Fore;
        AccessibleRole = AccessibleRole.List;
        AccessibleName = "Tag suggestions";
    }

    public bool HasSelection => _selected >= 0 && _selected < _items.Count;

    public int PreferredHeight =>
        Math.Min(Math.Max(_items.Count, 1), MaxVisible) * ItemHeight + PadY * 2;

    public void SetItems(List<WallhavenTag> items)
    {
        _items = items;
        _selected = -1;
        _scroll = 0;
        _hot = -1;
        Height = PreferredHeight;
        Invalidate();
    }

    public void MoveSelection(int delta)
    {
        if (_items.Count == 0) return;

        if (_selected < 0)
            _selected = delta > 0 ? 0 : _items.Count - 1;
        else
            _selected = (_selected + delta + _items.Count) % _items.Count;

        EnsureSelectedVisible();
        Invalidate();
    }

    public void CommitSelection()
    {
        if (!HasSelection) return;
        var tag = _items[_selected];
        Chosen?.Invoke(tag);
    }

    private void EnsureSelectedVisible()
    {
        if (_selected < 0) return;
        if (_selected < _scroll) _scroll = _selected;
        else if (_selected >= _scroll + MaxVisible) _scroll = _selected - MaxVisible + 1;
    }

    private int IndexAt(int y)
    {
        int local = y - PadY;
        if (local < 0) return -1;
        int row = local / ItemHeight;
        int index = _scroll + row;
        return index >= 0 && index < _items.Count ? index : -1;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        using (var bg = new SolidBrush(Theme.Card))
            g.FillRectangle(bg, ClientRectangle);

        for (int i = 0; i < MaxVisible; i++)
        {
            int index = _scroll + i;
            if (index >= _items.Count) break;

            var row = new RectangleF(5, PadY + i * ItemHeight, Width - 10, ItemHeight);
            bool selected = index == _selected;
            bool hot = index == _hot;

            if (selected || hot)
            {
                var fill = selected ? Theme.SelectedBack : Theme.Overlay(Theme.SubtleHover, Theme.Card);
                Theme.FillRounded(g, RectangleF.Inflate(row, -2, -1), 5f, fill);
            }

            var tag = _items[index];
            var textRect = new Rectangle((int)row.X + 12, (int)row.Y,
                (int)row.Width - 42, (int)row.Height);
            TextRenderer.DrawText(g, tag.Name, Theme.UiFont, textRect, Theme.Fore,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter |
                TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis |
                TextFormatFlags.NoPadding);

            var plus = new RectangleF(row.Right - 28, row.Y + (row.Height - 14) / 2f, 14, 14);
            FluentGlyphPainter.Draw(g, FluentGlyph.Plus, plus,
                selected ? Theme.Accent : Theme.ForeSecondary, Theme.Card, 1.5f);
        }

        var border = new RectangleF(0.5f, 0.5f, Width - 1f, Height - 1f);
        Theme.DrawRounded(g, border, 8f, Theme.ControlBorder);
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        using var path = Theme.RoundedRect(new RectangleF(0, 0, Width, Height), 8f);
        Region?.Dispose();
        Region = new Region(path);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        int index = IndexAt(e.Y);
        if (index == _hot) return;
        _hot = index;
        Invalidate();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        if (_hot < 0) return;
        _hot = -1;
        Invalidate();
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left) return;

        int index = IndexAt(e.Y);
        if (index < 0) return;

        _selected = index;
        Chosen?.Invoke(_items[index]);
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);
        if (_items.Count <= MaxVisible) return;

        int maxScroll = Math.Max(0, _items.Count - MaxVisible);
        int next = Math.Clamp(_scroll - Math.Sign(e.Delta), 0, maxScroll);
        if (next == _scroll) return;

        _scroll = next;
        _hot = -1;
        Invalidate();
    }
}
