using System.Drawing.Drawing2D;

namespace WallpaperRotator;

public enum FluentButtonVariant { Standard, Accent, Subtle, Danger }

/// <summary>Rounded, self-painted button matching the Windows 11 Fluent style.</summary>
public class FluentButton : Button
{
    public FluentButtonVariant Variant { get; set; } = FluentButtonVariant.Standard;
    public FluentGlyph Icon { get; set; } = FluentGlyph.None;
    public int IconSize { get; set; } = 16;
    public float CornerRadius { get; set; } = 5f;
    public bool Bold { get; set; }

    private bool _hover;
    private bool _pressed;

    protected virtual FluentGlyph GlyphToDraw => Icon;

    public FluentButton()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        UseVisualStyleBackColor = false;
        Font = Theme.ButtonFont;
        Height = 32;
        TabStop = true;
    }

    public void ApplyTheme()
    {
        Font = Bold ? new Font(Theme.ButtonFont, FontStyle.Bold) : Theme.ButtonFont;
        switch (Variant)
        {
            case FluentButtonVariant.Accent:
                BackColor = Theme.Accent;
                ForeColor = Theme.AccentFore;
                break;
            case FluentButtonVariant.Subtle:
                BackColor = Theme.Back;
                ForeColor = Theme.Fore;
                break;
            case FluentButtonVariant.Danger:
                BackColor = Theme.ControlBack;
                ForeColor = Theme.Danger;
                break;
            default:
                BackColor = Theme.ControlBack;
                ForeColor = Theme.Fore;
                break;
        }
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        // Clear the full client area first: the native Button control can leave a
        // 1px frame around owner-drawn faces, which looks like a square outline.
        using (var clear = new SolidBrush(Parent?.BackColor ?? Theme.Back))
            g.FillRectangle(clear, ClientRectangle);

        Color fill, border, text;
        if (!Enabled)
        {
            fill = Theme.Card;
            border = Theme.ControlBorder;
            text = Theme.DisabledFore;
        }
        else if (Variant == FluentButtonVariant.Accent)
        {
            fill = _pressed ? Theme.AccentPressed : _hover ? Theme.AccentHover : Theme.Accent;
            border = fill;
            text = Theme.AccentFore;
        }
        else if (Variant == FluentButtonVariant.Subtle)
        {
            fill = _pressed ? Theme.Overlay(Theme.SubtlePressed, Theme.Back)
                 : _hover ? Theme.Overlay(Theme.SubtleHover, Theme.Back)
                 : Theme.Back;
            border = fill;
            text = Variant == FluentButtonVariant.Danger ? Theme.Danger : ForeColor;
        }
        else
        {
            var baseFill = BackColor;
            fill = _pressed ? Theme.Blend(baseFill, Color.Black, 0.14f)
                 : _hover ? Theme.Blend(baseFill, Theme.Dark ? Color.White : Color.Black, 0.07f)
                 : baseFill;
            border = _hover ? Theme.ControlBorderHover : Theme.ControlBorder;
            text = Variant == FluentButtonVariant.Danger ? Theme.Danger : ForeColor;
        }

        var rect = new RectangleF(0.5f, 0.5f, Width - 1f, Height - 1f);
        Theme.FillAndStroke(g, rect, CornerRadius, fill, border);

        if (Focused && ShowFocusCues && Enabled)
        {
            var inner = new RectangleF(2.5f, 2.5f, Width - 5f, Height - 5f);
            Theme.DrawRounded(g, inner, Math.Max(1, CornerRadius - 2), Theme.Accent, 1.4f);
        }

        var glyph = GlyphToDraw;
        bool hasText = !string.IsNullOrEmpty(Text);
        bool hasIcon = glyph != FluentGlyph.None;
        int textW = hasText
            ? (int)Math.Ceiling((double)TextRenderer.MeasureText(Text, Font, Size.Empty,
                TextFormatFlags.NoPadding | TextFormatFlags.SingleLine).Width)
            : 0;

        int gap = hasText && hasIcon ? 8 : 0;
        int iconW = hasIcon ? IconSize : 0;
        int total = iconW + gap + textW;
        int startX = Math.Max(0, (Width - total) / 2);
        int centerY = Height / 2;

        if (hasIcon)
        {
            var box = new RectangleF(startX, centerY - IconSize / 2f, IconSize, IconSize);
            FluentGlyphPainter.Draw(g, glyph, box, text, fill, 1.55f);
        }

        if (hasText)
        {
            var textRect = new Rectangle(startX + iconW + gap, 0,
                Math.Max(0, Width - (startX + iconW + gap)), Height);
            TextRenderer.DrawText(g, Text, Font, textRect, text,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter |
                TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis |
                TextFormatFlags.NoPadding);
        }
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        _hover = true; Invalidate(); base.OnMouseEnter(e);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        _hover = false; _pressed = false; Invalidate(); base.OnMouseLeave(e);
    }

    protected override void OnMouseDown(MouseEventArgs mevent)
    {
        if (mevent.Button == MouseButtons.Left) { _pressed = true; Invalidate(); }
        base.OnMouseDown(mevent);
    }

    protected override void OnMouseUp(MouseEventArgs mevent)
    {
        _pressed = false; Invalidate(); base.OnMouseUp(mevent);
    }

    protected override void OnEnabledChanged(EventArgs e)
    {
        Invalidate(); base.OnEnabledChanged(e);
    }

    protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
    protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }
}
