using System.Drawing.Drawing2D;

namespace WallpaperRotator;

public enum FluentGlyph
{
    None,
    Refresh,
    Star,
    StarFilled,
    Block,
    Check,
    Photo,
    Cache,
    History,
    Logs,
    Moon,
    Sun,
    ChevronDown,
    Folder,
    Trash,
    Open,
    Search
}

/// <summary>Crisp, theme-colored vector glyphs (no icon-font dependency).</summary>
public static class FluentGlyphPainter
{
    public static void Draw(Graphics g, FluentGlyph glyph, RectangleF box, Color color, Color backdrop, float stroke = 1.6f)
    {
        if (glyph == FluentGlyph.None) return;

        var state = g.Save();
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;

        float s = Math.Min(box.Width, box.Height);
        float cx = box.X + box.Width / 2f;
        float cy = box.Y + box.Height / 2f;

        using var pen = new Pen(color, stroke)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round,
            LineJoin = LineJoin.Round
        };
        using var brush = new SolidBrush(color);

        switch (glyph)
        {
            case FluentGlyph.Refresh:
            {
                float r = s * 0.30f;
                var rect = new RectangleF(cx - r, cy - r, r * 2, r * 2);
                g.DrawArc(pen, rect, -55, 285);
                var tip = Polar(cx, cy, r, -55 + 285);
                ArrowHead(g, brush, tip, -55 + 285 + 90, s * 0.20f);
                break;
            }

            case FluentGlyph.Star:
            case FluentGlyph.StarFilled:
            {
                using var path = StarPath(cx, cy, s * 0.46f, s * 0.20f);
                if (glyph == FluentGlyph.StarFilled)
                    g.FillPath(brush, path);
                else
                    g.DrawPath(pen, path);
                break;
            }

            case FluentGlyph.Block:
            {
                float r = s * 0.36f;
                g.DrawEllipse(pen, cx - r, cy - r, r * 2, r * 2);
                float o = r * 0.70f;
                g.DrawLine(pen, cx - o, cy - o, cx + o, cy + o);
                break;
            }

            case FluentGlyph.Check:
            {
                float u = s * 0.30f;
                g.DrawLines(pen, new[]
                {
                    new PointF(cx - u, cy + s * 0.02f),
                    new PointF(cx - s * 0.06f, cy + u * 0.85f),
                    new PointF(cx + u, cy - u * 0.75f)
                });
                break;
            }

            case FluentGlyph.Photo:
            {
                var r = new RectangleF(cx - s * 0.38f, cy - s * 0.32f, s * 0.76f, s * 0.64f);
                using var path = Theme.RoundedRect(r, s * 0.12f);
                g.DrawPath(pen, path);
                g.FillEllipse(brush, r.X + r.Width * 0.18f, r.Y + r.Height * 0.18f, s * 0.13f, s * 0.13f);
                g.DrawLines(pen, new[]
                {
                    new PointF(r.X + r.Width * 0.08f, r.Bottom - r.Height * 0.14f),
                    new PointF(r.X + r.Width * 0.42f, r.Y + r.Height * 0.42f),
                    new PointF(r.X + r.Width * 0.66f, r.Bottom - r.Height * 0.30f),
                    new PointF(r.X + r.Width * 0.90f, r.Bottom - r.Height * 0.14f)
                });
                break;
            }

            case FluentGlyph.Cache:
            {
                float w = s * 0.66f, h = s * 0.20f;
                for (int i = 0; i < 3; i++)
                {
                    var r = new RectangleF(cx - w / 2, cy - s * 0.26f + i * s * 0.19f, w, h);
                    using var path = Theme.RoundedRect(r, s * 0.07f);
                    g.DrawPath(pen, path);
                }
                break;
            }

            case FluentGlyph.History:
            {
                float r = s * 0.36f;
                g.DrawEllipse(pen, cx - r, cy - r, r * 2, r * 2);
                g.DrawLine(pen, cx, cy - r * 0.55f, cx, cy);
                g.DrawLine(pen, cx, cy, cx + r * 0.52f, cy + r * 0.28f);
                break;
            }

            case FluentGlyph.Logs:
            {
                float w = s * 0.60f;
                for (int i = 0; i < 3; i++)
                {
                    float y = cy - s * 0.22f + i * s * 0.22f;
                    g.FillEllipse(brush, cx - w / 2 - s * 0.10f, y - s * 0.045f, s * 0.09f, s * 0.09f);
                    g.DrawLine(pen, cx - w / 2 + s * 0.06f, y, cx + w / 2, y);
                }
                break;
            }

            case FluentGlyph.Moon:
            {
                float r = s * 0.38f;
                using var moon = new GraphicsPath();
                moon.AddArc(cx - r, cy - r, r * 2, r * 2, 55, 250);
                moon.AddBezier(
                    new PointF(cx + r * 0.10f, cy - r * 1.00f),
                    new PointF(cx - r * 0.55f, cy - r * 0.35f),
                    new PointF(cx - r * 0.55f, cy + r * 0.35f),
                    new PointF(cx + r * 0.10f, cy + r * 1.00f));
                moon.CloseFigure();
                g.FillPath(brush, moon);
                break;
            }

            case FluentGlyph.Sun:
            {
                float r = s * 0.22f;
                g.DrawEllipse(pen, cx - r, cy - r, r * 2, r * 2);
                for (int i = 0; i < 8; i++)
                {
                    var a = i * Math.PI / 4;
                    var p1 = new PointF(cx + (float)Math.Cos(a) * r * 1.65f, cy + (float)Math.Sin(a) * r * 1.65f);
                    var p2 = new PointF(cx + (float)Math.Cos(a) * r * 2.15f, cy + (float)Math.Sin(a) * r * 2.15f);
                    g.DrawLine(pen, p1, p2);
                }
                break;
            }

            case FluentGlyph.ChevronDown:
            {
                float w = s * 0.32f;
                g.DrawLines(pen, new[]
                {
                    new PointF(cx - w, cy - w * 0.45f),
                    new PointF(cx, cy + w * 0.45f),
                    new PointF(cx + w, cy - w * 0.45f)
                });
                break;
            }

            case FluentGlyph.Folder:
            {
                var r = new RectangleF(cx - s * 0.36f, cy - s * 0.26f, s * 0.72f, s * 0.54f);
                using var path = Theme.RoundedRect(r, s * 0.10f);
                g.DrawPath(pen, path);
                g.DrawLine(pen, r.X + s * 0.02f, r.Y + s * 0.10f, r.X + s * 0.30f, r.Y + s * 0.10f);
                break;
            }

            case FluentGlyph.Trash:
            {
                float w = s * 0.50f;
                g.DrawLine(pen, cx - w / 2 - s * 0.06f, cy - s * 0.28f, cx + w / 2 + s * 0.06f, cy - s * 0.28f);
                g.DrawLine(pen, cx - s * 0.08f, cy - s * 0.36f, cx + s * 0.08f, cy - s * 0.36f);
                using var body = Theme.RoundedRect(new RectangleF(cx - w / 2, cy - s * 0.20f, w, s * 0.52f), s * 0.07f);
                g.DrawPath(pen, body);
                g.DrawLine(pen, cx, cy - s * 0.10f, cx, cy + s * 0.20f);
                break;
            }

            case FluentGlyph.Open:
            {
                var r = new RectangleF(cx - s * 0.34f, cy - s * 0.30f, s * 0.50f, s * 0.50f);
                using var path = Theme.RoundedRect(r, s * 0.08f);
                g.DrawPath(pen, path);
                g.DrawLine(pen, cx + s * 0.02f, cy + s * 0.02f, cx + s * 0.34f, cy - s * 0.30f);
                g.DrawLine(pen, cx + s * 0.14f, cy - s * 0.30f, cx + s * 0.34f, cy - s * 0.30f);
                g.DrawLine(pen, cx + s * 0.34f, cy - s * 0.30f, cx + s * 0.34f, cy - s * 0.10f);
                break;
            }

            case FluentGlyph.Search:
            {
                float r = s * 0.26f;
                g.DrawEllipse(pen, cx - r - s * 0.08f, cy - r - s * 0.08f, r * 2, r * 2);
                g.DrawLine(pen, cx + r * 0.55f, cy + r * 0.55f, cx + s * 0.34f, cy + s * 0.34f);
                break;
            }
        }

        g.Restore(state);
    }

    private static PointF Polar(float cx, float cy, float r, double degrees)
    {
        double a = degrees * Math.PI / 180.0;
        return new PointF(cx + (float)Math.Cos(a) * r, cy + (float)Math.Sin(a) * r);
    }

    private static void ArrowHead(Graphics g, Brush brush, PointF tip, double degrees, float size)
    {
        var state = g.Save();
        g.TranslateTransform(tip.X, tip.Y);
        g.RotateTransform((float)degrees);
        using var path = new GraphicsPath();
        path.AddPolygon(new[]
        {
            new PointF(0, 0),
            new PointF(-size, -size * 0.62f),
            new PointF(-size, size * 0.62f)
        });
        g.FillPath(brush, path);
        g.Restore(state);
    }

    private static GraphicsPath StarPath(float cx, float cy, float outer, float inner)
    {
        var path = new GraphicsPath();
        var points = new PointF[10];
        for (int i = 0; i < 10; i++)
        {
            double angle = -Math.PI / 2 + i * Math.PI / 5;
            float radius = i % 2 == 0 ? outer : inner;
            points[i] = new PointF(cx + (float)Math.Cos(angle) * radius, cy + (float)Math.Sin(angle) * radius);
        }
        path.AddPolygon(points);
        return path;
    }
}
