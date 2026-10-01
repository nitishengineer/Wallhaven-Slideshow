using System.Diagnostics;
using System.Runtime.InteropServices;
using WallpaperRotator.Services;

namespace WallpaperRotator;

/// <summary>Logs tab: live tail of rotator.log with Refresh / Open log / Clear log.</summary>
public class LogsForm : Form
{
    private const int EM_GETFIRSTVISIBLELINE = 0x01CE;
    private const int EM_LINESCROLL = 0x00B6;
    private const int TailLines = 1500;

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

    private readonly TextBox _box = new();
    private readonly Label _infoLabel = new();

    public LogsForm()
    {
        ClientSize = new Size(960, 570);   // MUST stay the first line

        var bottomBar = new Panel { Dock = DockStyle.Bottom, Height = 46 };

        var refreshBtn = new Button { Text = "Refresh", Location = new Point(12, 8), Size = new Size(100, 30) };
        refreshBtn.Click += (s, e) => RefreshList();

        var openBtn = new Button { Text = "Open log", Location = new Point(120, 8), Size = new Size(100, 30) };
        openBtn.Click += (s, e) =>
        {
            if (File.Exists(LogService.LogFilePath))
                Process.Start(new ProcessStartInfo(LogService.LogFilePath) { UseShellExecute = true });
        };

        var clearBtn = new Button { Text = "Clear log", Location = new Point(228, 8), Size = new Size(100, 30) };
        clearBtn.Click += (s, e) =>
        {
            var answer = MessageBox.Show(this,
                "Reset the log file?\nCurrent contents are discarded.",
                "Clear log",
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2);
            if (answer != DialogResult.Yes) return;
            LogService.Clear();
            RefreshList();
        };

        _infoLabel.Dock = DockStyle.Fill;
        _infoLabel.TextAlign = ContentAlignment.MiddleRight;
        _infoLabel.Padding = new Padding(0, 0, 8, 0);
        _infoLabel.Text = "";

        bottomBar.Controls.Add(_infoLabel);
        bottomBar.Controls.Add(refreshBtn);
        bottomBar.Controls.Add(openBtn);
        bottomBar.Controls.Add(clearBtn);
        clearBtn.BringToFront();
        openBtn.BringToFront();
        refreshBtn.BringToFront();

        _box.Multiline = true;
        _box.ReadOnly = true;
        _box.Dock = DockStyle.Fill;
        _box.ScrollBars = ScrollBars.Both;
        _box.WordWrap = false;
        _box.HideSelection = false;
        _box.Font = new Font("Consolas", 9F);
        _box.BackColor = Theme.ControlBack;
        _box.ForeColor = Theme.Fore;

        Controls.Add(_box);
        Controls.Add(bottomBar);
        _box.BringToFront();

        LogService.LineWritten += OnLineWritten;
        Theme.Apply(this);
        RefreshList();
    }

    // Live feed: marshal background-thread log lines onto the UI thread
    private void OnLineWritten(string line)
    {
        if (IsDisposed || !IsHandleCreated) return;
        try { BeginInvoke(new Action(() => AppendLine(line))); }
        catch (InvalidOperationException) { }
    }

    private void AppendLine(string line)
    {
        bool near = IsNearBottom();
        int firstVisible = GetFirstVisible();
        _box.AppendText(line + Environment.NewLine);
        if (!near)   // user is reading history — don't yank them down
            SendMessage(_box.Handle, EM_LINESCROLL, IntPtr.Zero, (IntPtr)(firstVisible - GetFirstVisible()));
        UpdateInfo();
    }

    /// <summary>Reloads the tail of the log file (Refresh button + tab entry).</summary>
    public void RefreshList()
    {
        try
        {
            var lines = File.Exists(LogService.LogFilePath)
                ? File.ReadAllLines(LogService.LogFilePath)
                : Array.Empty<string>();
            var tail = lines.Length > TailLines ? lines[^TailLines..] : lines;
            _box.Text = string.Join(Environment.NewLine, tail);
            _box.SelectionStart = _box.TextLength;
            _box.ScrollToCaret();
            UpdateInfo();
        }
        catch { /* file momentarily locked — live feed continues regardless */ }
    }

    private void UpdateInfo()
    {
        long size = 0;
        try { size = new FileInfo(LogService.LogFilePath).Length; } catch { }
        _infoLabel.Text = $"{_box.Lines.Length} lines  •  {size / 1024} KB";
    }

    private int GetFirstVisible() =>
        SendMessage(_box.Handle, EM_GETFIRSTVISIBLELINE, IntPtr.Zero, IntPtr.Zero).ToInt32();

    private bool IsNearBottom()
    {
        int lineH = Math.Max(1, TextRenderer.MeasureText("Ay", _box.Font).Height);
        int visible = _box.ClientSize.Height / lineH;
        return GetFirstVisible() + visible >= _box.GetLineFromCharIndex(_box.TextLength) - 1;
    }
}