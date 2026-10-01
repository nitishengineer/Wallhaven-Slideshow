using System.Diagnostics;
using WallpaperRotator.Services;

namespace WallpaperRotator;

/// <summary>Blocked tab: everything marked "never show again", with per-row Unblock.</summary>
public class BlockedForm : Form
{
    private readonly FlowLayoutPanel _flow = new();
    private readonly Label _statusLabel = new();
    private readonly List<BlockedEntry> _shown = new();

    public event Action<string>? StatusChanged;

    public BlockedForm()
    {
        ClientSize = new Size(960, 570);   // MUST stay the first line (anchor snapshot rule)

        var bottomBar = new Panel { Dock = DockStyle.Bottom, Height = 46 };
        _statusLabel.Dock = DockStyle.Fill;
        _statusLabel.TextAlign = ContentAlignment.MiddleRight;
        _statusLabel.Padding = new Padding(12, 0, 8, 0);
        _statusLabel.Text = "";
        bottomBar.Controls.Add(_statusLabel);

        _flow.Dock = DockStyle.Fill;
        _flow.FlowDirection = FlowDirection.TopDown;
        _flow.WrapContents = false;
        _flow.AutoScroll = true;
        _flow.Padding = new Padding(6);
        _flow.Resize += (s, e) => FitRowWidths();

        Controls.Add(_flow);
        Controls.Add(bottomBar);
        _flow.BringToFront();

        Theme.Apply(this);
        RefreshList();
    }

    private void Notify(string msg)
    {
        _statusLabel.Text = msg;
        StatusChanged?.Invoke(msg);
    }

    private Panel MakeRow(BlockedEntry e, int index)
    {
        var row = new Panel
        {
            Height = 46,
            Margin = new Padding(3),
            Name = e.Id,
            Tag = "card",
            BackColor = Theme.ControlBack,
            ForeColor = Theme.Fore
        };

        var lblNo = new Label { Text = $"{index}.", Location = new Point(10, 14), AutoSize = true, BackColor = Color.Transparent, ForeColor = Theme.DisabledFore, Tag = "onCardDim" };
        var lblName = new Label
        {
            Text = "🚫 " + e.Id,
            Location = new Point(52, 13),
            AutoSize = true,
            Font = new Font(Font, FontStyle.Bold),
            BackColor = Color.Transparent,
            ForeColor = Theme.Fore,
            Tag = "onCard"
        };
        var lblDate = new Label { Text = "blocked " + e.BlockedAt.ToString("yyyy-MM-dd  HH:mm"), Location = new Point(220, 14), AutoSize = true, BackColor = Color.Transparent, ForeColor = Theme.DisabledFore, Tag = "onCardDim" };
        var lblRes = new Label { Text = e.Resolution, Location = new Point(430, 14), AutoSize = true, BackColor = Color.Transparent, ForeColor = Theme.DisabledFore, Tag = "onCardDim" };

        var btnUnblock = new Button { Text = "Unblock", Size = new Size(90, 28), Location = new Point(600, 9) };
        var btnCopy = new Button { Text = "Copy", Size = new Size(68, 28), Location = new Point(696, 9) };
        var btnOpen = new Button { Text = "Open", Size = new Size(68, 28), Location = new Point(770, 9) };

        Tips.For(btnUnblock, "Allow this wallpaper back into the rotation pool.");
        Tips.For(btnCopy, "Copy the Wallhaven link to the clipboard.");
        Tips.For(btnOpen, "Open the Wallhaven page in your browser.");

        btnUnblock.Click += (s, ev) =>
        {
            BlocklistService.Remove(e.Id);
            Notify($"{e.Id} unblocked — back in the rotation pool.");
            RefreshList();
        };
        btnCopy.Click += (s, ev) =>
        {
            Clipboard.SetText($"https://wallhaven.cc/w/{e.Id}");
            Notify($"Link copied: wallhaven.cc/w/{e.Id}");
        };
        btnOpen.Click += (s, ev) =>
            Process.Start(new ProcessStartInfo($"https://wallhaven.cc/w/{e.Id}") { UseShellExecute = true });

        row.Controls.AddRange(new Control[] { lblNo, lblName, lblDate, lblRes, btnUnblock, btnCopy, btnOpen });
        return row;
    }

    private void FitRowWidths()
    {
        int w = _flow.ClientSize.Width - 16;
        foreach (Control c in _flow.Controls)
        {
            if (c is not Panel row) continue;
            row.Width = w;
            var buttons = row.Controls.OfType<Button>().ToList();
            if (buttons.Count == 3)
            {
                buttons[0].Location = new Point(w - 238, 9);   // Unblock
                buttons[1].Location = new Point(w - 142, 9);   // Copy
                buttons[2].Location = new Point(w - 68, 9);    // Open
            }
        }
    }

    public void RefreshList()
    {
        _flow.SuspendLayout();
        while (_flow.Controls.Count > 0)
        {
            var c = _flow.Controls[0];
            _flow.Controls.Remove(c);
            c.Dispose();
        }
        _shown.Clear();

        int i = 1;
        foreach (var e in BlocklistService.Entries)
        {
            _shown.Add(e);
            _flow.Controls.Add(MakeRow(e, i));
            i++;
        }
        _flow.ResumeLayout();
        FitRowWidths();
    }
}