using System.Drawing.Drawing2D;

namespace WallpaperRotator;

/// <summary>Windows 11 style toggle switch (label + animated track).</summary>
public class FluentToggleSwitch : Control
{
    public int TrackWidth { get; set; } = 36;
    public int TrackHeight { get; set; } = 20;
    public int LabelGap { get; set; } = 8;

    public event EventHandler? CheckedChanged;

    private readonly System.Windows.Forms.Timer _anim = new() { Interval = 15 };
    private bool _checked;
    private bool _hover;
    private bool _autoSize;
    private float _t;

    public FluentToggleSwitch()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                 ControlStyles.Selectable, true);
        Font = Theme.UiFont;
        ForeColor = Theme.Fore;
        BackColor = Theme.Back;
        Height = TrackHeight + 2;
        TabStop = true;
        _anim.Tick += (s, e) =>
        {
            float target = _checked ? 1f : 0f;
            float diff = target - _t;
            if (Math.Abs(diff) <= 0.08f)
            {
                _t = target;
                _anim.Stop();
            }
            else
            {
                _t += diff * 0.35f;
            }
            Invalidate();
        };
    }

    public bool Checked
    {
        get => _checked;
        set
        {
            if (_checked == value) return;
            _checked = value;
            if (IsHandleCreated) _anim.Start();
            else _t = value ? 1f : 0f;
            CheckedChanged?.Invoke(this, EventArgs.Empty);
            Invalidate();
        }
    }

    /// <summary>Manual auto-size: absolute layout, so we resize ourselves when the label changes.</summary>
    public new bool AutoSize
    {
        get => _autoSize;
        set
        {
            _autoSize = value;
            if (value) Size = Preferred;
        }
    }

    private Size Preferred => GetPreferredSize(Size.Empty);

    public void ApplyTheme()
    {
        Font = Theme.UiFont;
        ForeColor = Theme.Fore;
        BackColor = Theme.Back;
        Invalidate();
    }

    public override Size GetPreferredSize(Size proposedSize)
    {
        var text = TextRenderer.MeasureText(Text ?? "", Theme.UiFont, Size.Empty,
            TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
        int w = TrackWidth + (string.IsNullOrEmpty(Text) ? 0 : LabelGap + text.Width) + 2;
        int h = Math.Max(TrackHeight, text.Height) + 2;
        return new Size(w, h);
    }

    protected override void OnTextChanged(EventArgs e)
    {
        if (_autoSize) Size = Preferred;
        Invalidate();
        base.OnTextChanged(e);
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
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        var track = new RectangleF(1f, (Height - TrackHeight) / 2f, TrackWidth, TrackHeight);
        Color trackFill = Theme.Blend(Theme.ControlBack, Theme.Accent, _t);
        Color trackBorder = _t > 0.5f
            ? Theme.Blend(Theme.Accent, Color.Black, 0.12f)
            : (_hover ? Theme.ControlBorderHover : Theme.ControlBorder);
        Theme.FillAndStroke(g, track, TrackHeight / 2f, trackFill, trackBorder);

        float knobD = TrackHeight - 6f;
        float knobX = track.X + 3f + (TrackWidth - knobD - 6f) * _t;
        var knob = new RectangleF(knobX, track.Y + 3f, knobD, knobD);
        Color knobColor = Theme.Blend(Theme.ForeTertiary, Theme.AccentFore, _t);
        using (var brush = new SolidBrush(knobColor))
            g.FillEllipse(brush, knob);

        if (Focused && ShowFocusCues)
        {
            var ring = RectangleF.Inflate(track, 2f, 2f);
            Theme.DrawRounded(g, ring, ring.Height / 2f, Theme.Accent, 1.4f);
        }

        if (!string.IsNullOrEmpty(Text))
        {
            var textColor = Enabled ? ForeColor : Theme.DisabledFore;
            var rect = new Rectangle(TrackWidth + LabelGap, 0,
                Math.Max(0, Width - TrackWidth - LabelGap), Height);
            TextRenderer.DrawText(g, Text, Theme.UiFont, rect, textColor,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter |
                TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);
        }
    }

    protected override void OnClick(EventArgs e)
    {
        Checked = !Checked;
        base.OnClick(e);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode is Keys.Space or Keys.Enter)
        {
            Checked = !Checked;
            e.Handled = true;
        }
        base.OnKeyDown(e);
    }

    protected override void OnMouseEnter(EventArgs eventargs)
    {
        _hover = true; Invalidate(); base.OnMouseEnter(eventargs);
    }

    protected override void OnMouseLeave(EventArgs eventargs)
    {
        _hover = false; Invalidate(); base.OnMouseLeave(eventargs);
    }

    protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
    protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }
    protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _anim.Dispose();
        base.Dispose(disposing);
    }
}
