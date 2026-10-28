using System.Diagnostics.CodeAnalysis;
using System.Drawing.Drawing2D;

namespace WallpaperRotator;

/// <summary>Small list wrapper mimicking the parts of ComboBox.ObjectCollection the app uses.</summary>
public sealed class FluentItemCollection
{
    private readonly List<object> _items = new();

    public int Count => _items.Count;
    public object this[int index] => _items[index];

    public int Add(object? item)
    {
        _items.Add(item ?? "");
        return _items.Count - 1;
    }

    public void AddRange(object[] items) => _items.AddRange(items);
    public void Clear() => _items.Clear();
    public bool Contains(object? item) => IndexOf(item) >= 0;

    public int IndexOf(object? item)
    {
        if (item is null) return -1;
        string text = item.ToString() ?? "";
        return _items.FindIndex(i => string.Equals(i?.ToString(), text, StringComparison.Ordinal));
    }

    internal List<object> Snapshot() => new(_items);
}

/// <summary>Fluent drop-down: rounded field, vector chevron, themed popup list.</summary>
public class FluentComboBox : FluentField
{
    private readonly TextBox _edit = new();
    private FluentDropDownForm? _dropDown;
    private ComboBoxStyle _style = ComboBoxStyle.DropDownList;
    private int _selectedIndex = -1;
    private string _text = "";
    private bool _updating;
    private bool _cancelOpen;

    public FluentItemCollection Items { get; } = new();
    public bool ShowColorSwatches { get; set; }

    public event EventHandler? SelectedIndexChanged;
    public event EventHandler? DropDown;

    public FluentComboBox()
    {
        _edit.BorderStyle = BorderStyle.None;
        _edit.Font = Theme.UiFont;
        _edit.BackColor = BackColor;
        _edit.ForeColor = ForeColor;
        _edit.Visible = false;
        _edit.TextChanged += (s, e) => { if (!_updating) OnTextChanged(EventArgs.Empty); };
        _edit.GotFocus += (s, e) => Invalidate();
        _edit.LostFocus += (s, e) => Invalidate();
        _edit.KeyDown += EditKeyDown;
        Controls.Add(_edit);
        Height = 30;
        Cursor = Cursors.Default;
    }

    public ComboBoxStyle DropDownStyle
    {
        get => _style;
        set
        {
            if (_style == value) return;
            _style = value;
            _edit.Visible = value == ComboBoxStyle.DropDown;
            if (value == ComboBoxStyle.DropDownList && _selectedIndex < 0 && _text.Length > 0)
            {
                int idx = Items.IndexOf(_text);
                if (idx >= 0) SelectedIndex = idx;
            }
            PerformLayout();
            Invalidate();
        }
    }

    public int SelectedIndex
    {
        get => _selectedIndex;
        set
        {
            int idx = value < 0 || value >= Items.Count ? -1 : value;
            if (idx == _selectedIndex) return;
            _selectedIndex = idx;

            string text = idx >= 0 ? Items[idx]?.ToString() ?? "" : "";
            _text = text;
            _updating = true;
            if (_style == ComboBoxStyle.DropDown) _edit.Text = text;
            _updating = false;

            SelectedIndexChanged?.Invoke(this, EventArgs.Empty);
            OnTextChanged(EventArgs.Empty);
            Invalidate();
        }
    }

    public object? SelectedItem
    {
        get => _selectedIndex >= 0 && _selectedIndex < Items.Count ? Items[_selectedIndex] : null;
        set => SelectedIndex = value is null ? -1 : Items.IndexOf(value);
    }

    [AllowNull]
    public override string Text
    {
        get => _style == ComboBoxStyle.DropDown ? _edit.Text : _text;
        set
        {
            if (_style == ComboBoxStyle.DropDown)
            {
                if (_edit.Text != value) _edit.Text = value;
                return;
            }
            if (_text == value) return;
            int idx = Items.IndexOf(value);
            if (idx >= 0)
            {
                SelectedIndex = idx;
            }
            else
            {
                _text = value ?? "";
                OnTextChanged(EventArgs.Empty);
                Invalidate();
            }
        }
    }

    public bool DroppedDown
    {
        get => _dropDown is { IsDisposed: false, Visible: true };
        set
        {
            if (value) OpenDropDown();
            else { _cancelOpen = true; CloseDropDown(); }
        }
    }

    public override void ApplyTheme()
    {
        base.ApplyTheme();
        _edit.Font = Theme.UiFont;
        _edit.BackColor = BackColor;
        _edit.ForeColor = ForeColor;
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

        if (_style == ComboBoxStyle.DropDownList)
        {
            var textRect = new Rectangle(11, 0, Math.Max(0, Width - 44), Height);
            TextRenderer.DrawText(g, _text, Theme.UiFont, textRect, ForeColor,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter |
                TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
        }

        var chevron = new RectangleF(Width - 27, (Height - 16) / 2f, 16, 16);
        FluentGlyphPainter.Draw(g, FluentGlyph.ChevronDown, chevron, Theme.ForeSecondary, BackColor);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        Focus();
        bool onEdit = _style == ComboBoxStyle.DropDown && e.X < Width - 34;
        if (onEdit)
        {
            _edit.Focus();
            return;
        }
        ToggleDropDown();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.KeyCode == Keys.F4 || (e.Alt && e.KeyCode == Keys.Down))
        {
            e.Handled = true;
            OpenDropDown();
        }
    }

    private void EditKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Down || (e.Alt && e.KeyCode == Keys.Down))
        {
            e.Handled = true;
            OpenDropDown();
        }
    }

    public void OpenDropDown()
    {
        if (DroppedDown) return;
        _cancelOpen = false;
        DropDown?.Invoke(this, EventArgs.Empty);
        if (_cancelOpen || DroppedDown) return;
        if (Items.Count == 0) return;

        var owner = FindForm();
        if (owner is null) return;

        _dropDown = new FluentDropDownForm(Items.Snapshot(), _selectedIndex, ShowColorSwatches);
        int width = Math.Max(Width, 140);
        _dropDown.Width = width;

        var origin = PointToScreen(new Point(0, Height + 2));
        var wa = Screen.FromControl(this).WorkingArea;
        int x = Math.Min(origin.X, wa.Right - width - 8);
        int y = origin.Y;
        if (y + _dropDown.Height > wa.Bottom)
            y = PointToScreen(Point.Empty).Y - _dropDown.Height - 2;
        _dropDown.Location = new Point(x, y);

        _dropDown.IndexChosen += idx =>
        {
            SelectedIndex = idx;
            CloseDropDown();
        };
        _dropDown.FormClosed += (s, e) => _dropDown = null;
        _dropDown.Show(owner);
        _dropDown.Activate();
        Invalidate();
    }

    public void CloseDropDown()
    {
        if (_dropDown is { IsDisposed: false }) _dropDown.Close();
        _dropDown = null;
        Invalidate();
    }

    private void ToggleDropDown()
    {
        if (DroppedDown) CloseDropDown();
        else OpenDropDown();
    }

    protected override void OnBackColorChanged(EventArgs e)
    {
        base.OnBackColorChanged(e);
        if (_edit is not null) _edit.BackColor = BackColor;
    }

    protected override void OnForeColorChanged(EventArgs e)
    {
        base.OnForeColorChanged(e);
        if (_edit is not null) _edit.ForeColor = ForeColor;
    }

    protected override void OnEnabledChanged(EventArgs e)
    {
        base.OnEnabledChanged(e);
        _edit.Enabled = Enabled;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _dropDown?.Close();
            _edit.Dispose();
        }
        base.Dispose(disposing);
    }
}

/// <summary>Borderless rounded popup list used by FluentComboBox.</summary>
internal sealed class FluentDropDownForm : Form
{
    private const int CS_DROPSHADOW = 0x00020000;
    private const int WS_EX_TOOLWINDOW = 0x00000080;
    private const int ItemHeightPx = 34;

    private readonly ListBox _list = new();
    private readonly bool _swatches;
    private int _hotIndex = -1;

    public event Action<int>? IndexChosen;

    public FluentDropDownForm(List<object> items, int selectedIndex, bool swatches)
    {
        _swatches = swatches;
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        BackColor = Theme.Card;
        DoubleBuffered = true;
        Padding = new Padding(5);

        int visible = Math.Min(items.Count, 10);
        Height = Math.Max(1, visible) * ItemHeightPx + 12;

        _list.BorderStyle = BorderStyle.None;
        _list.DrawMode = DrawMode.OwnerDrawFixed;
        _list.ItemHeight = ItemHeightPx;
        _list.IntegralHeight = false;
        _list.Dock = DockStyle.Fill;
        _list.BackColor = Theme.Card;
        _list.ForeColor = Theme.Fore;
        _list.Font = Theme.UiFont;
        foreach (var item in items) _list.Items.Add(item);
        if (selectedIndex >= 0 && selectedIndex < _list.Items.Count)
            _list.SelectedIndex = selectedIndex;

        _list.DrawItem += DrawItem;
        _list.MouseMove += (s, e) =>
        {
            int idx = _list.IndexFromPoint(e.Location);
            if (idx != _hotIndex) { _hotIndex = idx; _list.Invalidate(); }
        };
        _list.MouseLeave += (s, e) => { _hotIndex = -1; _list.Invalidate(); };
        _list.MouseUp += (s, e) =>
        {
            int idx = _list.IndexFromPoint(e.Location);
            if (idx >= 0) Choose(idx);
        };
        _list.KeyDown += (s, e) =>
        {
            if (e.KeyCode == Keys.Enter) { if (_list.SelectedIndex >= 0) Choose(_list.SelectedIndex); }
            else if (e.KeyCode == Keys.Escape) Close();
        };

        Controls.Add(_list);
        Theme.ThemeNativeScrollbar(_list);

        Shown += (s, e) => { Activate(); _list.Focus(); };
        Paint += (s, e) =>
        {
            var rect = new RectangleF(0.5f, 0.5f, Width - 1f, Height - 1f);
            Theme.DrawRounded(e.Graphics, rect, 8f, Theme.ControlBorder);
        };
        Resize += (s, e) =>
        {
            using var path = Theme.RoundedRect(new RectangleF(0, 0, Width, Height), 8f);
            Region?.Dispose();
            Region = new Region(path);
        };
    }

    protected override bool ShowWithoutActivation => false;

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ClassStyle |= CS_DROPSHADOW;
            cp.ExStyle |= WS_EX_TOOLWINDOW;
            return cp;
        }
    }

    protected override void OnDeactivate(EventArgs e)
    {
        base.OnDeactivate(e);
        Close();
    }

    private void Choose(int index)
    {
        IndexChosen?.Invoke(index);
    }

    private void DrawItem(object? sender, DrawItemEventArgs e)
    {
        if (e.Index < 0) return;
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        using (var bg = new SolidBrush(Theme.Card))
            g.FillRectangle(bg, e.Bounds);

        bool selected = (e.State & DrawItemState.Selected) != 0;
        bool hot = e.Index == _hotIndex;
        if (selected || hot)
        {
            var highlight = new RectangleF(e.Bounds.X + 3, e.Bounds.Y + 2, e.Bounds.Width - 6, e.Bounds.Height - 4);
            Theme.FillRounded(g, highlight, 5f, selected ? Theme.SelectedBack : Theme.Overlay(Theme.SubtleHover, Theme.Card));
        }

        var item = _list.Items[e.Index];
        string text = item?.ToString() ?? "";
        int textX = e.Bounds.X + 14;

        if (_swatches && e.Index > 0 && IsHex(text))
        {
            var swatch = new RectangleF(e.Bounds.X + 12, e.Bounds.Y + (e.Bounds.Height - 18) / 2f, 26, 18);
            Theme.FillRounded(g, swatch, 4f, ColorTranslator.FromHtml("#" + text));
            Theme.DrawRounded(g, swatch, 4f, Theme.ControlBorder);
            textX = e.Bounds.X + 48;
            text = "#" + text;
        }

        var textRect = new Rectangle(textX, e.Bounds.Y, Math.Max(0, e.Bounds.Right - textX - 8), e.Bounds.Height);
        TextRenderer.DrawText(g, text, Theme.UiFont, textRect, Theme.Fore,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter |
            TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
    }

    private static bool IsHex(string s)
    {
        if (s.Length != 6) return false;
        foreach (char c in s)
            if (!Uri.IsHexDigit(c)) return false;
        return true;
    }
}
