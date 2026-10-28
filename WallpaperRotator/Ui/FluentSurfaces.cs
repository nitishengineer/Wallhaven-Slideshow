using System.Drawing.Drawing2D;

namespace WallpaperRotator;

/// <summary>Picture box that clips its image to a rounded card with a hairline border.</summary>
public class RoundedPictureBox : PictureBox
{
    public int CornerRadius { get; set; } = 8;

    public RoundedPictureBox()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
    }

    protected override void OnPaint(PaintEventArgs pe)
    {
        var g = pe.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;

        var rect = new RectangleF(0.5f, 0.5f, Width - 1f, Height - 1f);
        using var path = Theme.RoundedRect(rect, CornerRadius);
        using var back = new SolidBrush(Theme.PreviewBack);
        g.FillPath(back, path);

        if (Image is not null)
        {
            var state = g.Save();
            g.SetClip(path);
            float scale = Math.Min((float)Width / Image.Width, (float)Height / Image.Height);
            float dw = Image.Width * scale;
            float dh = Image.Height * scale;
            g.DrawImage(Image, (Width - dw) / 2f, (Height - dh) / 2f, dw, dh);
            g.Restore(state);
        }

        Theme.DrawRounded(g, rect, CornerRadius, Theme.ControlBorder);
    }
}

/// <summary>Rounded surface used by list rows; highlights on hover.</summary>
public class CardPanel : Panel
{
    public int CornerRadius { get; set; } = 8;
    public bool HoverHighlight { get; set; } = true;

    private bool _hover;

    public CardPanel()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        MouseEnter += (s, e) => { _hover = true; Invalidate(); };
        MouseLeave += (s, e) => { _hover = false; Invalidate(); };
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        using var brush = new SolidBrush(Parent?.BackColor ?? Theme.Back);
        e.Graphics.FillRectangle(brush, ClientRectangle);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var rect = new RectangleF(0.5f, 0.5f, Width - 1f, Height - 1f);
        Color fill = _hover && HoverHighlight ? Theme.Blend(Theme.Card, Theme.Dark ? Color.White : Color.Black, 0.05f) : Theme.Card;
        Theme.FillAndStroke(g, rect, CornerRadius, fill, Theme.ControlBorder);
    }
}
