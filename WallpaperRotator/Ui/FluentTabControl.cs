using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace WallpaperRotator;

/// <summary>
/// Borderless tab strip drawn as Fluent pills with vector icons.
/// The native control is never allowed to paint: its 3-D page border shows a
/// light hairline in dark mode and its hot-tracking repaint makes the strip
/// flash on hover.  Every pixel (including the strip background and the area
/// around the pages) is drawn straight onto the window DC, and hover never
/// triggers a repaint, so the strip stays pixel-static while the pointer moves.
/// </summary>
public class FluentTabControl : TabControl
{
    private const int TabWidth = 112;
    private const int TabHeight = 40;

    private const int WM_PAINT = 0x000F;
    private const int GWL_STYLE = -16;
    private const int WS_CLIPCHILDREN = 0x02000000;

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PAINTSTRUCT
    {
        public IntPtr hdc;
        public bool fErase;
        public RECT rcPaint;
        public bool fRestore;
        public bool fIncUpdate;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
        public byte[] rgbReserved;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr BeginPaint(IntPtr hwnd, out PAINTSTRUCT ps);

    [DllImport("user32.dll")]
    private static extern bool EndPaint(IntPtr hwnd, ref PAINTSTRUCT ps);

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hwnd, int index);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hwnd, int index, int value);

    public FluentTabControl()
    {
        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);
        DrawMode = TabDrawMode.OwnerDrawFixed;
        SizeMode = TabSizeMode.Fixed;
        ItemSize = new Size(TabWidth, TabHeight);
        SetStyle(ControlStyles.ResizeRedraw, true);
    }

    public static FluentGlyph GlyphFor(string pageText) => pageText switch
    {
        "Rotate" => FluentGlyph.Refresh,
        "Cache" => FluentGlyph.Cache,
        "Favorite" => FluentGlyph.Star,
        "History" => FluentGlyph.History,
        "Blocked" => FluentGlyph.Block,
        "Logs" => FluentGlyph.Logs,
        _ => FluentGlyph.None
    };

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);

        // Keep strip paints off the page windows so pages are never overdrawn.
        int style = GetWindowLong(Handle, GWL_STYLE);
        SetWindowLong(Handle, GWL_STYLE, style | WS_CLIPCHILDREN);
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WM_PAINT && IsHandleCreated && TabCount > 0)
        {
            IntPtr hdc = BeginPaint(Handle, out PAINTSTRUCT ps);
            if (hdc != IntPtr.Zero)
            {
                var invalid = Rectangle.FromLTRB(ps.rcPaint.Left, ps.rcPaint.Top, ps.rcPaint.Right, ps.rcPaint.Bottom);

                // Draw straight onto the window DC: GDI text on a 32bpp memory
                // bitmap is not ClearType-smoothed for dark-on-light labels, which
                // made the light theme look blocky.  The native control never
                // paints, so there is no underlying chrome that could flash.
                using (var g = Graphics.FromHdc(hdc))
                {
                    g.SetClip(invalid);
                    PaintStrip(g);
                }

                EndPaint(Handle, ref ps);
                m.Result = IntPtr.Zero;
                return;
            }
        }

        base.WndProc(ref m);
    }

    private void PaintStrip(Graphics g)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        int header = DisplayRectangle.Top;

        // Wipe every native pixel (header band, 3-D page border, outer chrome).
        using (var bg = new SolidBrush(Theme.Back))
            g.FillRectangle(bg, 0, 0, Width, Height);

        for (int i = 0; i < TabCount; i++)
            DrawTab(g, i, GetTabRect(i));

        using (var line = new Pen(Theme.ControlBorder))
            g.DrawLine(line, 0, header - 1, Width, header - 1);
    }

    private void DrawTab(Graphics g, int index, Rectangle bounds)
    {
        if (index < 0 || index >= TabCount) return;

        using (var bg = new SolidBrush(Theme.Back))
            g.FillRectangle(bg, bounds);

        bool selected = index == SelectedIndex;

        var pill = new RectangleF(bounds.X + 4, bounds.Y + 5, bounds.Width - 8, bounds.Height - 10);
        if (selected)
            Theme.FillAndStroke(g, pill, 7f, Theme.Card, Theme.ControlBorder);

        if (selected)
        {
            var underline = new RectangleF(pill.X + 14, pill.Bottom - 2.5f, pill.Width - 28, 2.5f);
            Theme.FillRounded(g, underline, 1.2f, Theme.Accent);
        }

        string label = TabPages[index].Text;
        var glyph = GlyphFor(label);
        Color textColor = selected ? Theme.Fore : Theme.ForeSecondary;
        const int iconSize = 16;
        const int gap = 8;

        int textWidth = (int)Math.Ceiling((double)TextRenderer.MeasureText(label, Theme.TabFont, Size.Empty,
            TextFormatFlags.NoPadding | TextFormatFlags.SingleLine).Width);
        int total = iconSize + (glyph == FluentGlyph.None ? 0 : gap) + textWidth;
        int startX = bounds.X + Math.Max(0, (bounds.Width - total) / 2);
        int centerY = bounds.Y + bounds.Height / 2;

        if (glyph != FluentGlyph.None)
        {
            var box = new RectangleF(startX, centerY - iconSize / 2f, iconSize, iconSize);
            FluentGlyphPainter.Draw(g, glyph, box, selected ? Theme.Accent : textColor, Theme.Back, 1.5f);
        }

        var textRect = new Rectangle(startX + iconSize + (glyph == FluentGlyph.None ? 0 : gap), 0,
            textWidth + 2, bounds.Height);
        TextRenderer.DrawText(g, label, Theme.TabFont, textRect, textColor,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);
    }
}
