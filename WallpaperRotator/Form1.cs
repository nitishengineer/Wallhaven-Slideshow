using Microsoft.Win32;
using System.Diagnostics;
using WallpaperRotator.Models;
using WallpaperRotator.Services;

namespace WallpaperRotator;

public partial class Form1 : Form
{
    private const string RunKeyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
    private const string RunName = "WallpaperRotator";

    private readonly System.Windows.Forms.Timer _timer = new();
    private readonly RotationEngine _engine = new();
    private readonly AppSettings _settings = SettingsService.Load();
    private NotifyIcon _trayIcon = new();
    private ToolStripMenuItem _pauseItem = new();
    private ToolStripMenuItem _multiItem = new();

    private readonly DarkTabControl _tabs = new();
    private readonly TabPage _rotatePage = new("Rotate");
    private readonly TabPage _cachePage = new("Cache");
    private readonly TabPage _favoritePage = new("Favorite");
    private readonly TabPage _historyLogPage = new("History");
    private readonly TabPage _blockedPage = new("Blocked");
    private readonly TabPage _logsPage = new("Logs");
    private readonly HistoryForm _cacheEmbed = new();
    private readonly FavoriteForm _favoriteEmbed = new();
    private readonly HistoryLogForm _historyLogEmbed = new();
    private readonly BlockedForm _blockedEmbed = new();
    private readonly LogsForm _logsEmbed = new();

    private readonly List<(string Label, int Minutes)> _intervals = new()
    {
        ("1 minute", 1), ("10 minutes", 10), ("15 minutes", 15), ("30 minutes", 30),
        ("1 hour", 60), ("2 hours", 120), ("4 hours", 240),
        ("6 hours", 360), ("12 hours", 720), ("24 hours", 1440)
    };

    private static readonly (string Label, string Value)[] SortOrders =
    {
        ("Relevance", "relevance"),
        ("Random", "random"),
        ("Date added", "date_added"),
        ("Views", "views"),
        ("Favorites", "favorites"),
        ("Toplist", "toplist"),
        ("Hot", "hot"),
    };

    private static readonly string[] Ratios =
    {
        "", "16x9", "16x10", "21x9", "32x9", "48x9",
        "9x16", "10x16", "9x18", "1x1", "3x2", "4x3", "5x4",
        "wide", "portrait", "ultrawide", "square"
    };

    private static readonly string[] Palette =
    {
        "660000","990000","cc0000","cc4455","cc6699","ea4c88","9933cc",
        "663399","333399","0066cc","0099cc","66cccc","77cc33",
        "669900","336600","666600","999900","cccc33","cccc00",
        "cc9900","ff9900","ff6600","cc6633","996633","663300",
        "000000","999999","cccccc","ffffff","424157"
    };

    private static readonly int[] PageCaps = { 10, 20, 30, 40, 50 };

    // ----- Rotate tab: left column -----
    private readonly PictureBox _preview = new();
    private readonly Button _viewBtn = new();
    private readonly Button _favBtn = new();
    private readonly Button _blockBtn = new();
    private readonly Label _currentLabel = new();
    private readonly Button _nextBtn = new();
    private readonly CheckBox _pauseBox = new();
    private readonly CheckBox _autostartBox = new();
    private readonly CheckBox _multiBox = new();
    private readonly CheckBox _fullscreenBox = new();
    private readonly CheckBox _offlineBox = new();
    private readonly CheckBox _pruneBox = new();
    private readonly NumericUpDown _maxFilesBox = new();
    private readonly NumericUpDown _maxMbBox = new();
    private readonly Label _filesOrLabel = new();
    private readonly Label _mbLabel = new();
    private readonly CheckBox _historyCapBox = new();
    private readonly NumericUpDown _historyCapValue = new();
    private readonly CheckBox _boostBox = new();
    private readonly NumericUpDown _boostValue = new();
    private readonly Label _pctLabel = new();
    private readonly CheckBox _scheduleBox = new();
    private readonly CheckBox _solarBox = new();
    private readonly NumericUpDown _latBox = new();
    private readonly NumericUpDown _lonBox = new();
    private readonly Label _morningLabel = new();
    private readonly Label _afternoonLabel = new();
    private readonly Label _eveningLabel = new();
    private readonly Label _nightLabel = new();
    private readonly TextBox _morningBox = new();
    private readonly TextBox _afternoonBox = new();
    private readonly TextBox _eveningBox = new();
    private readonly TextBox _nightBox = new();
    private readonly Label _statusLabel = new();
    private readonly List<Control> _scheduleControls = new();

    // ----- Rotate tab: right column -----
    private readonly ComboBox _tagBox = new();
    private readonly CheckBox _generalBox = new();
    private readonly CheckBox _animeBox = new();
    private readonly CheckBox _peopleBox = new();
    private readonly CheckBox _sketchyBox = new();
    private readonly ComboBox _intervalBox = new();
    private readonly ComboBox _orderBox = new();
    private readonly ComboBox _fitBox = new();
    private readonly CheckBox _matchScreenBox = new();
    private readonly NumericUpDown _minWBox = new();
    private readonly NumericUpDown _minHBox = new();
    private readonly ComboBox _resModeBox = new();
    private readonly ComboBox _ratioBox = new();
    private readonly ComboBox _colorBox = new();
    private readonly ComboBox _pageCapBox = new();
    private readonly TextBox _keyBox = new();
    private readonly ComboBox _collectionBox = new();
    private readonly ThemeToggleButton _themeBtn = new();
    private readonly List<(int Id, string Label)> _collections = new();

    private Icon? _appIcon;
    private bool _paused;
    private bool _rotating;
    private bool _tagLocked;
    private bool _resLocked;
    private bool _cacheLocked;
    private bool _capLocked;
    private bool _boostLocked;
    private bool _solarLocked;
    private string _currentPath = "";
    private string _currentId = "";
    private string _currentRes = "";

    public Form1()
    {
        InitializeComponent();
        Text = "Wallpaper Rotator";
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;

        _appIcon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        if (_appIcon is not null) Icon = _appIcon;

        // ----- tabs -----
        _tabs.Dock = DockStyle.Fill;
        _tabs.DrawMode = TabDrawMode.OwnerDrawFixed;
        _tabs.Padding = new Point(12, 8);
        _tabs.TabPages.Add(_rotatePage);
        _tabs.TabPages.Add(_cachePage);
        _tabs.TabPages.Add(_favoritePage);
        _tabs.TabPages.Add(_historyLogPage);
        _tabs.TabPages.Add(_blockedPage);
        _tabs.TabPages.Add(_logsPage);
        _tabs.DrawItem += DrawTabItem;
        _tabs.SelectedIndexChanged += (s, e) =>
        {
            if (_tabs.SelectedIndex == 1) _cacheEmbed.RefreshList();
            else if (_tabs.SelectedIndex == 2) _favoriteEmbed.RefreshList();
            else if (_tabs.SelectedIndex == 3) _historyLogEmbed.RefreshList();
            else if (_tabs.SelectedIndex == 4) _blockedEmbed.RefreshList();
            else if (_tabs.SelectedIndex == 5) _logsEmbed.RefreshList();
        };
        Controls.Add(_tabs);

        // Theme toggle floats on the empty right side of the tab strip
        _themeBtn.Size = new Size(32, 28);
        _themeBtn.Location = new Point(952, 4);
        _themeBtn.FlatStyle = FlatStyle.Flat;
        _themeBtn.FlatAppearance.BorderSize = 0;
        _themeBtn.Font = new Font("Segoe UI Emoji", 12F);
        _themeBtn.TabStop = false;
        Controls.Add(_themeBtn);
        _themeBtn.BringToFront();

        // ----- embedded Cache tab -----
        _cacheEmbed.TopLevel = false;
        _cacheEmbed.FormBorderStyle = FormBorderStyle.None;
        _cacheEmbed.Dock = DockStyle.Fill;
        _cachePage.Controls.Add(_cacheEmbed);
        _cacheEmbed.Show();
        _cacheEmbed.StatusChanged += msg => _statusLabel.Text = msg;
        _cacheEmbed.Applied += (id, path, res) => ShowCurrent(id, path, res);
        _cacheEmbed.FavoritesChanged += UpdateFavoriteButton;

        // ----- embedded Favorite tab -----
        _favoriteEmbed.TopLevel = false;
        _favoriteEmbed.FormBorderStyle = FormBorderStyle.None;
        _favoriteEmbed.Dock = DockStyle.Fill;
        _favoritePage.Controls.Add(_favoriteEmbed);
        _favoriteEmbed.Show();
        _favoriteEmbed.StatusChanged += msg => _statusLabel.Text = msg;
        _favoriteEmbed.Applied += (id, path, res) => ShowCurrent(id, path, res);
        _favoriteEmbed.FavoritesChanged += UpdateFavoriteButton;

        // ----- embedded History tab -----
        _historyLogEmbed.TopLevel = false;
        _historyLogEmbed.FormBorderStyle = FormBorderStyle.None;
        _historyLogEmbed.Dock = DockStyle.Fill;
        _historyLogPage.Controls.Add(_historyLogEmbed);
        _historyLogEmbed.Show();
        _historyLogEmbed.StatusChanged += msg => _statusLabel.Text = msg;
        _historyLogEmbed.Applied += (id, path, res) => ShowCurrent(id, path, res);

        // ----- embedded Blocked tab -----
        _blockedEmbed.TopLevel = false;
        _blockedEmbed.FormBorderStyle = FormBorderStyle.None;
        _blockedEmbed.Dock = DockStyle.Fill;
        _blockedPage.Controls.Add(_blockedEmbed);
        _blockedEmbed.Show();
        _blockedEmbed.StatusChanged += msg => _statusLabel.Text = msg;
        BlocklistService.Changed += UpdateBlockButton;

        // ----- embedded Logs tab -----
        _logsEmbed.TopLevel = false;
        _logsEmbed.FormBorderStyle = FormBorderStyle.None;
        _logsEmbed.Dock = DockStyle.Fill;
        _logsPage.Controls.Add(_logsEmbed);
        _logsEmbed.Show();

        BuildUi();
        BuildTray();

        // ----- Restore saved settings -----
        _tagBox.Text = _settings.Tag;
        SelectInterval(_settings.IntervalMinutes);
        SelectCombo(_orderBox, SortOrders.Select(s => s.Label),
            SortOrders.FirstOrDefault(s => s.Value == _settings.Sorting).Label);
        SelectCombo(_fitBox, Enum.GetNames<WallpaperFit>(), _settings.Fit);
        _autostartBox.Checked = IsAutostartEnabled();
        _fullscreenBox.Checked = _settings.PauseOnFullscreen;
        _offlineBox.Checked = _settings.OfflineMode;
        _multiBox.Checked = _settings.MultiMonitor;

        _pruneBox.Checked = _settings.CachePruneEnabled;
        _maxFilesBox.Value = Math.Clamp(_settings.CacheMaxFiles, (int)_maxFilesBox.Minimum, (int)_maxFilesBox.Maximum);
        _maxMbBox.Value = Math.Clamp((decimal)_settings.CacheMaxMb, _maxMbBox.Minimum, _maxMbBox.Maximum);
        SyncCacheSettings();

        _historyCapBox.Checked = _settings.HistoryCapEnabled;
        _historyCapValue.Value = Math.Clamp(_settings.HistoryCap, (int)_historyCapValue.Minimum, (int)_historyCapValue.Maximum);
        SyncHistoryCap();

        _boostBox.Checked = _settings.FavoriteBoostEnabled;
        _boostValue.Value = Math.Clamp(_settings.FavoriteBoostPercent, (int)_boostValue.Minimum, (int)_boostValue.Maximum);

        _matchScreenBox.Checked = _settings.ResMatchScreen;
        _minWBox.Value = Math.Clamp(_settings.MinWidth, (int)_minWBox.Minimum, (int)_minWBox.Maximum);
        _minHBox.Value = Math.Clamp(_settings.MinHeight, (int)_minHBox.Minimum, (int)_minHBox.Maximum);
        UpdateResEnabled();

        _scheduleBox.Checked = _settings.ScheduleEnabled;
        _morningBox.Text = _settings.MorningTag;
        _afternoonBox.Text = _settings.AfternoonTag;
        _eveningBox.Text = _settings.EveningTag;
        _nightBox.Text = _settings.NightTag;
        _solarBox.Checked = _settings.SolarScheduleEnabled;
        _latBox.Value = Math.Clamp((decimal)_settings.Latitude, _latBox.Minimum, _latBox.Maximum);
        _lonBox.Value = Math.Clamp((decimal)_settings.Longitude, _lonBox.Minimum, _lonBox.Maximum);
        UpdateSolarEnabled();
        UpdateScheduleLabels();
        UpdateTagBoxEnabled();

        _generalBox.Checked = _settings.CatGeneral;
        _animeBox.Checked = _settings.CatAnime;
        _peopleBox.Checked = _settings.CatPeople;
        _sketchyBox.Checked = _settings.PuritySketchy;
        var ratioIdx = Array.IndexOf(Ratios, _settings.Ratio);
        _ratioBox.SelectedIndex = ratioIdx >= 0 ? ratioIdx : 0;
        var colorIdx = Array.IndexOf(Palette, _settings.ColorHex);
        _colorBox.SelectedIndex = colorIdx >= 0 ? colorIdx + 1 : 0;
        _resModeBox.SelectedIndex = _settings.ResMode == "exactly" ? 1 : 0;
        var capIdx = Array.IndexOf(PageCaps, _settings.PageCap);
        _pageCapBox.SelectedIndex = capIdx >= 0 ? capIdx : 0;
        _keyBox.Text = _settings.ApiKey;
        _collectionBox.Items.Add("(tag search)");
        _collectionBox.SelectedIndex = 0;

        WallpaperSetter.SetFit(ParseFit());

        // ----- Theme -----
        CreateControl();
        Theme.Init(_settings.DarkMode);
        Theme.Apply(this);
        Theme.ApplyTabControlBorder(_tabs);
        UpdateThemeButton();
        UpdateCacheEnabled();
        UpdateCapEnabled();
        UpdateBoostEnabled();
        UpdateSolarEnabled();
        UpdateScheduleVisibility();

        // ----- Rotate tab mirrors the desktop: restore last-set wallpaper -----
        var lastSet = HistoryService.Entries.FirstOrDefault();
        if (lastSet is not null && File.Exists(lastSet.FilePath))
            ShowCurrent(lastSet.Id, lastSet.FilePath, lastSet.Resolution);

        // ----- Events -----
        _tagBox.TextChanged += (s, e) =>
        {
            if (_tagLocked)
            {
                if (_tagBox.Text != _settings.Tag) _tagBox.Text = _settings.Tag;
                return;
            }
            SaveSettings();
        };
        _tagBox.DropDown += (s, e) => { if (_tagLocked) _tagBox.DroppedDown = false; };
        _intervalBox.SelectedIndexChanged += (s, e) => { ApplyInterval(); SaveSettings(); };
        _orderBox.SelectedIndexChanged += (s, e) => SaveSettings();
        _fitBox.SelectedIndexChanged += (s, e) =>
        {
            WallpaperSetter.SetFit(ParseFit());
            SaveSettings();
            if (File.Exists(_currentPath)) WallpaperSetter.Set(_currentPath);
        };
        _pauseBox.CheckedChanged += (s, e) => SetPaused(_pauseBox.Checked);
        _fullscreenBox.CheckedChanged += (s, e) => SaveSettings();
        _autostartBox.CheckedChanged += (s, e) => SetAutostart(_autostartBox.Checked);
        _offlineBox.CheckedChanged += (s, e) => SaveSettings();
        _multiBox.CheckedChanged += (s, e) => SetMulti(_multiBox.Checked);
        _pruneBox.CheckedChanged += (s, e) => { SyncCacheSettings(); UpdateCacheEnabled(); SaveSettings(); };
        _maxFilesBox.ValueChanged += (s, e) =>
        {
            if (_cacheLocked) { _maxFilesBox.Value = Math.Clamp(_settings.CacheMaxFiles, (int)_maxFilesBox.Minimum, (int)_maxFilesBox.Maximum); return; }
            SyncCacheSettings(); SaveSettings();
        };
        _maxMbBox.ValueChanged += (s, e) =>
        {
            if (_cacheLocked) { _maxMbBox.Value = Math.Clamp((decimal)_settings.CacheMaxMb, _maxMbBox.Minimum, _maxMbBox.Maximum); return; }
            SyncCacheSettings(); SaveSettings();
        };
        _historyCapBox.CheckedChanged += (s, e) => { UpdateCapEnabled(); SyncHistoryCap(); SaveSettings(); };
        _historyCapValue.ValueChanged += (s, e) =>
        {
            if (_capLocked) { _historyCapValue.Value = Math.Clamp(_settings.HistoryCap, (int)_historyCapValue.Minimum, (int)_historyCapValue.Maximum); return; }
            SyncHistoryCap(); SaveSettings();
        };
        _boostBox.CheckedChanged += (s, e) => { UpdateBoostEnabled(); SaveSettings(); };
        _boostValue.ValueChanged += (s, e) =>
        {
            if (_boostLocked) { _boostValue.Value = Math.Clamp(_settings.FavoriteBoostPercent, (int)_boostValue.Minimum, (int)_boostValue.Maximum); return; }
            SaveSettings();
        };
        _matchScreenBox.CheckedChanged += (s, e) => { UpdateResEnabled(); SaveSettings(); };
        _minWBox.ValueChanged += (s, e) =>
        {
            if (_resLocked) { _minWBox.Value = Math.Clamp(_settings.MinWidth, (int)_minWBox.Minimum, (int)_minWBox.Maximum); return; }
            SaveSettings();
        };
        _minHBox.ValueChanged += (s, e) =>
        {
            if (_resLocked) { _minHBox.Value = Math.Clamp(_settings.MinHeight, (int)_minHBox.Minimum, (int)_minHBox.Maximum); return; }
            SaveSettings();
        };
        _scheduleBox.CheckedChanged += (s, e) => { UpdateTagBoxEnabled(); UpdateScheduleVisibility(); SaveSettings(); };
        _solarBox.CheckedChanged += (s, e) => { UpdateSolarEnabled(); UpdateScheduleLabels(); SaveSettings(); };
        _latBox.ValueChanged += (s, e) =>
        {
            if (_solarLocked) { _latBox.Value = Math.Clamp((decimal)_settings.Latitude, _latBox.Minimum, _latBox.Maximum); return; }
            UpdateScheduleLabels(); SaveSettings();
        };
        _lonBox.ValueChanged += (s, e) =>
        {
            if (_solarLocked) { _lonBox.Value = Math.Clamp((decimal)_settings.Longitude, _lonBox.Minimum, _lonBox.Maximum); return; }
            UpdateScheduleLabels(); SaveSettings();
        };
        foreach (var box in new[] { _morningBox, _afternoonBox, _eveningBox, _nightBox })
            box.TextChanged += (s, e) => SaveSettings();
        foreach (var cb in new[] { _generalBox, _animeBox, _peopleBox, _sketchyBox })
            cb.CheckedChanged += (s, e) => SaveSettings();
        _ratioBox.SelectedIndexChanged += (s, e) => SaveSettings();
        _colorBox.SelectedIndexChanged += (s, e) => SaveSettings();
        _resModeBox.SelectedIndexChanged += (s, e) => SaveSettings();
        _pageCapBox.SelectedIndexChanged += (s, e) => SaveSettings();
        _keyBox.TextChanged += (s, e) => { _settings.ApiKey = _keyBox.Text.Trim(); SettingsService.Save(_settings); };
        _collectionBox.SelectedIndexChanged += (s, e) =>
        {
            _settings.CollectionId = _collectionBox.SelectedIndex > 0 && _collections.Count > 0
                ? _collections[_collectionBox.SelectedIndex - 1].Id : 0;
            SettingsService.Save(_settings);
        };
        _themeBtn.Click += (s, e) =>
        {
            Theme.Toggle();
            Theme.Apply(this);
            _tabs.Invalidate(true);
            UpdateThemeButton();
            UpdateTagBoxEnabled();
            UpdateResEnabled();
            UpdateCacheEnabled();
            UpdateCapEnabled();
            UpdateBoostEnabled();
            UpdateSolarEnabled();
            UpdateFavoriteButton();
            UpdateBlockButton();
            SaveSettings();
            ActiveControl = null;
        };
        _viewBtn.Click += (s, e) => OpenWallhavenPage();
        _favBtn.Click += (s, e) => ToggleCurrentFavorite();
        _blockBtn.Click += (s, e) => BlockCurrent();

        _timer.Tick += async (s, e) =>
        {
            if (_paused) return;
            if (_fullscreenBox.Checked && FullscreenDetector.IsFullscreenApp()) return;
            await RotateSafeAsync();
        };
        ApplyInterval();

        Load += async (s, e) =>
        {
            LogService.Info("=== Wallpaper Rotator started ===");
            LogService.Info($"Settings: tag='{_settings.Tag}' interval={_settings.IntervalMinutes}m sorting={_settings.Sorting} fit={_settings.Fit} " +
                            $"pageCap={_settings.PageCap} historyCap={(_settings.HistoryCapEnabled ? _settings.HistoryCap.ToString() : "off")} " +
                            $"boost={(_settings.FavoriteBoostEnabled ? _settings.FavoriteBoostPercent + "%" : "off")} " +
                            $"solar={(_settings.SolarScheduleEnabled ? $"{_settings.Latitude},{_settings.Longitude}" : "off")} " +
                            $"cache={_settings.CacheMaxFiles}files/{_settings.CacheMaxMb}MB dark={_settings.DarkMode}");
            await RotateSafeAsync();
            _ = CheckForUpdatesAsync();
        };

        if (Environment.GetCommandLineArgs().Contains("--silent"))
            BeginInvoke(new Action(Hide));
    }

    // ---------- UI ----------
    private void BuildUi()
    {
        const int pad = 20;
        const int c2 = 500;
        const int cw = 220;

        // ================= LEFT COLUMN =================
        _preview.Size = new Size(440, 250);
        _preview.Location = new Point(pad, 20);
        _preview.SizeMode = PictureBoxSizeMode.Zoom;
        _preview.BorderStyle = BorderStyle.FixedSingle;
        _rotatePage.Controls.Add(_preview);

        _viewBtn.Text = "View in Wallhaven";
        _viewBtn.Location = new Point(pad, 280);
        _viewBtn.Size = new Size(150, 30);
        _viewBtn.Enabled = false;
        _rotatePage.Controls.Add(_viewBtn);

        _favBtn.Location = new Point(178, 280);
        _favBtn.Size = new Size(50, 30);
        _favBtn.Text = "☆";
        _favBtn.Font = new Font("Segoe UI", 12F);
        _favBtn.Enabled = false;
        _rotatePage.Controls.Add(_favBtn);

        _blockBtn.Location = new Point(234, 280);
        _blockBtn.Size = new Size(50, 30);
        _blockBtn.Text = "🚫";
        _blockBtn.Font = new Font("Segoe UI Emoji", 11F);
        _blockBtn.Enabled = false;
        _rotatePage.Controls.Add(_blockBtn);

        _currentLabel.Location = new Point(292, 284);
        _currentLabel.Size = new Size(168, 24);
        _currentLabel.TextAlign = ContentAlignment.MiddleLeft;
        _currentLabel.Text = "(no wallpaper yet)";
        _rotatePage.Controls.Add(_currentLabel);

        _nextBtn.Text = "Next now";
        _nextBtn.Tag = "accent";
        _nextBtn.Location = new Point(pad, 320);
        _nextBtn.Size = new Size(440, 36);
        _nextBtn.Click += async (s, e) => await RotateSafeAsync();
        _rotatePage.Controls.Add(_nextBtn);

        // Toggles: per-monitor top-left, auto-prune bottom-left, pause bottom-right
        _multiBox.Text = "Per-monitor"; _multiBox.Location = new Point(pad, 370); _multiBox.AutoSize = true;
        _autostartBox.Text = "Start with Windows"; _autostartBox.Location = new Point(pad, 396); _autostartBox.AutoSize = true;
        _pruneBox.Text = "Auto-prune cache"; _pruneBox.Location = new Point(pad, 422); _pruneBox.AutoSize = true;
        _fullscreenBox.Text = "Pause in fullscreen"; _fullscreenBox.Location = new Point(240, 370); _fullscreenBox.AutoSize = true;
        _offlineBox.Text = "Pause downloads"; _offlineBox.Location = new Point(240, 396); _offlineBox.AutoSize = true;
        _pauseBox.Text = "Pause rotation"; _pauseBox.Location = new Point(240, 422); _pauseBox.AutoSize = true;
        _rotatePage.Controls.AddRange(new Control[] { _pauseBox, _autostartBox, _multiBox, _fullscreenBox, _offlineBox, _pruneBox });

        _maxFilesBox.Location = new Point(pad, 450);
        _maxFilesBox.Width = 80;
        _maxFilesBox.Minimum = 2;
        _maxFilesBox.Maximum = 10000;
        _maxFilesBox.Value = 200;
        _rotatePage.Controls.Add(_maxFilesBox);
        _filesOrLabel.Text = "files  or";
        _filesOrLabel.Location = new Point(106, 453);
        _filesOrLabel.AutoSize = true;
        _rotatePage.Controls.Add(_filesOrLabel);
        _maxMbBox.Location = new Point(162, 450);
        _maxMbBox.Width = 96;
        _maxMbBox.Minimum = 50;
        _maxMbBox.Maximum = 102400;
        _maxMbBox.Increment = 50;
        _maxMbBox.Value = 2048;
        _rotatePage.Controls.Add(_maxMbBox);
        _mbLabel.Text = "MB";
        _mbLabel.Location = new Point(264, 453);
        _mbLabel.AutoSize = true;
        _rotatePage.Controls.Add(_mbLabel);

        _historyCapBox.Text = "History cap";
        _historyCapBox.Location = new Point(pad, 484);
        _historyCapBox.AutoSize = true;
        _rotatePage.Controls.Add(_historyCapBox);
        _historyCapValue.Location = new Point(150, 482);
        _historyCapValue.Width = 90;
        _historyCapValue.Minimum = 10;
        _historyCapValue.Maximum = 100000;
        _historyCapValue.Increment = 50;
        _historyCapValue.Value = 500;
        _rotatePage.Controls.Add(_historyCapValue);

        _boostBox.Text = "Favorite boost";
        _boostBox.Location = new Point(pad, 514);
        _boostBox.AutoSize = true;
        _rotatePage.Controls.Add(_boostBox);
        _boostValue.Location = new Point(150, 512);
        _boostValue.Width = 60;
        _boostValue.Minimum = 1;
        _boostValue.Maximum = 100;
        _boostValue.Value = 30;
        _rotatePage.Controls.Add(_boostValue);
        _pctLabel.Text = "%";
        _pctLabel.Location = new Point(215, 515);
        _pctLabel.AutoSize = true;
        _rotatePage.Controls.Add(_pctLabel);

        _scheduleBox.Text = "Time-of-day schedule:";
        _scheduleBox.Location = new Point(pad, 544);
        _scheduleBox.AutoSize = true;
        _rotatePage.Controls.Add(_scheduleBox);

        _solarBox.Text = "Use sunrise/sunset";
        _solarBox.Location = new Point(pad, 570);
        _solarBox.AutoSize = true;
        _rotatePage.Controls.Add(_solarBox);
        var latLabel = new Label { Text = "Lat", Location = new Point(170, 573), AutoSize = true };
        var lonLabel = new Label { Text = "Lon", Location = new Point(280, 573), AutoSize = true };
        _latBox.Location = new Point(195, 570);
        _latBox.Width = 65;
        _latBox.Minimum = -90;
        _latBox.Maximum = 90;
        _latBox.DecimalPlaces = 2;
        _latBox.Increment = 0.25m;
        _rotatePage.Controls.Add(_latBox);
        _lonBox.Location = new Point(310, 570);
        _lonBox.Width = 65;
        _lonBox.Minimum = -180;
        _lonBox.Maximum = 180;
        _lonBox.DecimalPlaces = 2;
        _lonBox.Increment = 0.25m;
        _rotatePage.Controls.Add(_lonBox);

        _morningLabel.AutoSize = true;
        _morningLabel.Location = new Point(pad, 596);
        _afternoonLabel.AutoSize = true;
        _afternoonLabel.Location = new Point(240, 596);
        _morningBox.Location = new Point(pad, 618); _morningBox.Width = 210;
        _afternoonBox.Location = new Point(240, 618); _afternoonBox.Width = 210;
        _eveningLabel.AutoSize = true;
        _eveningLabel.Location = new Point(pad, 652);
        _nightLabel.AutoSize = true;
        _nightLabel.Location = new Point(240, 652);
        _eveningBox.Location = new Point(pad, 674); _eveningBox.Width = 210;
        _nightBox.Location = new Point(240, 674); _nightBox.Width = 210;
        _scheduleControls.AddRange(new Control[]
        {
            _solarBox, latLabel, _latBox, lonLabel, _lonBox,
            _morningLabel, _afternoonLabel, _morningBox, _afternoonBox,
            _eveningLabel, _nightLabel, _eveningBox, _nightBox
        });
        _rotatePage.Controls.AddRange(_scheduleControls.ToArray());

        _statusLabel.Location = new Point(pad, 710);
        _statusLabel.Size = new Size(440, 68);
        _rotatePage.Controls.Add(_statusLabel);

        // ================= RIGHT COLUMN =================
        _rotatePage.Controls.Add(new Label { Text = "Category", Location = new Point(c2, 20), AutoSize = true });
        _tagBox.Location = new Point(c2, 42);
        _tagBox.Width = 480;
        _tagBox.Items.AddRange(new object[] { "abstract", "nature", "cyberpunk", "minimalist", "space", "fantasy", "anime", "cars", "avengers" });
        _rotatePage.Controls.Add(_tagBox);

        _generalBox.Text = "General"; _generalBox.Location = new Point(c2, 74); _generalBox.AutoSize = true;
        _animeBox.Text = "Anime"; _animeBox.Location = new Point(c2 + 80, 74); _animeBox.AutoSize = true;
        _peopleBox.Text = "People"; _peopleBox.Location = new Point(c2 + 150, 74); _peopleBox.AutoSize = true;
        _sketchyBox.Text = "Sketchy"; _sketchyBox.Location = new Point(c2 + 220, 74); _sketchyBox.AutoSize = true;
        _rotatePage.Controls.AddRange(new Control[] { _generalBox, _animeBox, _peopleBox, _sketchyBox });

        _rotatePage.Controls.Add(new Label { Text = "Change every", Location = new Point(c2, 110), AutoSize = true });
        _intervalBox.Location = new Point(c2, 132);
        _intervalBox.Width = cw;
        _intervalBox.DropDownStyle = ComboBoxStyle.DropDownList;
        foreach (var i in _intervals) _intervalBox.Items.Add(i.Label);
        _rotatePage.Controls.Add(_intervalBox);

        _rotatePage.Controls.Add(new Label { Text = "Source order", Location = new Point(740, 110), AutoSize = true });
        _orderBox.Location = new Point(740, 132);
        _orderBox.Width = cw;
        _orderBox.DropDownStyle = ComboBoxStyle.DropDownList;
        _rotatePage.Controls.Add(_orderBox);

        // Resolution group: values + match mode + screen-match on one line
        _rotatePage.Controls.Add(new Label { Text = "Resolution", Location = new Point(c2, 170), AutoSize = true });
        _minWBox.Location = new Point(c2, 192);
        _minWBox.Width = 100;
        _minWBox.Minimum = 640; _minWBox.Maximum = 7680; _minWBox.Value = 1920;
        _rotatePage.Controls.Add(_minWBox);
        _minHBox.Location = new Point(608, 192);
        _minHBox.Width = 100;
        _minHBox.Minimum = 480; _minHBox.Maximum = 4320; _minHBox.Value = 1080;
        _rotatePage.Controls.Add(_minHBox);
        _resModeBox.Location = new Point(730, 192);
        _resModeBox.Width = 120;
        _resModeBox.DropDownStyle = ComboBoxStyle.DropDownList;
        _resModeBox.Items.Add("At least");
        _resModeBox.Items.Add("Exactly");
        _rotatePage.Controls.Add(_resModeBox);
        _matchScreenBox.Text = "Match my screen";
        _matchScreenBox.Location = new Point(860, 194);
        _matchScreenBox.AutoSize = true;
        _rotatePage.Controls.Add(_matchScreenBox);

        _rotatePage.Controls.Add(new Label { Text = "Ratio", Location = new Point(c2, 230), AutoSize = true });
        _ratioBox.Location = new Point(c2, 252);
        _ratioBox.Width = cw;
        _ratioBox.DropDownStyle = ComboBoxStyle.DropDownList;
        _ratioBox.Items.Add("Any ratio");
        for (int i = 1; i < Ratios.Length; i++) _ratioBox.Items.Add(Ratios[i].Replace('x', ':'));
        _rotatePage.Controls.Add(_ratioBox);

        _rotatePage.Controls.Add(new Label { Text = "Color", Location = new Point(740, 230), AutoSize = true });
        _colorBox.Location = new Point(740, 252);
        _colorBox.Width = cw;
        _colorBox.DropDownStyle = ComboBoxStyle.DropDownList;
        _colorBox.DrawMode = DrawMode.OwnerDrawFixed;
        _colorBox.Items.Add("Any color");
        foreach (var hex in Palette) _colorBox.Items.Add(hex);
        _colorBox.DrawItem += DrawColorItem;
        _rotatePage.Controls.Add(_colorBox);

        _rotatePage.Controls.Add(new Label { Text = "Fit", Location = new Point(c2, 290), AutoSize = true });
        _fitBox.Location = new Point(c2, 312);
        _fitBox.Width = cw;
        _fitBox.DropDownStyle = ComboBoxStyle.DropDownList;
        _rotatePage.Controls.Add(_fitBox);

        _rotatePage.Controls.Add(new Label { Text = "Page cap", Location = new Point(740, 290), AutoSize = true });
        _pageCapBox.Location = new Point(740, 312);
        _pageCapBox.Width = cw;
        _pageCapBox.DropDownStyle = ComboBoxStyle.DropDownList;
        foreach (var v in PageCaps) _pageCapBox.Items.Add($"{v} pages");
        _rotatePage.Controls.Add(_pageCapBox);

        _rotatePage.Controls.Add(new Label { Text = "Wallhaven API key", Location = new Point(c2, 350), AutoSize = true });
        _keyBox.Location = new Point(c2, 372);
        _keyBox.Width = 480;
        _keyBox.UseSystemPasswordChar = true;
        _rotatePage.Controls.Add(_keyBox);

        _rotatePage.Controls.Add(new Label { Text = "Collection", Location = new Point(c2, 410), AutoSize = true });
        _collectionBox.Location = new Point(c2, 432);
        _collectionBox.Width = 380;
        _collectionBox.DropDownStyle = ComboBoxStyle.DropDownList;
        _rotatePage.Controls.Add(_collectionBox);
        var loadBtn = new Button { Text = "Load", Location = new Point(890, 430), Size = new Size(90, 26) };
        loadBtn.Click += async (s, e) => await LoadCollectionsAsync();
        Tips.For(loadBtn, "Fetch your collection list from Wallhaven using the key above.");
        _rotatePage.Controls.Add(loadBtn);
        ApplyTooltips();
    }

    private void ApplyTooltips()
    {
        // ----- left column -----
        Tips.For(_preview, "Live preview of the wallpaper currently on your desktop.");
        Tips.For(_viewBtn, "Open the current wallpaper's Wallhaven page in your browser.");
        Tips.For(_favBtn, "Favorite / unfavorite the current wallpaper. Favorites survive pruning and trimming, and can win the boost dice.");
        Tips.For(_blockBtn, "Block / unblock the current wallpaper. Blocked wallpapers are never picked again (see Blocked tab).");
        Tips.For(_currentLabel, "Wallhaven ID and resolution of the wallpaper currently on your desktop.");
        Tips.For(_nextBtn, "Rotate to a new wallpaper immediately, without waiting for the timer.");
        Tips.For(_multiBox, "Give each connected monitor its own different wallpaper.");
        Tips.For(_autostartBox, "Launch Wallpaper Rotator hidden when Windows starts.");
        Tips.For(_pruneBox, "Auto-delete oldest cached wallpapers when the caps below are exceeded. Favorites are never deleted.");
        Tips.For(_fullscreenBox, "Skip rotations while a fullscreen game, video or F11 browser is in focus.");
        Tips.For(_offlineBox, "Stop downloading; rotate only from wallpapers already in the cache.");
        Tips.For(_pauseBox, "Freeze rotation completely until unticked (tray menu works too).");
        Tips.For(_maxFilesBox, "Maximum number of wallpaper files kept in cache (needs auto-prune on).");
        Tips.For(_maxMbBox, "Maximum total cache size in MB (needs auto-prune on). Minimum 50 MB.");
        Tips.For(_historyCapBox, "Limit how many wallpapers the app remembers. Off = remember forever = never repeat a wallpaper.");
        Tips.For(_historyCapValue, "Memory size in wallpapers. Past this, oldest non-favorites are forgotten and may return.");
        Tips.For(_boostBox, "Roll a dice before each search: on a win, set a random cached favorite instead of fetching new wallpapers.");
        Tips.For(_boostValue, "Win chance for the favorite dice, in percent.");
        Tips.For(_scheduleBox, "Use four different tags by time of day instead of one tag all day.");
        Tips.For(_solarBox, "Compute the four slots from real sunrise/sunset at your coordinates instead of fixed clock hours.");
        Tips.For(_latBox, "Your latitude in degrees (-90 to 90) for the sunrise/sunset math.");
        Tips.For(_lonBox, "Your longitude in degrees (-180 to 180) for the sunrise/sunset math.");
        Tips.For(_morningBox, "Tag for the morning slot (sunrise–noon, or 06–12 without solar).");
        Tips.For(_afternoonBox, "Tag for the afternoon slot (noon–sunset, or 12–18 without solar).");
        Tips.For(_eveningBox, "Tag for the evening slot (sunset–+4h, or 18–24 without solar).");
        Tips.For(_nightBox, "Tag for the night slot (the remaining hours).");

        // ----- right column -----
        Tips.For(_tagBox, "Search tag(s) — anything Wallhaven accepts, e.g. 'minimalist' or 'cyberpunk city'. Locked while the schedule is on.");
        Tips.For(_generalBox, "Include General-category wallpapers.");
        Tips.For(_animeBox, "Include Anime-category wallpapers.");
        Tips.For(_peopleBox, "Include People-category wallpapers.");
        Tips.For(_sketchyBox, "Also allow 'Sketchy' purity uploads (borderline content). Keep off for strictly safe results.");
        Tips.For(_intervalBox, "How long each wallpaper stays on screen before the next rotation.");
        Tips.For(_orderBox, "Ranking before sampling: Favorites/Toplist = community-approved, Date added = fresh, Views/Hot = popular, Random = variety, Relevance = best tag match.");
        Tips.For(_minWBox, "Minimum (or exact) wallpaper width in pixels.");
        Tips.For(_minHBox, "Minimum (or exact) wallpaper height in pixels.");
        Tips.For(_resModeBox, "'At least' accepts this size or bigger; 'Exactly' accepts only this exact resolution.");
        Tips.For(_matchScreenBox, "Fill the resolution boxes with your primary monitor's size and keep them locked to it.");
        Tips.For(_ratioBox, "Accept only this aspect ratio — match your monitor (16:9, 21:9…) to avoid odd cropping.");
        Tips.For(_colorBox, "Accept only wallpapers containing this exact palette color. Some tag+color combos genuinely have zero results.");
        Tips.For(_fitBox, "Windows scaling mode. Fill = cover the screen (recommended); Span = stretch across all monitors.");
        Tips.For(_pageCapBox, "How deep into Wallhaven's ranked pages the app may sample. Page 1 = best matches; deeper = more variety, lower average quality.");
        Tips.For(_keyBox, "Your Wallhaven API key (wallhaven.cc → Settings → API). Only needed for collections.");
        Tips.For(_collectionBox, "Rotate from a saved Wallhaven collection instead of tag search (needs API key).");
        Tips.For(_themeBtn, "Switch between dark and light theme.");
    }
    private void DrawTabItem(object? sender, DrawItemEventArgs e)
    {
        if (e.Index < 0) return;
        bool selected = e.Index == _tabs.SelectedIndex;
        using var bg = new SolidBrush(selected ? Theme.ControlBack : Theme.Back);
        e.Graphics.FillRectangle(bg, e.Bounds);
        var textColor = selected ? Theme.Fore : Theme.DisabledFore;
        TextRenderer.DrawText(e.Graphics, _tabs.TabPages[e.Index].Text, e.Font, e.Bounds, textColor,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        if (selected)
        {
            using var accent = new Pen(Theme.Accent, 3);
            e.Graphics.DrawLine(accent, e.Bounds.Left + 8, e.Bounds.Bottom - 2, e.Bounds.Right - 8, e.Bounds.Bottom - 2);
        }
    }

    private void DrawColorItem(object? sender, DrawItemEventArgs e)
    {
        if (e.Index < 0) return;
        e.DrawBackground();
        var text = (string)_colorBox.Items[e.Index];
        if (e.Index == 0)
        {
            TextRenderer.DrawText(e.Graphics, text, e.Font, e.Bounds, Theme.Fore,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
        }
        else
        {
            var swatch = new Rectangle(e.Bounds.X + 2, e.Bounds.Y + 2, 24, e.Bounds.Height - 4);
            using (var brush = new SolidBrush(ColorTranslator.FromHtml("#" + text)))
                e.Graphics.FillRectangle(brush, swatch);
            e.Graphics.DrawRectangle(Pens.Gray, swatch);
            TextRenderer.DrawText(e.Graphics, "#" + text, e.Font,
                new Rectangle(e.Bounds.X + 32, e.Bounds.Y, e.Bounds.Width - 32, e.Bounds.Height),
                Theme.Fore, TextFormatFlags.VerticalCenter);
        }
        e.DrawFocusRectangle();
    }

    private async Task LoadCollectionsAsync()
    {
        _statusLabel.Text = "Loading collections…";
        try
        {
            var client = new WallhavenClient { ApiKey = _keyBox.Text.Trim() };
            var list = await client.ListCollectionsAsync();
            _collections.Clear();
            _collectionBox.Items.Clear();
            _collectionBox.Items.Add("(tag search)");
            foreach (var c in list)
            {
                _collections.Add((c.Id, c.Label));
                _collectionBox.Items.Add($"{c.Label} ({c.Count})");
            }
            _collectionBox.SelectedIndex = 0;
            _statusLabel.Text = list.Count > 0 ? $"Loaded {list.Count} collection(s)." : "No collections on this account.";
        }
        catch (Exception ex)
        {
            _statusLabel.Text = $"Could not load collections: {ex.Message}";
        }
    }

    private void BuildTray()
    {
        _trayIcon = new NotifyIcon { Icon = _appIcon ?? SystemIcons.Application, Visible = true, Text = "Wallpaper Rotator" };
        _pauseItem = new ToolStripMenuItem("Pause rotation") { CheckOnClick = true };
        _pauseItem.Click += (s, e) => SetPaused(_pauseItem.Checked);
        _multiItem = new ToolStripMenuItem("Per-monitor wallpapers")
        {
            CheckOnClick = true,
            Checked = _settings.MultiMonitor
        };
        _multiItem.Click += (s, e) => SetMulti(_multiItem.Checked);

        var menu = new ContextMenuStrip();
        menu.Items.Add("Show window", null, (s, e) => ShowWindow());
        menu.Items.Add("Next wallpaper now", null, async (s, e) => await RotateSafeAsync());
        menu.Items.Add(_pauseItem);
        menu.Items.Add(_multiItem);
        menu.Items.Add("Cache", null, (s, e) => { ShowWindow(); _tabs.SelectedIndex = 1; });
        menu.Items.Add("Favorites", null, (s, e) => { ShowWindow(); _tabs.SelectedIndex = 2; });
        menu.Items.Add("History", null, (s, e) => { ShowWindow(); _tabs.SelectedIndex = 3; });
        menu.Items.Add("Blocked", null, (s, e) => { ShowWindow(); _tabs.SelectedIndex = 4; });
        menu.Items.Add("Logs", null, (s, e) => { ShowWindow(); _tabs.SelectedIndex = 5; });
        menu.Items.Add("Open log", null, (s, e) =>
            Process.Start(new ProcessStartInfo(LogService.LogFilePath) { UseShellExecute = true }));
        menu.Items.Add("Check for updates", null, async (s, e) => await CheckForUpdatesAsync(force: true));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit", null, (s, e) => ExitApp());
        _trayIcon.ContextMenuStrip = menu;
        _trayIcon.DoubleClick += (s, e) => ShowWindow();
    }

    private static void SelectCombo(ComboBox box, IEnumerable<string> items, string selected)
    {
        box.Items.Clear();
        box.Items.AddRange(items.Cast<object>().ToArray());
        var idx = box.Items.IndexOf(selected);
        box.SelectedIndex = idx >= 0 ? idx : 0;
    }

    private void ShowWindow()
    {
        Show();
        WindowState = FormWindowState.Normal;
        Activate();
    }

    private void SetPaused(bool paused)
    {
        _paused = paused;
        if (_pauseBox.Checked != paused) _pauseBox.Checked = paused;
        if (_pauseItem.Checked != paused) _pauseItem.Checked = paused;
        _statusLabel.Text = paused ? "Rotation paused." : $"Rotation resumed — next in {_intervalBox.Text}.";
    }

    private void SetMulti(bool multi)
    {
        _settings.MultiMonitor = multi;
        if (_multiBox.Checked != multi) _multiBox.Checked = multi;
        if (_multiItem.Checked != multi) _multiItem.Checked = multi;
        SettingsService.Save(_settings);
    }

    private void UpdateCacheEnabled()
    {
        _cacheLocked = !_pruneBox.Checked;
        _maxFilesBox.ForeColor = _cacheLocked ? Theme.DisabledFore : Theme.Fore;
        _maxMbBox.ForeColor = _cacheLocked ? Theme.DisabledFore : Theme.Fore;
        _filesOrLabel.Enabled = !_cacheLocked;
        _mbLabel.Enabled = !_cacheLocked;
    }

    private void UpdateCapEnabled()
    {
        _capLocked = !_historyCapBox.Checked;
        _historyCapValue.ForeColor = _capLocked ? Theme.DisabledFore : Theme.Fore;
    }

    private void UpdateBoostEnabled()
    {
        _boostLocked = !_boostBox.Checked;
        _boostValue.ForeColor = _boostLocked ? Theme.DisabledFore : Theme.Fore;
    }

    private void UpdateSolarEnabled()
    {
        _solarLocked = !_solarBox.Checked;
        _latBox.ForeColor = _solarLocked ? Theme.DisabledFore : Theme.Fore;
        _lonBox.ForeColor = _solarLocked ? Theme.DisabledFore : Theme.Fore;
    }

    private void UpdateScheduleLabels()
    {
        if (!_solarBox.Checked)
        {
            _morningLabel.Text = "Morning 6–12";
            _afternoonLabel.Text = "Afternoon 12–18";
            _eveningLabel.Text = "Evening 18–24";
            _nightLabel.Text = "Night 0–6";
            return;
        }
        var (sunrise, sunset) = SolarTimes.ForDate(DateTime.Now, (double)_latBox.Value, (double)_lonBox.Value);
        var noon = sunrise + (sunset - sunrise) / 2;
        var eveningEnd = sunset + TimeSpan.FromHours(4);
        _morningLabel.Text = $"Morning {sunrise:HH:mm}–{noon:HH:mm}";
        _afternoonLabel.Text = $"Afternoon {noon:HH:mm}–{sunset:HH:mm}";
        _eveningLabel.Text = $"Evening {sunset:HH:mm}–{eveningEnd:HH:mm}";
        _nightLabel.Text = $"Night {eveningEnd:HH:mm}–{sunrise:HH:mm}";
    }

    private void SyncHistoryCap()
    {
        HistoryService.Cap = _historyCapBox.Checked ? (int)_historyCapValue.Value : null;
        HistoryService.TrimToCap();
        HistoryService.Save();
    }

    private void UpdateScheduleVisibility()
    {
        bool show = _scheduleBox.Checked;
        foreach (var c in _scheduleControls) c.Visible = show;

        int statusY = show ? 710 : 574;
        _statusLabel.Location = new Point(20, statusY);
        ClientSize = new Size(1000, statusY + 88);
    }

    private void UpdateThemeButton()
    {
        _themeBtn.Invalidate();
    }

    private void OpenWallhavenPage()
    {
        if (string.IsNullOrEmpty(_currentId)) return;
        Process.Start(new ProcessStartInfo($"https://wallhaven.cc/w/{_currentId}") { UseShellExecute = true });
    }

    // ---------- "currently on desktop" mirror ----------
    private void ShowCurrent(string id, string path, string resolution)
    {
        _currentId = id;
        _currentPath = path;
        _currentRes = resolution;
        _viewBtn.Enabled = true;
        _favBtn.Enabled = true;
        _blockBtn.Enabled = true;
        _currentLabel.Text = $"{id}   ({resolution})";

        if (File.Exists(path))
        {
            using var loaded = Image.FromFile(path);
            var old = _preview.Image;
            _preview.Image = new Bitmap(loaded);
            old?.Dispose();
        }
        UpdateFavoriteButton();
        UpdateBlockButton();
    }

    private void UpdateFavoriteButton()
    {
        bool fav = !string.IsNullOrEmpty(_currentId) && HistoryService.IsFavorite(_currentId);
        _favBtn.Text = fav ? "★" : "☆";
        _favBtn.ForeColor = fav ? Theme.Accent : Theme.Fore;
    }

    private void ToggleCurrentFavorite()
    {
        if (string.IsNullOrEmpty(_currentId)) return;
        var entry = HistoryService.Entries.FirstOrDefault(e => e.Id == _currentId);
        if (entry is null) { _statusLabel.Text = "Nothing to favorite yet."; return; }
        entry.Favorite = !entry.Favorite;
        HistoryService.Save();
        UpdateFavoriteButton();
        _statusLabel.Text = entry.Favorite ? $"{entry.Id} added to favorites." : $"{entry.Id} removed from favorites.";
    }

    private void UpdateBlockButton()
    {
        bool blocked = !string.IsNullOrEmpty(_currentId) && BlocklistService.IsBlocked(_currentId);
        _blockBtn.Text = blocked ? "✓" : "🚫";
        _blockBtn.ForeColor = blocked ? Theme.Accent : Theme.Fore;
    }

    private void BlockCurrent()
    {
        if (string.IsNullOrEmpty(_currentId)) return;
        if (BlocklistService.IsBlocked(_currentId))
        {
            BlocklistService.Remove(_currentId);
            _statusLabel.Text = $"{_currentId} unblocked.";
        }
        else
        {
            BlocklistService.Add(_currentId, _currentRes);
            _statusLabel.Text = $"{_currentId} blocked — never again.";
        }
    }

    // ---------- Rotation ----------
    private async Task RotateSafeAsync()
    {
        if (_rotating) return;      // a retry-in-progress rotation already owns the pipeline
        _rotating = true;
        _nextBtn.Enabled = false;
        _statusLabel.Text = _offlineBox.Checked ? "Picking from cache…" : "Fetching new wallpaper…";
        try
        {
            SyncEngineOptions();

            if (_offlineBox.Checked)
            {
                var cached = _engine.RotateFromCache(ParseFit());
                if (cached is null)
                {
                    _statusLabel.Text = "No cached wallpapers yet — untick \"Pause downloads\" first.";
                    return;
                }
                ApplyResult(cached, "  [cache]");
                SaveSettings();
                return;
            }

            // Favorite boost: dice roll first — win = set a random cached favorite
            if (_boostBox.Checked)
            {
                int roll = Random.Shared.Next(1, 101);
                LogService.Info($"Boost roll {roll}/100 vs {_settings.FavoriteBoostPercent}% -> {(roll <= (int)_boostValue.Value ? "favorite" : "search")}");
                if (roll <= (int)_boostValue.Value)
                {
                    var fav = _engine.RotateFromFavorites(ParseFit());
                    if (fav is not null)
                    {
                        ApplyResult(fav, "  [favorite boost]");
                        SaveSettings();
                        return;
                    }
                    // no cached favorites right now -> fall through to normal search
                }
            }

            if (_scheduleBox.Checked) UpdateScheduleLabels();   // keep solar boundaries fresh across days
            var tag = CurrentTag();
            var (minW, minH) = CurrentMinResolution();
            var query = new WallhavenQuery
            {
                Tag = tag,
                Width = minW,
                Height = minH,
                Exact = _settings.ResMode == "exactly",
                Sorting = CurrentSorting(),
                Categories = CategoryBits(),
                Purity = PurityBits(),
                Ratio = _settings.Ratio,
                ColorHex = _settings.ColorHex,
                PageCap = _settings.PageCap
            };
            try
            {
                var result = await _engine.RotateAsync(query, ParseFit());
                if (result is null)
                {
                    var active = new List<string>();
                    if (_settings.Ratio.Length > 0) active.Add("ratio " + _settings.Ratio.Replace('x', ':'));
                    if (_settings.ColorHex.Length > 0) active.Add("color #" + _settings.ColorHex);
                    active.Add(_settings.ResMode == "exactly" ? $"exactly {minW}×{minH}" : $"at least {minW}×{minH}");
                    if (!_settings.CatAnime && !_settings.CatPeople) active.Add("General only");
                    _statusLabel.Text = $"No wallpapers for \"{tag}\" matching: {string.Join(", ", active)}. Relax a filter or pick another color.";
                    return;
                }
                ApplyResult(result, "");
                SaveSettings();
            }
            catch (Exception netEx) when (IsNetworkError(netEx))
            {
                var fallback = _engine.RotateFromCache(ParseFit());
                if (fallback is null) throw;
                ApplyResult(fallback, "  [offline – using cache]");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Rotation failed: {ex.Message}");
            LogService.Error($"Rotation failed: {ex.Message}");
            var code = (int?)(ex as System.Net.Http.HttpRequestException)?.StatusCode;
            _statusLabel.Text = code.HasValue && code.Value >= 500
                ? $"Wallhaven looks down (HTTP {code.Value}). Will retry next rotation."
                : $"Error: {ex.Message}";
        }
        finally
        {
            _nextBtn.Enabled = true;
            _rotating = false;
        }
    }

    private void ApplyResult(RotationResult result, string suffix)
    {
        ShowCurrent(result.Wallpaper.Id, result.FilePath, result.Wallpaper.Resolution);

        var extra = result.ExtraCount > 0 ? $"  •  +{result.ExtraCount} more monitor(s)" : "";
        _statusLabel.Text = $"Changed at {DateTime.Now:HH:mm:ss}  •  next in {_intervalBox.Text}" + suffix + extra;
    }

    private static bool IsNetworkError(Exception ex)
    {
        for (var e = ex; e is not null; e = e.InnerException)
        {
            if (e is System.Net.Http.HttpRequestException ||
                e is System.Net.Sockets.SocketException ||
                e is TimeoutException ||
                e is TaskCanceledException)
                return true;
        }
        return false;
    }

    private void SyncEngineOptions()
    {
        _engine.MultiMonitor = _multiBox.Checked;
        _engine.CollectionId = string.IsNullOrEmpty(_settings.ApiKey) ? 0 : _settings.CollectionId;
        _engine.ApiKey = _settings.ApiKey;
    }

    private string CategoryBits()
    {
        var bits = $"{(_settings.CatGeneral ? 1 : 0)}{(_settings.CatAnime ? 1 : 0)}{(_settings.CatPeople ? 1 : 0)}";
        return bits == "000" ? "100" : bits;
    }

    private string PurityBits() => $"1{(_settings.PuritySketchy ? 1 : 0)}0";

    private string CurrentSorting() =>
        SortOrders.FirstOrDefault(s => s.Label == _orderBox.Text).Value ?? "random";

    private string CurrentTag()
    {
        if (!_scheduleBox.Checked) return _tagBox.Text.Trim();
        var fallback = _tagBox.Text.Trim();

        string tag;
        if (_solarBox.Checked)
        {
            var (sunrise, sunset) = SolarTimes.ForDate(DateTime.Now, (double)_latBox.Value, (double)_lonBox.Value);
            var noon = sunrise + (sunset - sunrise) / 2;
            var eveningEnd = sunset + TimeSpan.FromHours(4);
            var now = DateTime.Now;
            tag = (now >= sunrise && now < noon) ? _morningBox.Text.Trim()
                : (now >= noon && now < sunset) ? _afternoonBox.Text.Trim()
                : (now >= sunset && now < eveningEnd) ? _eveningBox.Text.Trim()
                : _nightBox.Text.Trim();
        }
        else
        {
            tag = DateTime.Now.Hour switch
            {
                >= 6 and < 12 => _morningBox.Text.Trim(),
                >= 12 and < 18 => _afternoonBox.Text.Trim(),
                >= 18 and < 24 => _eveningBox.Text.Trim(),
                _ => _nightBox.Text.Trim(),
            };
        }
        return tag.Length > 0 ? tag : fallback;
    }

    private (int Width, int Height) CurrentMinResolution()
    {
        if (_matchScreenBox.Checked)
        {
            var bounds = Screen.PrimaryScreen?.Bounds ?? new Rectangle(0, 0, 1920, 1080);
            return (bounds.Width, bounds.Height);
        }
        return ((int)_minWBox.Value, (int)_minHBox.Value);
    }

    private void UpdateResEnabled()
    {
        _resLocked = _matchScreenBox.Checked;
        _minWBox.ForeColor = _resLocked ? Theme.DisabledFore : Theme.Fore;
        _minHBox.ForeColor = _resLocked ? Theme.DisabledFore : Theme.Fore;
    }

    private WallpaperFit ParseFit() =>
        Enum.TryParse<WallpaperFit>(_fitBox.Text, out var f) ? f : WallpaperFit.Fill;

    private void UpdateTagBoxEnabled()
    {
        _tagLocked = _scheduleBox.Checked;

        if (_tagLocked)
        {
            var current = _settings.Tag;
            if (!string.IsNullOrEmpty(current) && !_tagBox.Items.Contains(current))
                _tagBox.Items.Add(current);
            _tagBox.DropDownStyle = ComboBoxStyle.DropDownList;
            _tagBox.SelectedItem = current;
            _tagBox.ForeColor = Theme.DisabledFore;
        }
        else
        {
            _tagBox.DropDownStyle = ComboBoxStyle.DropDown;
            _tagBox.Text = _settings.Tag;
            _tagBox.ForeColor = Theme.Fore;
        }

        _tagBox.BackColor = Theme.ControlBack;
    }

    // ---------- Settings ----------
    private void SaveSettings()
    {
        _settings.Tag = _tagBox.Text.Trim();
        if (_intervalBox.SelectedIndex >= 0)
            _settings.IntervalMinutes = _intervals[_intervalBox.SelectedIndex].Minutes;
        _settings.Sorting = CurrentSorting();
        _settings.Fit = _fitBox.Text;
        _settings.CachePruneEnabled = _pruneBox.Checked;
        _settings.CacheMaxFiles = (int)_maxFilesBox.Value;
        _settings.CacheMaxMb = (double)_maxMbBox.Value;
        _settings.HistoryCapEnabled = _historyCapBox.Checked;
        _settings.HistoryCap = (int)_historyCapValue.Value;
        _settings.FavoriteBoostEnabled = _boostBox.Checked;
        _settings.FavoriteBoostPercent = (int)_boostValue.Value;
        _settings.ResMatchScreen = _matchScreenBox.Checked;
        _settings.MinWidth = (int)_minWBox.Value;
        _settings.MinHeight = (int)_minHBox.Value;
        _settings.PauseOnFullscreen = _fullscreenBox.Checked;
        _settings.OfflineMode = _offlineBox.Checked;
        _settings.MultiMonitor = _multiBox.Checked;
        _settings.DarkMode = Theme.Dark;
        _settings.ScheduleEnabled = _scheduleBox.Checked;
        _settings.SolarScheduleEnabled = _solarBox.Checked;
        _settings.Latitude = (double)_latBox.Value;
        _settings.Longitude = (double)_lonBox.Value;
        _settings.MorningTag = _morningBox.Text.Trim();
        _settings.AfternoonTag = _afternoonBox.Text.Trim();
        _settings.EveningTag = _eveningBox.Text.Trim();
        _settings.NightTag = _nightBox.Text.Trim();
        _settings.CatGeneral = _generalBox.Checked;
        _settings.CatAnime = _animeBox.Checked;
        _settings.CatPeople = _peopleBox.Checked;
        _settings.PuritySketchy = _sketchyBox.Checked;
        _settings.Ratio = _ratioBox.SelectedIndex <= 0 ? "" : Ratios[_ratioBox.SelectedIndex];
        _settings.ColorHex = _colorBox.SelectedIndex <= 0 ? "" : Palette[_colorBox.SelectedIndex - 1];
        _settings.ResMode = _resModeBox.SelectedIndex == 1 ? "exactly" : "atleast";
        _settings.PageCap = PageCaps[_pageCapBox.SelectedIndex];
        SettingsService.Save(_settings);
    }

    private void SyncCacheSettings()
    {
        _engine.CachePruneEnabled = _pruneBox.Checked;
        _engine.CacheMaxFiles = (int)_maxFilesBox.Value;
        _engine.CacheMaxBytes = (long)(_settings.CacheMaxMb * 1024 * 1024);
    }

    private void SelectInterval(int minutes)
    {
        var idx = _intervals.FindIndex(i => i.Minutes == minutes);
        if (idx < 0)
        {
            _intervals.Insert(0, ($"{minutes} minutes (custom)", minutes));
            _intervalBox.Items.Clear();
            foreach (var i in _intervals) _intervalBox.Items.Add(i.Label);
            idx = 0;
        }
        _intervalBox.SelectedIndex = idx;
    }

    private void ApplyInterval()
    {
        if (_intervalBox.SelectedIndex < 0) return;
        _timer.Interval = _intervals[_intervalBox.SelectedIndex].Minutes * 60 * 1000;
        _timer.Start();
    }

    // ---------- Auto-start ----------
    private static bool IsAutostartEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, false);
        return key?.GetValue(RunName) is not null;
    }

    private void SetAutostart(bool enable)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, true);
        if (key is null) return;
        if (enable) key.SetValue(RunName, $"\"{Application.ExecutablePath}\" --silent");
        else key.DeleteValue(RunName, false);
    }

    // ---------- Update check ----------
    private async Task CheckForUpdatesAsync(bool force = false)
    {
        if (!_settings.UpdateChecksEnabled) return;

        if (!force)
        {
            await Task.Delay(TimeSpan.FromSeconds(15));   // let startup rotation settle first
            if ((DateTime.UtcNow - _settings.LastUpdateCheckUtc).TotalHours < 12) return;
        }

        var info = await UpdateChecker.CheckAsync();
        _settings.LastUpdateCheckUtc = DateTime.UtcNow;
        SettingsService.Save(_settings);
        LogService.Info($"Update check: latest={info?.Version ?? "none"} current={UpdateChecker.CurrentVersion}");

        if (info is null)
        {
            if (force)
                BeginInvoke(new Action(() => MessageBox.Show(this,
                    "You're on the latest version.", "Wallpaper Rotator",
                    MessageBoxButtons.OK, MessageBoxIcon.Information)));
            return;
        }
        if (info.Version == _settings.SkippedVersion && !force) return;

        BeginInvoke(new Action(() => ShowUpdateDialog(info)));
    }

    private void ShowUpdateDialog(UpdateInfo info)
    {
        var download = new TaskDialogButton("Download now");
        var later = new TaskDialogButton("Remind me later");
        var skip = new TaskDialogButton("Skip this version");

        var page = new TaskDialogPage
        {
            Caption = "Wallpaper Rotator",
            Heading = $"Version {info.Version} is available (you have {UpdateChecker.CurrentVersion}).",
            Text = string.IsNullOrWhiteSpace(info.Notes)
                ? "See the release page for what's new."
                : Truncate(info.Notes, 400),
            Icon = TaskDialogIcon.Information,
            SizeToContent = true,
            Buttons = { download, later, skip }
        };

        var result = TaskDialog.ShowDialog(page);
        if (result == download)
        {
            var url = info.DownloadUrl.Length > 0 ? info.DownloadUrl : info.ReleaseUrl;
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        else if (result == skip)
        {
            _settings.SkippedVersion = info.Version;
            SettingsService.Save(_settings);
        }
        // "Remind me later": nothing saved — the next 12-hour check asks again
    }

    private static string Truncate(string s, int max) =>
        s.Length <= max ? s : s[..max] + "…";

    // ---------- Window / tray ----------
    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            Hide();
        }
        else
        {
            _trayIcon.Visible = false;
            base.OnFormClosing(e);
        }
    }

    private void ExitApp()
    {
        _trayIcon.Visible = false;
        Application.Exit();
    }
}