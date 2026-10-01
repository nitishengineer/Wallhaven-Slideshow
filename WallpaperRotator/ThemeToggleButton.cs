namespace WallpaperRotator;

/// <summary>Borderless, non-focusable icon button that paints itself to blend into the tab strip.</summary>
public class ThemeToggleButton : Button
{
    private bool _hover;

    public ThemeToggleButton()
    {
        SetStyle(ControlStyles.Selectable, false);   // never focused -> never a focus ring
        TabStop = false;
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
    }

    protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        // Background: blend with strip, subtle hover highlight
        using (var bg = new SolidBrush(_hover ? Theme.ControlBack : Theme.Back))
            e.Graphics.FillRectangle(bg, ClientRectangle);

        // Icon: drawn by us, so font fallbacks can't surprise us
        var glyph = Theme.Dark ? "☀" : "🌙";
        TextRenderer.DrawText(e.Graphics, glyph, Font, e.ClipRectangle, Theme.Fore,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
    }
}