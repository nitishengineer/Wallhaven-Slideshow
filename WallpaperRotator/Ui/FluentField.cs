using System.Diagnostics.CodeAnalysis;
using System.Drawing.Drawing2D;
using System.Globalization;

namespace WallpaperRotator;

/// <summary>Base for rounded Fluent input surfaces (text, number, combo).</summary>
public abstract class FluentField : Control
{
    protected float CornerRadius { get; set; } = 5f;
    private bool _hover;

    protected FluentField()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        Height = 30;
        BackColor = Theme.ControlBack;
        ForeColor = Theme.Fore;
        Font = Theme.UiFont;
    }

    public virtual void ApplyTheme()
    {
        Font = Theme.UiFont;
        BackColor = Theme.ControlBack;
        ForeColor = Theme.Fore;
        Invalidate();
    }

    protected override void OnPaintBackground(PaintEventArgs pevent)
    {
        using var brush = new SolidBrush(Parent?.BackColor ?? Theme.Back);
        pevent.Graphics.FillRectangle(brush, ClientRectangle);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        bool focused = ContainsFocusedChild();
        Color border = focused ? Theme.Accent
                     : _hover ? Theme.ControlBorderHover
                     : Theme.ControlBorder;
        var rect = new RectangleF(0.5f, 0.5f, Width - 1f, Height - 1f);
        Theme.FillAndStroke(g, rect, CornerRadius, BackColor, border, focused ? 1.6f : 1f);
    }

    protected virtual bool ContainsFocusedChild() => Focused;

    protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
    protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }
    protected override void OnResize(EventArgs e) { base.OnResize(e); PerformLayout(); }
}

/// <summary>Rounded single-line text field.</summary>
public class FluentTextBox : FluentField
{
    private readonly TextBox _inner = new();

    public FluentTextBox()
    {
        _inner.BorderStyle = BorderStyle.None;
        _inner.Font = Theme.UiFont;
        _inner.BackColor = BackColor;
        _inner.ForeColor = ForeColor;
        _inner.TextChanged += (s, e) => OnTextChanged(EventArgs.Empty);
        _inner.GotFocus += (s, e) => Invalidate();
        _inner.LostFocus += (s, e) => Invalidate();
        Controls.Add(_inner);
    }

    [AllowNull]
    public override string Text
    {
        get => _inner.Text;
        set => _inner.Text = value;
    }

    public bool UseSystemPasswordChar
    {
        get => _inner.UseSystemPasswordChar;
        set => _inner.UseSystemPasswordChar = value;
    }

    public TextBox InnerTextBox => _inner;

    public override void ApplyTheme()
    {
        base.ApplyTheme();
        _inner.Font = Theme.UiFont;
        _inner.BackColor = BackColor;
        _inner.ForeColor = ForeColor;
        PerformLayout();
        Invalidate();
    }

    protected override bool ContainsFocusedChild() => _inner.Focused || Focused;

    protected override void OnLayout(LayoutEventArgs levent)
    {
        base.OnLayout(levent);
        int top = Math.Max(0, (Height - _inner.Height) / 2);
        _inner.SetBounds(11, top, Math.Max(0, Width - 22), _inner.Height);
    }

    protected override void OnBackColorChanged(EventArgs e)
    {
        base.OnBackColorChanged(e);
        if (_inner is not null) _inner.BackColor = BackColor;
    }

    protected override void OnForeColorChanged(EventArgs e)
    {
        base.OnForeColorChanged(e);
        if (_inner is not null) _inner.ForeColor = ForeColor;
    }

    protected override void OnEnabledChanged(EventArgs e)
    {
        base.OnEnabledChanged(e);
        _inner.Enabled = Enabled;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _inner.Dispose();
        base.Dispose(disposing);
    }
}

/// <summary>Rounded numeric field with custom chevrons (no native spinner chrome).</summary>
public class FluentNumberBox : FluentField
{
    private readonly TextBox _edit = new();
    private decimal _value;
    private decimal _minimum;
    private decimal _maximum = 100m;
    private decimal _increment = 1m;
    private int _decimals;
    private bool _formatting;
    private int _hotArrow;

    public event EventHandler? ValueChanged;

    public FluentNumberBox()
    {
        _edit.BorderStyle = BorderStyle.None;
        _edit.Font = Theme.UiFont;
        _edit.BackColor = BackColor;
        _edit.ForeColor = ForeColor;
        _edit.TextAlign = HorizontalAlignment.Left;
        _edit.TextChanged += OnEditTextChanged;
        _edit.KeyDown += OnEditKeyDown;
        _edit.GotFocus += (s, e) => Invalidate();
        _edit.LostFocus += (s, e) => { CommitText(); Invalidate(); };
        Controls.Add(_edit);
        FormatText();
    }

    public decimal Minimum
    {
        get => _minimum;
        set
        {
            _minimum = value;
            if (_maximum < _minimum) _maximum = _minimum;
            SetValue(_value, true);
        }
    }

    public decimal Maximum
    {
        get => _maximum;
        set
        {
            _maximum = value;
            if (_minimum > _maximum) _minimum = _maximum;
            SetValue(_value, true);
        }
    }

    public decimal Value
    {
        get => _value;
        set => SetValue(value, true);
    }

    public decimal Increment
    {
        get => _increment;
        set => _increment = value <= 0 ? 1m : value;
    }

    public int DecimalPlaces
    {
        get => _decimals;
        set
        {
            _decimals = Math.Clamp(value, 0, 6);
            FormatText();
        }
    }

    private decimal Clamp(decimal value)
    {
        if (_maximum < _minimum) return _minimum;
        return Math.Min(Math.Max(value, _minimum), _maximum);
    }

    private void SetValue(decimal value, bool raise)
    {
        decimal v = Clamp(value);
        if (v == _value)
        {
            FormatText();
            return;
        }
        _value = v;
        FormatText();
        if (raise) ValueChanged?.Invoke(this, EventArgs.Empty);
    }

    private void FormatText()
    {
        _formatting = true;
        _edit.Text = _value.ToString("F" + _decimals, CultureInfo.CurrentCulture);
        _formatting = false;
    }

    private static bool TryParse(string? text, out decimal value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(text)) return false;
        return decimal.TryParse(text, NumberStyles.Number, CultureInfo.CurrentCulture, out value)
            || decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out value);
    }

    private void OnEditTextChanged(object? sender, EventArgs e)
    {
        if (_formatting) return;
        if (!TryParse(_edit.Text, out decimal parsed)) return;
        if (parsed < _minimum || parsed > _maximum) return;
        if (parsed == _value) return;
        _value = parsed;
        ValueChanged?.Invoke(this, EventArgs.Empty);
    }

    private void CommitText()
    {
        if (!TryParse(_edit.Text, out decimal parsed)) parsed = _value;
        SetValue(parsed, true);
        FormatText();
    }

    private void OnEditKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Up)
        {
            e.Handled = true;
            SetValue(_value + _increment, true);
        }
        else if (e.KeyCode == Keys.Down)
        {
            e.Handled = true;
            SetValue(_value - _increment, true);
        }
        else if (e.KeyCode == Keys.Enter)
        {
            e.Handled = true;
            CommitText();
        }
        else if (e.KeyCode == Keys.Escape)
        {
            e.Handled = true;
            FormatText();
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
        _edit.SetBounds(10, top, Math.Max(0, Width - 42), _edit.Height);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        float cx = Width - 15f;
        float cy = Height / 2f;
        var upColor = _hotArrow == 1 ? Theme.Fore : Theme.ForeSecondary;
        var downColor = _hotArrow == 2 ? Theme.Fore : Theme.ForeSecondary;

        using var upPen = new Pen(upColor, 1.5f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
        g.DrawLines(upPen, new[]
        {
            new PointF(cx - 4.5f, cy - 4f),
            new PointF(cx, cy - 8f),
            new PointF(cx + 4.5f, cy - 4f)
        });

        using var downPen = new Pen(downColor, 1.5f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
        g.DrawLines(downPen, new[]
        {
            new PointF(cx - 4.5f, cy + 4f),
            new PointF(cx, cy + 8f),
            new PointF(cx + 4.5f, cy + 4f)
        });
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        int hot = e.X >= Width - 30 ? (e.Y < Height / 2 ? 1 : 2) : 0;
        if (hot != _hotArrow) { _hotArrow = hot; Invalidate(); }
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        if (_hotArrow != 0) { _hotArrow = 0; Invalidate(); }
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left || e.X < Width - 30) return;
        Focus();
        _edit.Focus();
        SetValue(e.Y < Height / 2 ? _value + _increment : _value - _increment, true);
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
        if (disposing) _edit.Dispose();
        base.Dispose(disposing);
    }
}
