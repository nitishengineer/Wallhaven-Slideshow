using System.Runtime.InteropServices;

namespace WallpaperRotator;

public static class Theme
{
    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    [DllImport("uxtheme.dll", ExactSpelling = true, CharSet = CharSet.Unicode)]
    private static extern int SetWindowTheme(IntPtr hWnd, string? pszSubAppName, string? pszSubIdList);

    public static bool Dark { get; private set; }
    public static void Init(bool dark) => Dark = dark;
    public static void Toggle() => Dark = !Dark;

    public static Color Back => Dark ? Color.FromArgb(32, 32, 32) : SystemColors.Control;
    public static Color Fore => Dark ? Color.FromArgb(240, 240, 240) : SystemColors.ControlText;
    public static Color ControlBack => Dark ? Color.FromArgb(43, 43, 43) : SystemColors.Window;
    public static Color ControlBorder => Dark ? Color.FromArgb(58, 58, 58) : SystemColors.ControlDark;
    public static Color Accent => Dark ? Color.FromArgb(76, 194, 255) : Color.FromArgb(0, 103, 192);
    public static Color AccentFore => Dark ? Color.FromArgb(10, 10, 10) : Color.White;
    public static Color DisabledFore => Dark ? Color.FromArgb(105, 105, 105) : SystemColors.GrayText;
    private static readonly Color PreviewBack = Color.FromArgb(25, 25, 28);
    public static Color SelectedBack => Dark ? Color.FromArgb(64, 64, 72) : Color.FromArgb(205, 220, 240);
    public static Font UiFont => new Font("Segoe UI Variable Text", 9.5F);

    public static void Apply(Control root)
    {
        root.BackColor = Back;
        root.ForeColor = Fore;
        root.Font = UiFont;
        ApplyRecursive(root);

        if (root is Form form)
        {
            int value = Dark ? 1 : 0;
            DwmSetWindowAttribute(form.Handle, DWMWA_USE_IMMERSIVE_DARK_MODE, ref value, 4);
        }
    }

    private static void ApplyRecursive(Control parent)
    {
        foreach (Control c in parent.Controls)
        {
            switch (c)
            {
                case PictureBox pb:
                    pb.BackColor = PreviewBack;
                    break;

                case Button b:
                    b.FlatStyle = FlatStyle.Flat;
                    b.FlatAppearance.BorderColor = ControlBorder;
                    if (b.Tag as string == "accent")
                    {
                        b.BackColor = Accent;
                        b.ForeColor = AccentFore;
                        b.FlatAppearance.BorderColor = Accent;
                    }
                    else
                    {
                        b.BackColor = ControlBack;
                        b.ForeColor = Fore;
                    }
                    break;

                case ListView lv:
                    lv.BackColor = ControlBack;
                    lv.ForeColor = Fore;
                    if (lv.IsHandleCreated)
                        SetWindowTheme(lv.Handle, Dark ? "DarkMode_ItemsView" : null, null);
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
                        {
                            if (child.IsHandleCreated)
                                SetWindowTheme(child.Handle, Dark ? "DarkMode_Explorer" : null, null);
                        }
                    }
                    break;

                case TextBox tb:
                    tb.BackColor = ControlBack;
                    tb.ForeColor = Fore;
                    break;
                
                case Panel p when p.Tag as string is "card" or "cardSel":
                    p.BackColor = p.Tag as string == "cardSel" ? SelectedBack : ControlBack;
                    p.ForeColor = Fore;
                    break;
                
                case Label l when l.Tag as string == "onCard":
                    l.BackColor = Color.Transparent;
                    l.ForeColor = Fore;
                    break;
                
                case Label l when l.Tag as string == "onCardDim":
                    l.BackColor = Color.Transparent;
                    l.ForeColor = DisabledFore;
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
        tab.Padding = new Point(12, 8);
        tab.BackColor = Back;
        tab.Invalidate();
    }
}