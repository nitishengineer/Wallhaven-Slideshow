using System.Drawing.Drawing2D;

namespace WallpaperRotator;

/// <summary>Subtle, borderless icon button that toggles the dark/light theme.</summary>
public class ThemeToggleButton : Control
{
    private bool _hover;
    private bool _pressed;

    public ThemeToggleButton()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                 ControlStyles.SupportsTransparentBackColor | ControlStyles.UserMouse, true);
        SetStyle(ControlStyles.Selectable, false);
        TabStop = false;
        Size = new Size(32, 30);
        BackColor = Theme.Back;
        ForeColor = Theme.Fore;
        AccessibleRole = AccessibleRole.PushButton;
        AccessibleName = "Toggle dark or light theme";
    }

    public void ApplyTheme()
    {
        BackColor = Theme.Back;
        ForeColor = Theme.Fore;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;

        using (var clear = new SolidBrush(Theme.Back))
            g.FillRectangle(clear, ClientRectangle);

        var fill = _pressed ? Theme.Overlay(Theme.SubtlePressed, Theme.Back)
                 : _hover ? Theme.Overlay(Theme.SubtleHover, Theme.Back)
                 : Theme.Back;

        var rect = new RectangleF(0.5f, 0.5f, Width - 1f, Height - 1f);
        Theme.FillAndStroke(g, rect, 6f, fill, fill);

        var glyph = Theme.Dark ? FluentGlyph.Sun : FluentGlyph.Moon;
        float size = 18f;
        var box = new RectangleF((Width - size) / 2f, (Height - size) / 2f, size, size);
        FluentGlyphPainter.Draw(g, glyph, box, ForeColor, fill, 1.6f);
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        _hover = true;
        Invalidate();
        base.OnMouseEnter(e);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        _hover = false;
        _pressed = false;
        Invalidate();
        base.OnMouseLeave(e);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            _pressed = true;
            Invalidate();
        }
        base.OnMouseDown(e);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left && _pressed)
        {
            _pressed = false;
            Invalidate();
        }
        base.OnMouseUp(e);
    }
}
