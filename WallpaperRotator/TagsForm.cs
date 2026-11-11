using WallpaperRotator.Services;

namespace WallpaperRotator;

/// <summary>
/// Tags tab: two editable lists.  Included is ordered and rotation walks it
/// (or draws from a shuffle bag); Excluded is an unordered set of negative
/// terms that is applied to every search, pool or not.
/// </summary>
public class TagsForm : Form
{
    private const int MaxIncluded = 50;
    private const int MaxExcluded = 20;

    private readonly AppSettings _settings;

    private readonly Panel _topBar = new();
    private readonly Panel _body = new();
    private readonly Panel _bottomBar = new();

    private readonly FluentToggleSwitch _poolToggle = new();
    private readonly Label _hint = new();

    private readonly Panel _incColumn = new();
    private readonly Panel _excColumn = new();
    private readonly Label _incTitle = new();
    private readonly Label _excTitle = new();
    private readonly Label _incCount = new();
    private readonly Label _excCount = new();
    private readonly TagPicker _incPicker = new();
    private readonly TagPicker _excPicker = new();
    private readonly FlowLayoutPanel _incFlow = new();
    private readonly FlowLayoutPanel _excFlow = new();

    private readonly FluentToggleSwitch _modeToggle = new();
    private readonly Label _nextLabel = new();
    private readonly Label _statusLabel = new();

    private bool _syncing;

    // Drag-to-reorder state for the Included list.
    private TagRow? _dragRow;
    private Control? _dragSource;
    private Cursor? _dragCursor;
    private Point _dragStart;
    private bool _dragging;
    private int _dropIndex = -1;

    public event Action<string>? StatusChanged;
    public event Action? Changed;
    public event Action<bool>? PoolEnabledChanged;

    public TagsForm(AppSettings settings)
    {
        ClientSize = new Size(960, 570);   // keep first: the embedded-form anchor snapshot rule

        _settings = settings;
        _settings.PoolTags ??= new List<string>();
        _settings.ExcludedTags ??= new List<string>();
        _settings.PoolBag ??= new List<string>();

        BuildUi();
        WireEvents();

        Theme.Apply(this);
        RefreshView();
    }

    /// <summary>When true, Wallhaven's NSFW tags may appear in suggestions.</summary>
    public bool AllowNsfw
    {
        set
        {
            _incPicker.AllowNsfw = value;
            _excPicker.AllowNsfw = value;
        }
    }

    /// <summary>Synced with the Rotation tab's "Use multiple tags" toggle.</summary>
    public bool PoolEnabled
    {
        get => _poolToggle.Checked;
        set
        {
            if (_poolToggle.Checked == value)
            {
                UpdateHint();
                return;
            }

            _syncing = true;
            _poolToggle.Checked = value;
            _syncing = false;
            UpdateHint();
        }
    }

    public string OrderMode => _modeToggle.Checked ? "shuffle" : "order";

    // ---------- construction ----------
    private void BuildUi()
    {
        _topBar.Dock = DockStyle.Top;
        _topBar.Height = 58;

        _poolToggle.Text = "Use multiple tags";
        _poolToggle.Location = new Point(16, 8);
        _poolToggle.AutoSize = true;
        Tips.For(_poolToggle, "Rotate through the Included tags instead of the single Category on the Rotate tab. " +
                              "Excluded tags are always filtered out.");
        _topBar.Controls.Add(_poolToggle);

        _hint.Location = new Point(16, 34);
        _hint.AutoSize = true;
        _hint.BackColor = Color.Transparent;
        _hint.ForeColor = Theme.ForeSecondary;
        _hint.Tag = "caption";
        _topBar.Controls.Add(_hint);

        _body.Dock = DockStyle.Fill;
        _body.Padding = new Padding(14, 0, 14, 0);
        BuildColumn(_incColumn, _incTitle, _incCount, _incPicker, _incFlow,
            "Included", "Search or add a tag…", true);
        BuildColumn(_excColumn, _excTitle, _excCount, _excPicker, _excFlow,
            "Excluded", "Search or exclude a tag…", false);

        _body.Controls.Add(_incColumn);
        _body.Controls.Add(_excColumn);

        _bottomBar.Dock = DockStyle.Bottom;
        _bottomBar.Height = 58;

        _modeToggle.Text = "Shuffle included tags";
        _modeToggle.Location = new Point(16, 16);
        _modeToggle.AutoSize = true;
        Tips.For(_modeToggle, "Off: follow the Included order, one tag per rotation. " +
                              "On: draw from a shuffle bag so every tag is used before any repeats.");
        _bottomBar.Controls.Add(_modeToggle);

        _nextLabel.Location = new Point(268, 20);
        _nextLabel.AutoSize = true;
        _nextLabel.BackColor = Color.Transparent;
        _nextLabel.Tag = "caption";
        _bottomBar.Controls.Add(_nextLabel);

        _statusLabel.AutoSize = false;
        _statusLabel.Size = new Size(300, 20);
        _statusLabel.Location = new Point(644, 20);
        _statusLabel.TextAlign = ContentAlignment.MiddleRight;
        _statusLabel.BackColor = Color.Transparent;
        _statusLabel.Tag = "caption";
        _bottomBar.Controls.Add(_statusLabel);

        Controls.Add(_body);
        Controls.Add(_bottomBar);
        Controls.Add(_topBar);
        _body.BringToFront();

        _body.Resize += (s, e) => LayoutColumns();
        Resize += (s, e) => LayoutBottom();
        LayoutBottom();
    }

    private void BuildColumn(
        Panel column, Label title, Label count, TagPicker picker,
        FlowLayoutPanel flow, string titleText, string placeholder, bool included)
    {
        column.BackColor = Theme.Back;

        title.Text = titleText;
        title.Location = new Point(0, 0);
        title.AutoSize = true;
        title.Font = Theme.StrongFont;
        title.BackColor = Color.Transparent;
        title.ForeColor = Theme.Fore;
        column.Controls.Add(title);

        count.Location = new Point(78, 4);
        count.AutoSize = true;
        count.BackColor = Color.Transparent;
        count.Tag = "caption";
        column.Controls.Add(count);

        picker.PlaceholderText = placeholder;
        picker.AllowNsfw = _settings.PuritySketchy;
        column.Controls.Add(picker);

        flow.FlowDirection = FlowDirection.TopDown;
        flow.WrapContents = false;
        flow.AutoScroll = true;
        flow.BackColor = Theme.Back;
        flow.Padding = new Padding(2);
        flow.Resize += (s, e) => FitRowWidths(flow);
        column.Controls.Add(flow);

        Tips.For(picker, included
            ? "Type a tag and press Enter, or pick a Wallhaven suggestion. Multi-word tags are fine; commas add several at once."
            : "Type a tag and press Enter, or pick a suggestion. Any wallpaper carrying this tag is filtered out of every search.");
    }

    private void WireEvents()
    {
        _poolToggle.CheckedChanged += (s, e) => OnPoolToggleChanged();
        _modeToggle.CheckedChanged += (s, e) => OnModeToggleChanged();

        _incPicker.TagChosen += AddIncluded;
        _excPicker.TagChosen += AddExcluded;

        _incFlow.Resize += (s, e) => FitRowWidths(_incFlow);
        _excFlow.Resize += (s, e) => FitRowWidths(_excFlow);

        _incFlow.Paint += PaintDropLine;

        // A child suggestion list cannot follow scrolled rows, so hide it.
        _incFlow.Scroll += (s, e) => _incPicker.CloseSuggestions();
        _excFlow.Scroll += (s, e) => _excPicker.CloseSuggestions();
    }

    // ---------- layout ----------
    private void LayoutColumns()
    {
        _incPicker.CloseSuggestions();
        _excPicker.CloseSuggestions();

        const int pad = 14;
        const int gap = 16;
        int width = _body.ClientSize.Width;
        int height = _body.ClientSize.Height;
        int columnWidth = Math.Max(220, (width - pad * 2 - gap) / 2);

        _incColumn.SetBounds(pad, 0, columnWidth, height);
        _excColumn.SetBounds(pad + columnWidth + gap, 0, columnWidth, height);

        LayoutColumn(_incColumn, _incPicker, _incFlow);
        LayoutColumn(_excColumn, _excPicker, _excFlow);
    }

    private static void LayoutColumn(Panel column, TagPicker picker, FlowLayoutPanel flow)
    {
        int w = column.ClientSize.Width;
        picker.SetBounds(0, 28, w, 34);
        flow.SetBounds(0, 72, w, Math.Max(0, column.ClientSize.Height - 72));
    }

    private void LayoutBottom()
    {
        _statusLabel.Left = Math.Max(16, ClientSize.Width - _statusLabel.Width - 18);
    }

    // ---------- rows ----------
    private sealed class TagRow : CardPanel
    {
        public bool Included;
        public Label IndexLabel = new();
        public Label NameLabel = new();
        public FluentButton Up = new();
        public FluentButton Down = new();
        public FluentButton Remove = new();
    }

    private TagRow MakeRow(string tag, int index, bool included)
    {
        var row = new TagRow
        {
            Included = included,
            Height = 38,
            Margin = new Padding(2),
            Tag = "card",
            BackColor = Theme.Card,
            ForeColor = Theme.Fore
        };

        row.IndexLabel = new Label
        {
            Text = included ? (index + 1).ToString() : "",
            Location = new Point(9, 12),
            AutoSize = true,
            BackColor = Color.Transparent,
            ForeColor = Theme.DisabledFore,
            Tag = "onCardDim"
        };
        row.Controls.Add(row.IndexLabel);

        row.NameLabel = new Label
        {
            Text = tag,
            Location = new Point(included ? 34 : 12, 11),
            AutoSize = false,
            Height = 18,
            BackColor = Color.Transparent,
            ForeColor = Theme.Fore,
            Tag = "onCard",
            Font = new Font(Theme.UiFont, FontStyle.Bold),
            AutoEllipsis = true
        };
        row.Controls.Add(row.NameLabel);

        if (included)
        {
            row.Up = IconButton(FluentGlyph.ChevronUp, "Move this tag up in the rotation order.");
            row.Down = IconButton(FluentGlyph.ChevronDown, "Move this tag down in the rotation order.");
            row.Up.Click += (s, e) => MoveIncluded(index, -1);
            row.Down.Click += (s, e) => MoveIncluded(index, 1);
            row.Controls.Add(row.Up);
            row.Controls.Add(row.Down);

            AttachDrag(row, row);
            AttachDrag(row.NameLabel, row);
            AttachDrag(row.IndexLabel, row);
            var dragTip = "Drag this row to change the rotation order (or use the arrows).";
            Tips.For(row, dragTip);
            Tips.For(row.NameLabel, dragTip);
            Tips.For(row.IndexLabel, dragTip);
            row.NameLabel.Cursor = Cursors.SizeAll;
        }

        row.Remove = IconButton(FluentGlyph.Trash, included
            ? "Remove this tag from the rotation pool."
            : "Stop filtering out this tag.");
        row.Remove.Variant = FluentButtonVariant.Danger;
        row.Remove.Click += (s, e) =>
        {
            if (included) RemoveIncluded(index);
            else RemoveExcluded(index);
        };
        row.Controls.Add(row.Remove);

        return row;
    }

    private static FluentButton IconButton(FluentGlyph icon, string tip)
    {
        var button = new FluentButton
        {
            Icon = icon,
            IconSize = 14,
            Size = new Size(30, 26),
            AccessibleName = tip
        };
        button.ApplyTheme();
        Tips.For(button, tip);
        return button;
    }

    // ---------- drag to reorder ----------
    private void AttachDrag(Control source, TagRow row)
    {
        source.MouseDown += (s, e) =>
        {
            if (e.Button != MouseButtons.Left) return;

            _dragRow = row;
            _dragSource = source;
            _dragStart = e.Location;
            _dragging = false;
        };

        source.MouseMove += (s, e) =>
        {
            if (_dragRow != row || _dragSource != source) return;

            if (e.Button != MouseButtons.Left)
            {
                ResetDrag();
                return;
            }

            if (!_dragging)
            {
                var dragSize = SystemInformation.DragSize;
                if (Math.Abs(e.X - _dragStart.X) < dragSize.Width &&
                    Math.Abs(e.Y - _dragStart.Y) < dragSize.Height) return;

                _dragging = true;
                source.Capture = true;
                _dragCursor = source.Cursor;
                source.Cursor = Cursors.SizeAll;
            }

            Point p = _incFlow.PointToClient(source.PointToScreen(e.Location));
            AutoScrollDuringDrag(p);

            if (!_incFlow.ClientRectangle.Contains(p))
            {
                SetDropIndex(-1);
                return;
            }

            SetDropIndex(InsertIndexAt(p));
        };

        source.MouseUp += (s, e) =>
        {
            if (_dragRow != row) return;

            bool wasDragging = _dragging;
            int drop = _dropIndex;
            string tag = row.NameLabel.Text;
            ResetDrag();

            if (!wasDragging || drop < 0) return;

            int from = _settings.PoolTags.FindIndex(t => Same(t, tag));
            if (from < 0) return;

            // Removing the row shifts everything after it up by one.
            int to = drop > from ? drop - 1 : drop;
            MoveIncludedTo(from, to);
        };

        source.MouseCaptureChanged += (s, e) =>
        {
            if (source.Capture || !_dragging || _dragRow != row) return;
            ResetDrag();
        };
    }

    private void ResetDrag()
    {
        var source = _dragSource;
        var cursor = _dragCursor;

        // Clear the state first: releasing capture raises MouseCaptureChanged,
        // which must see that no drag is active any more.
        _dragRow = null;
        _dragSource = null;
        _dragCursor = null;
        _dragging = false;
        SetDropIndex(-1);

        if (source is { IsDisposed: false })
        {
            source.Cursor = cursor ?? Cursors.Default;
            if (source.Capture) source.Capture = false;
        }
    }

    /// <summary>Where a row dropped at <paramref name="p"/> would be inserted.</summary>
    private int InsertIndexAt(Point p)
    {
        var rows = _incFlow.Controls.OfType<TagRow>().ToList();
        for (int i = 0; i < rows.Count; i++)
        {
            if (p.Y < rows[i].Top + rows[i].Height / 2) return i;
        }
        return rows.Count;
    }

    private void AutoScrollDuringDrag(Point p)
    {
        if (!_incFlow.VerticalScroll.Visible) return;

        var scroll = _incFlow.VerticalScroll;
        int max = Math.Max(scroll.Minimum, scroll.Maximum - scroll.LargeChange + 1);
        int target = scroll.Value;

        if (p.Y < 16) target -= 24;
        else if (p.Y > _incFlow.ClientSize.Height - 16) target += 24;
        else return;

        target = Math.Clamp(target, scroll.Minimum, max);
        if (target == scroll.Value) return;

        scroll.Value = target;
        _incFlow.Invalidate();
    }

    private void SetDropIndex(int index)
    {
        if (_dropIndex == index) return;
        _dropIndex = index;
        _incFlow.Invalidate();
    }

    private void PaintDropLine(object? sender, PaintEventArgs e)
    {
        if (_dropIndex < 0) return;

        var rows = _incFlow.Controls.OfType<TagRow>().ToList();
        int y = rows.Count == 0
            ? 4
            : _dropIndex >= rows.Count
                ? rows[^1].Bottom + 2
                : rows[_dropIndex].Top - 2;

        using var pen = new Pen(Theme.Accent, 3f);
        e.Graphics.DrawLine(pen, 4, y, Math.Max(5, _incFlow.ClientSize.Width - 4), y);
    }

    private void RebuildRows()
    {
        RebuildFlow(_incFlow, _settings.PoolTags, true,
            "No included tags yet — add one above.");
        RebuildFlow(_excFlow, _settings.ExcludedTags, false,
            "No excluded tags — nothing is being filtered out.");

        FitRowWidths(_incFlow);
        FitRowWidths(_excFlow);
    }

    private void RebuildFlow(FlowLayoutPanel flow, List<string> tags, bool included, string emptyText)
    {
        flow.SuspendLayout();

        while (flow.Controls.Count > 0)
        {
            var control = flow.Controls[0];
            flow.Controls.Remove(control);
            control.Dispose();
        }

        for (int i = 0; i < tags.Count; i++)
            flow.Controls.Add(MakeRow(tags[i], i, included));

        if (tags.Count == 0)
        {
            flow.Controls.Add(new Label
            {
                Text = emptyText,
                Height = 38,
                AutoSize = false,
                BackColor = Color.Transparent,
                ForeColor = Theme.DisabledFore,
                Tag = "onCardDim",
                Padding = new Padding(10, 11, 0, 0)
            });
        }

        flow.ResumeLayout();
        FitRowWidths(flow);
    }

    private static void FitRowWidths(FlowLayoutPanel flow)
    {
        int width = Math.Max(180, flow.ClientSize.Width - 8);

        foreach (Control control in flow.Controls)
        {
            control.Width = width;

            if (control is not TagRow row) continue;

            if (row.Included)
            {
                row.Remove.SetBounds(width - 36, 6, 30, 26);
                row.Down.SetBounds(width - 70, 6, 30, 26);
                row.Up.SetBounds(width - 104, 6, 30, 26);
                row.NameLabel.SetBounds(34, 11, Math.Max(40, width - 34 - 112), 18);
            }
            else
            {
                row.Remove.SetBounds(width - 36, 6, 30, 26);
                row.NameLabel.SetBounds(12, 11, Math.Max(40, width - 56), 18);
            }
        }
    }

    // ---------- tag mutations ----------
    private void AddIncluded(string raw)
    {
        string tag = Sanitize(raw);
        if (tag.Length == 0) return;

        if (Contains(_settings.PoolTags, tag))
        {
            Notify($"\"{tag}\" is already included.");
            return;
        }
        if (_settings.PoolTags.Count >= MaxIncluded)
        {
            Notify($"Included list is full ({MaxIncluded} tags).");
            return;
        }

        bool moved = _settings.ExcludedTags.RemoveAll(t => Same(t, tag)) > 0;
        _settings.PoolTags.Add(tag);
        _settings.PoolBag.RemoveAll(t => Same(t, tag));

        AfterChange(moved
            ? $"Moved \"{tag}\" from Excluded to Included."
            : $"Added \"{tag}\" to Included.");
    }

    private void AddExcluded(string raw)
    {
        string tag = Sanitize(raw);
        if (tag.Length == 0) return;

        if (Contains(_settings.ExcludedTags, tag))
        {
            Notify($"\"{tag}\" is already excluded.");
            return;
        }
        if (_settings.ExcludedTags.Count >= MaxExcluded)
        {
            Notify($"Excluded list is full ({MaxExcluded} tags).");
            return;
        }

        bool moved = false;
        int removedAt = _settings.PoolTags.FindIndex(t => Same(t, tag));
        if (removedAt >= 0)
        {
            _settings.PoolTags.RemoveAt(removedAt);
            _settings.PoolBag.RemoveAll(t => Same(t, tag));
            if (_settings.PoolCursor > removedAt) _settings.PoolCursor--;
            if (_settings.PoolCursor < 0) _settings.PoolCursor = 0;
            moved = true;
        }

        _settings.ExcludedTags.Add(tag);
        AfterChange(moved
            ? $"Moved \"{tag}\" from Included to Excluded."
            : $"Added \"{tag}\" to Excluded.");
    }

    private void RemoveIncluded(int index)
    {
        if (index < 0 || index >= _settings.PoolTags.Count) return;

        string tag = _settings.PoolTags[index];
        _settings.PoolTags.RemoveAt(index);
        _settings.PoolBag.RemoveAll(t => Same(t, tag));
        if (_settings.PoolCursor > index) _settings.PoolCursor--;
        if (_settings.PoolCursor < 0) _settings.PoolCursor = 0;

        AfterChange($"Removed \"{tag}\" from Included.");
    }

    private void RemoveExcluded(int index)
    {
        if (index < 0 || index >= _settings.ExcludedTags.Count) return;

        string tag = _settings.ExcludedTags[index];
        _settings.ExcludedTags.RemoveAt(index);
        AfterChange($"Removed \"{tag}\" from Excluded.");
    }

    private void MoveIncluded(int index, int delta) => MoveIncludedTo(index, index + delta);

    private void MoveIncludedTo(int from, int to)
    {
        var tags = _settings.PoolTags;
        if (from < 0 || from >= tags.Count) return;

        to = Math.Clamp(to, 0, tags.Count - 1);
        if (from == to) return;

        // Keep the cursor pointing at the same "next" tag even if the user
        // reorders the list while a schedule is running.
        string next = tags[Math.Clamp(_settings.PoolCursor, 0, tags.Count - 1)];
        string moved = tags[from];
        tags.RemoveAt(from);
        tags.Insert(to, moved);

        int newNext = tags.FindIndex(t => Same(t, next));
        if (newNext >= 0) _settings.PoolCursor = newNext;

        AfterChange($"Moved \"{moved}\" to position {to + 1}.");
    }

    private void AfterChange(string message)
    {
        RebuildRows();
        UpdateSummary();
        Notify(message);
        Changed?.Invoke();
    }

    private static string Sanitize(string raw)
    {
        string tag = (raw ?? "").Trim().Trim('"').Trim();
        if (tag.StartsWith('-') || tag.StartsWith('+'))
            tag = tag[1..].Trim();
        return tag;
    }

    private static bool Contains(List<string> tags, string tag) => tags.Any(t => Same(t, tag));
    private static bool Same(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    // ---------- summaries ----------
    private void OnPoolToggleChanged()
    {
        if (_syncing) return;

        _settings.UseTagPool = _poolToggle.Checked;
        UpdateHint();
        Notify(_poolToggle.Checked
            ? "Tag pool on — rotation picks one included tag per change."
            : "Tag pool off — rotation uses the single Category. Excluded tags still apply.");
        Changed?.Invoke();
        PoolEnabledChanged?.Invoke(_poolToggle.Checked);
    }

    private void OnModeToggleChanged()
    {
        if (_syncing) return;

        _settings.TagOrderMode = _modeToggle.Checked ? "shuffle" : "order";
        _settings.PoolBag.Clear();
        UpdateSummary();
        Notify(_modeToggle.Checked
            ? "Shuffle mode — every included tag is used once before repeating."
            : "Order mode — included tags are used in the order shown.");
        Changed?.Invoke();
    }

    private void UpdateHint()
    {
        if (_settings.ScheduleEnabled)
            _hint.Text = "Time-of-day schedule is on — turn it off to use the tag pool.";
        else if (!_poolToggle.Checked)
            _hint.Text = "Tag pool is off — rotation uses the single Category. Excluded tags still apply.";
        else
            _hint.Text = "One included tag is picked per rotation — drag rows to reorder. Excluded tags are filtered from every search.";
    }

    private void UpdateSummary()
    {
        int included = _settings.PoolTags.Count;
        int excluded = _settings.ExcludedTags.Count;

        _incCount.Text = included == 1 ? "1 tag" : $"{included} tags";
        _excCount.Text = excluded == 1 ? "1 tag" : $"{excluded} tags";
        _statusLabel.Text = $"{included} included · {excluded} excluded · " +
                            (_modeToggle.Checked ? "shuffle" : "in order");
        _nextLabel.Text = NextPreview();
    }

    private string NextPreview()
    {
        int count = _settings.PoolTags.Count;
        if (count == 0) return "Add a tag to start";

        if (_modeToggle.Checked)
        {
            if (_settings.PoolBag.Count > 0)
                return $"Next: {_settings.PoolBag[0]}";
            return "Next: a fresh shuffle";
        }

        int index = Math.Clamp(_settings.PoolCursor, 0, count - 1);
        return $"Next: {_settings.PoolTags[index]} ({index + 1}/{count})";
    }

    private void Notify(string message)
    {
        _statusLabel.Text = message;
        StatusChanged?.Invoke(message);
    }

    protected override void OnVisibleChanged(EventArgs e)
    {
        base.OnVisibleChanged(e);
        if (!Visible)
        {
            _incPicker.CloseSuggestions();
            _excPicker.CloseSuggestions();
        }
    }

    // ---------- public ----------
    /// <summary>Re-reads the shared settings into the controls (tab selected, theme change, etc.).</summary>
    public void RefreshView()
    {
        _settings.PoolTags ??= new List<string>();
        _settings.ExcludedTags ??= new List<string>();
        _settings.PoolBag ??= new List<string>();

        _syncing = true;
        _poolToggle.Checked = _settings.UseTagPool;
        _modeToggle.Checked = string.Equals(_settings.TagOrderMode, "shuffle", StringComparison.OrdinalIgnoreCase);
        _syncing = false;

        AllowNsfw = _settings.PuritySketchy;
        UpdateHint();
        RebuildRows();
        UpdateSummary();
        LayoutColumns();
    }
}
