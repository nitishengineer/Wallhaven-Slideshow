namespace WallpaperRotator;

/// <summary>App-wide, theme-aware tooltips. One shared component, owner-drawn to match dark/light mode.</summary>
public static class Tips
{
    private static readonly ToolTip Tip = new()
    {
        AutoPopDelay = 20000,   // leave plenty of reading time
        InitialDelay = 250,
        ReshowDelay = 150,
        ShowAlways = true,
        OwnerDraw = true
    };

    static Tips()
    {
        // Size the bubble to fit the wrapped text (PopupEventArgs has no ToolTipText —
        // pull it from the component via the associated control)
        Tip.Popup += (s, e) =>
        {
            var text = e.AssociatedControl is null ? "" : Tip.GetToolTip(e.AssociatedControl);
            var size = TextRenderer.MeasureText(text, SystemFonts.MessageBoxFont,
                new Size(360, int.MaxValue), TextFormatFlags.WordBreak);
            e.ToolTipSize = new Size(Math.Min(size.Width + 16, 376), size.Height + 12);
        };

        Tip.Draw += (s, e) =>
        {
            using var bg = new SolidBrush(Theme.ControlBack);
            using var border = new Pen(Theme.ControlBorder);
            e.Graphics.FillRectangle(bg, e.Bounds);
            e.Graphics.DrawRectangle(border, new Rectangle(e.Bounds.X, e.Bounds.Y, e.Bounds.Width - 1, e.Bounds.Height - 1));
            TextRenderer.DrawText(e.Graphics, e.ToolTipText, SystemFonts.MessageBoxFont,
                new Rectangle(e.Bounds.X + 8, e.Bounds.Y + 6, e.Bounds.Width - 16, e.Bounds.Height - 12),
                Theme.Fore, TextFormatFlags.WordBreak);
        };
    }

    public static void For(Control c, string text) => Tip.SetToolTip(c, text);
}