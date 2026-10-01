using System.Drawing;
using System.Windows.Forms;

namespace WallpaperRotator;

/// <summary>
/// TabControl that covers the native light-gray tab strip header and page border
/// with the current theme background.
/// </summary>
public class DarkTabControl : TabControl
{
    private const int WM_PAINT = 0x000F;

    public DarkTabControl()
    {
        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);
    }

    protected override void WndProc(ref Message m)
    {
        base.WndProc(ref m);

        if (m.Msg == WM_PAINT && IsHandleCreated)
        {
            using var g = Graphics.FromHwnd(Handle);
            using var brush = new SolidBrush(Theme.Back);

            int headerHeight = DisplayRectangle.Top;

            // 1. Fill the empty header strip to the right of the last tab
            int lastRight = TabCount > 0 ? GetTabRect(TabCount - 1).Right : 0;
            if (Width > lastRight)
            {
                g.FillRectangle(brush, lastRight, 0, Width - lastRight, headerHeight);
            }

            // 2. Fill any margin to the left of the first tab
            if (TabCount > 0)
            {
                int firstLeft = GetTabRect(0).Left;
                if (firstLeft > 0)
                {
                    g.FillRectangle(brush, 0, 0, firstLeft, headerHeight);
                }
            }

            // 3. Cover the 2px light-gray border surrounding the TabPage display area
            var rect = DisplayRectangle;
            rect.Inflate(2, 2);
            using var pen = new Pen(Theme.Back, 3);
            g.DrawRectangle(pen, rect);

            // 4. Cover the outermost 1px edge
            using var outerPen = new Pen(Theme.Back, 2);
            g.DrawRectangle(outerPen, 0, 0, Width - 1, Height - 1);
        }
    }
}