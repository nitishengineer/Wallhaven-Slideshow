using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace WallpaperRotator;

/// <summary>Fluent (Windows 11) palette, typography, window chrome and control theming.</summary>
public static class Theme
{
    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
    private const int DWMWA_CLOAK = 13;
    private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
    private const int DWMWA_BORDER_COLOR = 34;
    private const int DWMWA_SYSTEMBACKDROP_TYPE = 38;

    private const int DWMWCP_ROUND = 2;
    private const int DWMSBT_MAINWINDOW = 2;   // Mica

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    [DllImport("uxtheme.dll", ExactSpelling = true, CharSet = CharSet.Unicode)]
    private static extern int SetWindowTheme(IntPtr hWnd, string? pszSubAppName, string? pszSubIdList);

    [StructLayout(LayoutKind.Sequential)]
    private struct WindowCompositionAttributeData
    {
        public int Attribute;
        public IntPtr Data;
        public int SizeOfData;
    }

    [DllImport("user32.dll")]
    private static extern int SetWindowCompositionAttribute(IntPtr hwnd, ref WindowCompositionAttributeData data);

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool RedrawWindow(IntPtr hWnd, IntPtr lprcUpdate, IntPtr hrgnUpdate, uint flags);

    private const int WCA_USEDARKMODECOLORS = 26;

    private const int WM_SETREDRAW = 0x000B;
    private const uint RDW_INVALIDATE = 0x0001;
    private const uint RDW_ERASE = 0x0004;
    private const uint RDW_ALLCHILDREN = 0x0080;
    private const uint RDW_UPDATENOW = 0x0100;
    private const uint RDW_FRAME = 0x0400;

    // ---------- state ----------
    public static bool Dark { get; private set; }

    private static Color _accent = Color.FromArgb(0x00, 0x78, 0xD4);

    public static void Init(bool dark)
    {
        Dark = dark;
        LoadSystemAccent();
    }

    public static void Toggle() => Dark = !Dark;

    private static void LoadSystemAccent()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\DWM");
            if (key?.GetValue("ColorizationColor") is int raw)
            {
                var c = Color.FromArgb(raw);          // 0xAARRGGBB
                if (c.R + c.G + c.B > 40)
                    _accent = Color.FromArgb(255, c.R, c.G, c.B);
            }
        }
        catch { /* fall back to the Windows default blue */ }
    }

    // ---------- palette ----------
    public static Color Back => Dark ? Color.FromArgb(32, 32, 32) : Color.FromArgb(243, 243, 243);
    public static Color Card => Dark ? Color.FromArgb(43, 43, 43) : Color.FromArgb(251, 251, 251);
    public static Color Fore => Dark ? Color.FromArgb(255, 255, 255) : Color.FromArgb(26, 26, 26);
    public static Color ForeSecondary => Dark ? Color.FromArgb(200, 200, 200) : Color.FromArgb(93, 93, 93);
    public static Color ForeTertiary => Dark ? Color.FromArgb(150, 150, 150) : Color.FromArgb(138, 138, 138);
    public static Color DisabledFore => ForeTertiary;

    public static Color ControlBack => Dark ? Color.FromArgb(45, 45, 45) : Color.FromArgb(255, 255, 255);
    public static Color ControlBorder => Dark ? Color.FromArgb(64, 64, 64) : Color.FromArgb(214, 214, 214);
    public static Color ControlBorderHover => Dark ? Color.FromArgb(92, 92, 92) : Color.FromArgb(170, 170, 170);

    public static Color SubtleHover => Dark ? Color.FromArgb(14, 255, 255, 255) : Color.FromArgb(10, 0, 0, 0);
    public static Color SubtlePressed => Dark ? Color.FromArgb(22, 255, 255, 255) : Color.FromArgb(16, 0, 0, 0);

    public static Color PreviewBack => Dark ? Color.FromArgb(25, 25, 28) : Color.FromArgb(232, 232, 232);
    public static Color SelectedBack => Dark ? Color.FromArgb(58, 58, 58) : Color.FromArgb(225, 235, 245);

    public static Color Danger => Dark ? Color.FromArgb(255, 120, 100) : Color.FromArgb(196, 43, 28);

    // Accent is brightened in dark mode so it stays readable on the dark surface.
    public static Color Accent => Dark ? Blend(_accent, Color.White, 0.34f) : _accent;
    public static Color AccentHover => Blend(Accent, Dark ? Color.White : Color.Black, 0.10f);
    public static Color AccentPressed => Blend(Accent, Color.Black, 0.14f);
    public static Color AccentFore => Luminance(Accent) > 0.55f ? Color.FromArgb(16, 16, 16) : Color.White;
    public static Color AccentSoft => Blend(Accent, Back, Dark ? 0.82f : 0.88f);

    /// <summary>Linear blend between two colors; t = 0 returns a, t = 1 returns b.</summary>
    public static Color Blend(Color a, Color b, float t)
    {
        t = Math.Clamp(t, 0f, 1f);
        return Color.FromArgb(
            (int)(a.A + (b.A - a.A) * t),
            (int)(a.R + (b.R - a.R) * t),
            (int)(a.G + (b.G - a.G) * t),
            (int)(a.B + (b.B - a.B) * t));
    }

    private static float Luminance(Color c) => (0.299f * c.R + 0.587f * c.G + 0.114f * c.B) / 255f;

    /// <summary>Overlays <paramref name="overlay"/> (alpha allowed) on top of <paramref name="baseColor"/>.</summary>
    public static Color Overlay(Color overlay, Color baseColor) =>
        Blend(baseColor, Color.FromArgb(255, overlay.R, overlay.G, overlay.B), overlay.A / 255f);

    // ---------- typography ----------
    private static readonly string TextFamily = ResolveFamily("Segoe UI Variable Text", "Segoe UI");
    private static readonly string DisplayFamily = ResolveFamily("Segoe UI Variable Display", "Segoe UI");

    private static readonly Font _uiFont = new(TextFamily, 9.5F);
    private static readonly Font _buttonFont = new(TextFamily, 9.5F);
    private static readonly Font _strongFont = new(TextFamily, 9.5F, FontStyle.Bold);
    private static readonly Font _captionFont = new(TextFamily, 8.5F);
    private static readonly Font _titleFont = new(DisplayFamily, 15F, FontStyle.Bold);
    private static readonly Font _tabFont = new(TextFamily, 9.75F);

    public static Font UiFont => _uiFont;
    public static Font ButtonFont => _buttonFont;
    public static Font StrongFont => _strongFont;
    public static Font CaptionFont => _captionFont;
    public static Font TitleFont => _titleFont;
    public static Font TabFont => _tabFont;

    private static string ResolveFamily(params string[] names)
    {
        try
        {
            var installed = FontFamily.Families.Select(f => f.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
            return names.FirstOrDefault(installed.Contains) ?? "Segoe UI";
        }
        catch { return "Segoe UI"; }
    }

    // ---------- geometry helpers ----------
    public static GraphicsPath RoundedRect(RectangleF r, float radius)
    {
        var path = new GraphicsPath();
        float d = Math.Max(0, Math.Min(radius * 2, Math.Min(r.Width, r.Height)));
        if (d <= 0) { path.AddRectangle(r); path.CloseFigure(); return path; }
        path.AddArc(r.X, r.Y, d, d, 180, 90);
        path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    public static void FillRounded(Graphics g, RectangleF r, float radius, Color fill)
    {
        var old = g.SmoothingMode;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using var path = RoundedRect(r, radius);
        using var brush = new SolidBrush(fill);
        g.FillPath(brush, path);
        g.SmoothingMode = old;
    }

    public static void DrawRounded(Graphics g, RectangleF r, float radius, Color stroke, float width = 1f)
    {
        var old = g.SmoothingMode;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using var path = RoundedRect(r, radius);
        using var pen = new Pen(stroke, width);
        g.DrawPath(pen, path);
        g.SmoothingMode = old;
    }

    public static void FillAndStroke(Graphics g, RectangleF r, float radius, Color fill, Color stroke, float width = 1f)
    {
        FillRounded(g, r, radius, fill);
        DrawRounded(g, r, radius, stroke, width);
    }

    /// <summary>Soft card: surface + hairline border, used for the preview and status strips.</summary>
    public static void PaintCard(Graphics g, Rectangle r, int radius = 8)
    {
        var rf = new RectangleF(r.X + 0.5f, r.Y + 0.5f, r.Width - 1, r.Height - 1);
        FillAndStroke(g, rf, radius, Card, ControlBorder);
    }

    // ---------- window chrome ----------
    public static void ApplyChrome(Form form)
    {
        IntPtr hwnd;
        try { hwnd = form.Handle; } catch { return; }

        int dark = Dark ? 1 : 0;
        DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref dark, sizeof(int));

        int corners = DWMWCP_ROUND;
        DwmSetWindowAttribute(hwnd, DWMWA_WINDOW_CORNER_PREFERENCE, ref corners, sizeof(int));

        int backdrop = DWMSBT_MAINWINDOW;   // Mica (Win 11 22H2+); ignored on older builds
        DwmSetWindowAttribute(hwnd, DWMWA_SYSTEMBACKDROP_TYPE, ref backdrop, sizeof(int));

        int border = ColorTranslator.ToWin32(ControlBorder);
        DwmSetWindowAttribute(hwnd, DWMWA_BORDER_COLOR, ref border, sizeof(int));

        // Undocumented but stable since Win10 1809: ask the window manager to tint
        // native scrollbars (ListView, TextBox, ...) to match the app colour mode.
        IntPtr mem = Marshal.AllocHGlobal(sizeof(int));
        try
        {
            Marshal.WriteInt32(mem, Dark ? 1 : 0);
            var data = new WindowCompositionAttributeData
            {
                Attribute = WCA_USEDARKMODECOLORS,
                Data = mem,
                SizeOfData = sizeof(int)
            };
            SetWindowCompositionAttribute(hwnd, ref data);
        }
        catch
        {
            // Best effort only.
        }
        finally
        {
            Marshal.FreeHGlobal(mem);
        }
    }

    /// <summary>
    /// Asks Windows to draw this control's native scrollbar using the current
    /// colour mode.  The window may not have a handle yet, so the call is
    /// repeated once it is created.
    /// </summary>
    public static void ThemeNativeScrollbar(Control control)
    {
        void Apply()
        {
            if (control.IsHandleCreated)
                SetWindowTheme(control.Handle, Dark ? "DarkMode_Explorer" : null, null);
        }

        Apply();
        if (!control.IsHandleCreated)
            control.HandleCreated += (s, e) => Apply();
    }

    // ---------- control theming ----------
    public static void Apply(Control root)
    {
        using var redraw = SuspendRedraw(root);

        root.BackColor = Back;
        root.ForeColor = Fore;
        root.Font = UiFont;

        if (root is Form form)
            ApplyChrome(form);

        ApplyRecursive(root);
    }

    /// <summary>
    /// Freezes every window in <paramref name="root"/> while a theme change is
    /// applied, then repaints the whole tree in one go.  Without this, WinForms
    /// paints child controls as their colours change, so the window is briefly
    /// left half themed (dark fields on a light page, or the reverse).
    /// </summary>
    public static IDisposable SuspendRedraw(Control root)
    {
        if (_redrawDepth++ == 0)
        {
            _redrawRoot = root;
            _redrawHandles = new List<IntPtr>();
            CollectHandles(root, _redrawHandles);

            foreach (IntPtr hwnd in _redrawHandles)
                SendMessage(hwnd, WM_SETREDRAW, IntPtr.Zero, IntPtr.Zero);
        }

        return new RedrawScope();
    }

    /// <summary>
    /// Puts a snapshot of the current client area over the form while the theme
    /// is switched.  The page background and transparent labels can otherwise
    /// repaint before the native fields do, briefly showing the new background
    /// behind old controls (or the reverse).
    /// </summary>
    public static IDisposable CoverForThemeChange(Form form)
    {
        Size size = form.ClientSize;
        if (size.Width <= 0 || size.Height <= 0)
            return new EmptyScope();

        Point location = form.PointToScreen(Point.Empty);
        var shot = new Bitmap(size.Width, size.Height);

        try
        {
            using (var g = Graphics.FromImage(shot))
            {
                g.CopyFromScreen(location, Point.Empty, size);
            }

            // The cover has to be a top-level window, not a child panel: a child
            // panel clips the sibling controls underneath it, so they would still
            // be unpainted when the cover is removed.  A separate top-level window
            // lets the real form repaint normally while the old snapshot stays on
            // screen, then reveals the finished theme in one step.
            var cover = new CoverForm
            {
                FormBorderStyle = FormBorderStyle.None,
                ShowInTaskbar = false,
                StartPosition = FormStartPosition.Manual,
                Bounds = new Rectangle(location, size),
                BackColor = Color.Black,
                BackgroundImage = shot,
                BackgroundImageLayout = ImageLayout.None,
                Owner = form,
                TopMost = true
            };

            // Create the window while it is still hidden and cloak it, then paint
            // it in full before revealing it.  Without the cloak the compositor
            // can show the cover before its snapshot has been drawn (a black band
            // flashes along the part of the window that is still unpainted).
            int cloak = 1;
            DwmSetWindowAttribute(cover.Handle, DWMWA_CLOAK, ref cloak, sizeof(int));
            cover.Show();
            cover.Update();
            cloak = 0;
            DwmSetWindowAttribute(cover.Handle, DWMWA_CLOAK, ref cloak, sizeof(int));

            return new CoverScope(form, cover, shot);
        }
        catch
        {
            shot.Dispose();
            return new EmptyScope();
        }
    }

    private sealed class CoverScope : IDisposable
    {
        private readonly Form _form;
        private readonly CoverForm _cover;
        private readonly Bitmap _shot;
        private bool _disposed;

        public CoverScope(Form form, CoverForm cover, Bitmap shot)
        {
            _form = form;
            _cover = cover;
            _shot = shot;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            _cover.Hide();
            _form.Invalidate(true);

            if (_form.IsHandleCreated)
            {
                RedrawWindow(_form.Handle, IntPtr.Zero, IntPtr.Zero,
                    RDW_INVALIDATE | RDW_ERASE | RDW_FRAME | RDW_ALLCHILDREN | RDW_UPDATENOW);
            }

            _cover.Dispose();
            _shot.Dispose();
        }
    }

    private sealed class CoverForm : Form
    {
        public CoverForm()
        {
            // Compose the snapshot off-screen and present it in a single blit so
            // the window can never be composited half painted.
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);
        }

        protected override bool ShowWithoutActivation => true;

        protected override CreateParams CreateParams
        {
            get
            {
                const int WS_EX_NOACTIVATE = 0x08000000;
                const int WS_EX_TOOLWINDOW = 0x00000080;

                CreateParams cp = base.CreateParams;
                cp.ExStyle |= WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW;
                return cp;
            }
        }
    }

    private sealed class EmptyScope : IDisposable
    {
        public void Dispose() { }
    }

    private sealed class RedrawScope : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            if (--_redrawDepth > 0) return;

            if (_redrawHandles is not null)
            {
                foreach (IntPtr hwnd in _redrawHandles)
                    SendMessage(hwnd, WM_SETREDRAW, new IntPtr(1), IntPtr.Zero);
            }

            if (_redrawRoot is { IsHandleCreated: true } root)
            {
                root.Invalidate(true);
                RedrawWindow(root.Handle, IntPtr.Zero, IntPtr.Zero,
                    RDW_INVALIDATE | RDW_ERASE | RDW_FRAME | RDW_ALLCHILDREN | RDW_UPDATENOW);
            }

            _redrawHandles = null;
            _redrawRoot = null;
        }
    }

    private static void CollectHandles(Control parent, List<IntPtr> handles)
    {
        // Hidden windows cannot paint, and sending WM_SETREDRAW(TRUE) to a
        // hidden HWND makes Windows show it while Control.Visible still
        // reports false (so the app can never hide it again).  Skipping
        // invisible subtrees keeps toggled-off sections hidden across theme
        // changes.
        if (!parent.Visible) return;

        if (parent.IsHandleCreated)
            handles.Add(parent.Handle);

        foreach (Control child in parent.Controls)
            CollectHandles(child, handles);
    }

    [ThreadStatic] private static int _redrawDepth;
    [ThreadStatic] private static List<IntPtr>? _redrawHandles;
    [ThreadStatic] private static Control? _redrawRoot;

    private static void ApplyRecursive(Control parent)
    {
        foreach (Control c in parent.Controls)
        {
            switch (c)
            {
                // Composite custom controls theme themselves and stop recursion.
                case FluentField field:
                    field.ApplyTheme();
                    continue;

                case ThemeToggleButton themeToggle:
                    themeToggle.ApplyTheme();
                    break;

                case FluentButton b:
                    b.ApplyTheme();
                    break;

                case FluentToggleSwitch toggle:
                    toggle.ApplyTheme();
                    break;

                case FluentTabControl tabs:
                    tabs.Invalidate();
                    break;

                // Auto-scrolling containers (the History / Blocked card lists)
                // draw a native scrollbar that ignores the app colour mode
                // unless the window itself is themed.
                case ScrollableControl sc when sc.AutoScroll:
                    sc.BackColor = Back;
                    sc.ForeColor = Fore;
                    ThemeNativeScrollbar(sc);
                    break;

                case RoundedPictureBox preview:
                    preview.Invalidate();
                    break;

                case CardPanel card:
                    card.Invalidate();
                    break;

                case PictureBox pb:
                    pb.BackColor = PreviewBack;
                    break;

                case Button btn:
                    btn.FlatStyle = FlatStyle.Flat;
                    btn.FlatAppearance.BorderColor = ControlBorder;
                    if (btn.Tag as string == "accent")
                    {
                        btn.BackColor = Accent;
                        btn.ForeColor = AccentFore;
                        btn.FlatAppearance.BorderColor = Accent;
                    }
                    else
                    {
                        btn.BackColor = ControlBack;
                        btn.ForeColor = Fore;
                    }
                    break;

                case ListView lv:
                    lv.BorderStyle = BorderStyle.None;
                    lv.BackColor = Card;
                    lv.ForeColor = Fore;
                    void ThemeList()
                    {
                        if (lv.IsHandleCreated)
                            SetWindowTheme(lv.Handle, Dark ? "DarkMode_Explorer" : null, null);
                    }
                    ThemeList();
                    if (!lv.IsHandleCreated)
                        lv.HandleCreated += (s, e) => ThemeList();
                    break;

                case ComboBox cb:
                    cb.FlatStyle = FlatStyle.Flat;
                    cb.BackColor = ControlBack;
                    cb.ForeColor = Fore;
                    if (cb.IsHandleCreated)
                        SetWindowTheme(cb.Handle, Dark ? "DarkMode_CFD" : null, null);
                    break;

                case CheckBox chk:
                    chk.BackColor = Back;
                    chk.ForeColor = Fore;
                    if (chk.IsHandleCreated)
                        SetWindowTheme(chk.Handle, Dark ? "DarkMode_Explorer" : null, null);
                    break;

                case NumericUpDown nud:
                    nud.BackColor = ControlBack;
                    nud.ForeColor = Fore;
                    if (nud.IsHandleCreated)
                    {
                        SetWindowTheme(nud.Handle, Dark ? "DarkMode_Explorer" : null, null);
                        foreach (Control child in nud.Controls)
                            if (child.IsHandleCreated)
                                SetWindowTheme(child.Handle, Dark ? "DarkMode_Explorer" : null, null);
                    }
                    break;

                case TextBox tb:
                    tb.BackColor = ControlBack;
                    tb.ForeColor = Fore;
                    void ThemeEdit()
                    {
                        if (tb.IsHandleCreated)
                            SetWindowTheme(tb.Handle, Dark ? "DarkMode_Explorer" : null, null);
                    }
                    ThemeEdit();
                    tb.HandleCreated += (s, e) => ThemeEdit();
                    break;

                case Panel p when p.Tag as string == "logpad":
                    p.BackColor = ControlBack;
                    break;

                case Panel p when p.Tag as string is "card" or "cardSel":
                    p.BackColor = p.Tag as string == "cardSel" ? SelectedBack : Card;
                    p.ForeColor = Fore;
                    break;

                case Label l when l.Tag as string is "onCard":
                    l.BackColor = Color.Transparent;
                    l.ForeColor = Fore;
                    break;

                case Label l when l.Tag as string is "onCardDim":
                    l.BackColor = Color.Transparent;
                    l.ForeColor = DisabledFore;
                    break;

                case Label l when l.Tag as string is "caption":
                    l.ForeColor = ForeSecondary;
                    break;

                case Label l when l.Tag as string is "status":
                    l.ForeColor = ForeSecondary;
                    l.BackColor = Color.Transparent;
                    break;

                default:
                    c.BackColor = Back;
                    c.ForeColor = Fore;
                    break;
            }

            if (c.HasChildren) ApplyRecursive(c);
        }
    }

    public static void ApplyTabControlBorder(TabControl tab)
    {
        tab.BackColor = Back;
        tab.Invalidate();
    }
}
