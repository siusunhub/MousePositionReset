using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace MousePositionReset;

public partial class Form1 : Form
{
    private const string AppVersion = "v0.3";

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern bool DestroyIcon(IntPtr handle);

    // Monitor Item representation for dropdowns
    public class MonitorItem
    {
        public string DeviceName { get; set; } = "";
        public string ExactName { get; set; } = "";
        public int Width { get; set; }
        public int Height { get; set; }
        public string DisplayText { get; set; } = "";
        public override string ToString() => DisplayText;
    }

    private float _dpiScale = 1.0f;

    private int Scale(int value) => (int)(value * _dpiScale);
    private Point ScalePoint(int x, int y) => new Point((int)(x * _dpiScale), (int)(y * _dpiScale));
    private Size ScaleSize(int w, int h) => new Size((int)(w * _dpiScale), (int)(h * _dpiScale));

    private AppSettings _settings = new();
    private readonly List<ShortcutConfig> _tempShortcuts = new();
    private readonly HotKeyManager _hotKeyManager;
    
    private NotifyIcon _notifyIcon = null!;
    private Icon? _trayIcon;
    private IdentifyMonitorForm? _activeIdentifyMonitorForm;
    private bool _allowExit = false;
    private bool _isSettingStartupCheckbox = false;
    private bool _isSettingRunAsAdminCheckbox = false;
    
    private bool _isFirstShow = true;
    public bool StartMinimized = false;

    // Controls
    private FlowLayoutPanel flowLayoutPanelShortcuts = null!;
    private CheckBox chkRunAtStartup = null!;
    private CheckBox chkRunAsAdministrator = null!;
    private Button btnAddShortcut = null!;
    private Button btnAddCycle = null!;
    private Button btnSave = null!;

    public Form1()
    {
        InitializeComponent();
        this.Text = $"Mouse Position Reset {AppVersion}";
        
        // Calculate DPI scaling factor
        _dpiScale = this.DeviceDpi / 96f;
        
        // Setup Form properties programmatically for consistent modern look
        this.Size = ScaleSize(780, 520);
        this.MinimumSize = ScaleSize(780, 520);
        this.MaximumSize = ScaleSize(780, 520);
        this.FormBorderStyle = FormBorderStyle.FixedSingle;
        this.MaximizeBox = false;
        this.BackColor = Color.FromArgb(20, 20, 24);
        this.ForeColor = Color.White;
        this.StartPosition = FormStartPosition.CenterScreen;

        _hotKeyManager = new HotKeyManager();
        _hotKeyManager.HotKeyPressed += OnHotKeyTriggered;

        InitializeCustomComponents();
        InitializeTrayIcon();
        LoadConfiguration();
    }

    protected override void SetVisibleCore(bool value)
    {
        if (_isFirstShow && StartMinimized)
        {
            _isFirstShow = false;
            base.SetVisibleCore(false);
        }
        else
        {
            base.SetVisibleCore(value);
        }
    }

    private void InitializeCustomComponents()
    {
        // Header Panel
        var headerPanel = new Panel
        {
            Location = ScalePoint(0, 0),
            Size = ScaleSize(780, 65),
            BackColor = Color.FromArgb(15, 15, 18)
        };
        this.Controls.Add(headerPanel);

        var lblTitle = new Label
        {
            Text = "Mouse Position Reset",
            Location = ScalePoint(15, 18),
            AutoSize = true,
            Font = new Font("Segoe UI", 16F, FontStyle.Bold),
            ForeColor = Color.White
        };
        headerPanel.Controls.Add(lblTitle);

        // Flow Layout Panel for Shortcuts
        flowLayoutPanelShortcuts = new FlowLayoutPanel
        {
            Location = ScalePoint(15, 80),
            Size = ScaleSize(736, 310),
            AutoScroll = true,
            BackColor = Color.FromArgb(24, 24, 28)
        };
        flowLayoutPanelShortcuts.ClientSizeChanged += (s, e) => UpdateFormLayout();
        this.Controls.Add(flowLayoutPanelShortcuts);

        // Divider
        var divider = new Panel
        {
            Location = ScalePoint(15, 400),
            Size = ScaleSize(736, 1),
            BackColor = Color.FromArgb(48, 48, 54)
        };
        this.Controls.Add(divider);

        // Run At Startup Checkbox
        chkRunAtStartup = new CheckBox
        {
            Text = "Run at Startup",
            Location = ScalePoint(15, 420),
            AutoSize = true,
            Font = new Font("Segoe UI", 10F, FontStyle.Regular),
            ForeColor = Color.White,
            Cursor = Cursors.Hand
        };
        chkRunAtStartup.CheckedChanged += chkRunAtStartup_CheckedChanged;
        this.Controls.Add(chkRunAtStartup);

        // Run As Administrator Checkbox
        chkRunAsAdministrator = new CheckBox
        {
            Text = "Run as Admin",
            Location = ScalePoint(150, 420),
            AutoSize = true,
            Font = new Font("Segoe UI", 10F, FontStyle.Regular),
            ForeColor = Color.White,
            Cursor = Cursors.Hand
        };
        chkRunAsAdministrator.CheckedChanged += chkRunAsAdministrator_CheckedChanged;
        this.Controls.Add(chkRunAsAdministrator);

        // Add Shortcut Button
        btnAddShortcut = new Button
        {
            Text = "+ Add Shortcut",
            Location = ScalePoint(300, 415),
            Size = ScaleSize(130, 36),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(55, 65, 81),
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 10F, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        btnAddShortcut.FlatAppearance.BorderSize = 0;
        btnAddShortcut.Click += (s, e) => AddNewShortcutRow();
        this.Controls.Add(btnAddShortcut);

        // Add Cycle Button
        btnAddCycle = new Button
        {
            Text = "+ Add Cycle",
            Location = ScalePoint(440, 415),
            Size = ScaleSize(120, 36),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(55, 65, 81),
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 10F, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        btnAddCycle.FlatAppearance.BorderSize = 0;
        btnAddCycle.Click += (s, e) => AddNewCycleRow();
        this.Controls.Add(btnAddCycle);

        // Save & Activate Button
        btnSave = new Button
        {
            Text = "Save & Activate",
            Location = ScalePoint(570, 415),
            Size = ScaleSize(180, 36),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(99, 102, 241), // Indigo Accent
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 10F, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        btnSave.FlatAppearance.BorderSize = 0;
        btnSave.Click += (s, e) => SaveAndActivate();
        this.Controls.Add(btnSave);
    }

    private void InitializeTrayIcon()
    {
        _trayIcon = CreateDynamicIcon();
        
        _notifyIcon = new NotifyIcon
        {
            Icon = _trayIcon,
            Text = $"Mouse Position Reset {AppVersion}",
            Visible = true
        };

        var contextMenu = new ContextMenuStrip();
        var itemConfig = new ToolStripMenuItem("Configure Shortcuts...", null, (s, e) => ShowConfigForm());
        var itemExit = new ToolStripMenuItem("Exit", null, (s, e) => ExitApplication());

        contextMenu.Items.Add(itemConfig);
        contextMenu.Items.Add(new ToolStripSeparator());
        contextMenu.Items.Add(itemExit);

        _notifyIcon.ContextMenuStrip = contextMenu;
        _notifyIcon.DoubleClick += (s, e) => ShowConfigForm();
    }

    private Icon CreateDynamicIcon()
    {
        // Create an elegant Indigo target/reticle icon dynamically
        using var bitmap = new Bitmap(16, 16);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);
            
            using var pen = new Pen(Color.FromArgb(99, 102, 241), 2f);
            g.DrawEllipse(pen, 2, 2, 12, 12);
            
            using var centerPen = new Pen(Color.FromArgb(99, 102, 241), 1f);
            g.DrawLine(centerPen, 8, 4, 8, 12);
            g.DrawLine(centerPen, 4, 8, 12, 8);
        }
        IntPtr hIcon = bitmap.GetHicon();
        return Icon.FromHandle(hIcon);
    }

    private void LoadConfiguration()
    {
        _settings = AppSettings.Load();

        // Sync startup checkbox and registry
        bool registryEnabled = RegistryStartupManager.IsStartupEnabled();
        if (_settings.RunAtStartup != registryEnabled)
        {
            // Sync registry to config setting
            RegistryStartupManager.SetStartup(_settings.RunAtStartup);
        }

        _isSettingStartupCheckbox = true;
        chkRunAtStartup.Checked = _settings.RunAtStartup;
        _isSettingStartupCheckbox = false;

        _isSettingRunAsAdminCheckbox = true;
        chkRunAsAdministrator.Checked = _settings.RunAsAdministrator;
        _isSettingRunAsAdminCheckbox = false;

        // Initialize temp shortcuts list
        _tempShortcuts.Clear();
        foreach (var sc in _settings.Shortcuts)
        {
            _tempShortcuts.Add(new ShortcutConfig
            {
                Modifier = sc.Modifier,
                Key = sc.Key,
                MonitorName = sc.MonitorName,
                MonitorDeviceName = sc.MonitorDeviceName,
                MonitorExactName = sc.MonitorExactName,
                MonitorWidth = sc.MonitorWidth,
                MonitorHeight = sc.MonitorHeight,
                IsCenter = sc.IsCenter,
                X = sc.X,
                Y = sc.Y,
                IsCycle = false
            });
        }

        if (_settings.Cycle != null)
        {
            foreach (var cy in _settings.Cycle)
            {
                _tempShortcuts.Add(new ShortcutConfig
                {
                    Modifier = cy.Modifier,
                    Key = cy.Key,
                    IsCycle = true,
                    MonitorSequence = cy.MonitorSequence != null
                        ? cy.MonitorSequence.Select(m => new CycleMonitorInfo { MonitorName = m.MonitorName, MonitorDeviceName = m.MonitorDeviceName }).ToList()
                        : new List<CycleMonitorInfo>()
                });
            }
        }

        // Render UI rows
        flowLayoutPanelShortcuts.Controls.Clear();
        foreach (var sc in _tempShortcuts)
        {
            if (sc.IsCycle)
            {
                flowLayoutPanelShortcuts.Controls.Add(CreateCycleRowPanel(sc));
            }
            else
            {
                flowLayoutPanelShortcuts.Controls.Add(CreateShortcutRowPanel(sc));
            }
        }

        UpdateFormLayout();
        UpdateAddCycleButtonState();

        // Register hotkeys if we have any
        RegisterAllShortcuts();
    }

    private void UpdateAddCycleButtonState()
    {
        bool hasCycle = _tempShortcuts.Any(sc => sc.IsCycle);
        btnAddCycle.Enabled = !hasCycle;
        if (hasCycle)
        {
            btnAddCycle.BackColor = Color.FromArgb(37, 40, 48);
            btnAddCycle.ForeColor = Color.FromArgb(156, 163, 175);
        }
        else
        {
            btnAddCycle.BackColor = Color.FromArgb(55, 65, 81);
            btnAddCycle.ForeColor = Color.White;
        }
    }

    public class CycleDropdownItem
    {
        public CycleMonitorInfo MonitorInfo { get; set; } = new();
        public override string ToString() => MonitorInfo.MonitorName;
    }

    private void AddNewCycleRow()
    {
        if (_tempShortcuts.Any(sc => sc.IsCycle))
        {
            MessageBox.Show("Only one cycle setting is allowed.", "Limit Exceeded", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var activeMonitors = MonitorFriendlyNameHelper.GetActiveMonitors();
        var screens = Screen.AllScreens;
        var initialSeq = new List<CycleMonitorInfo>();
        for (int i = 0; i < screens.Length; i++)
        {
            var s = screens[i];
            var info = activeMonitors.FirstOrDefault(m => string.Equals(m.DeviceName, s.DeviceName, StringComparison.OrdinalIgnoreCase));
            initialSeq.Add(new CycleMonitorInfo
            {
                MonitorName = info?.FriendlyName ?? $"Display {i + 1}",
                MonitorDeviceName = s.DeviceName
            });
        }

        var config = new ShortcutConfig
        {
            Modifier = "Ctrl",
            Key = "C",
            IsCycle = true,
            MonitorSequence = initialSeq
        };

        _tempShortcuts.Add(config);
        var rowPanel = CreateCycleRowPanel(config);
        flowLayoutPanelShortcuts.Controls.Add(rowPanel);
        UpdateFormLayout();
        flowLayoutPanelShortcuts.ScrollControlIntoView(rowPanel);
        UpdateAddCycleButtonState();
    }

    private Panel CreateCycleRowPanel(ShortcutConfig config)
    {
        var panel = new Panel
        {
            Size = new Size(flowLayoutPanelShortcuts.ClientSize.Width - Scale(10), Scale(90)),
            BackColor = Color.FromArgb(32, 32, 42),
            Margin = new Padding(0, 0, 0, Scale(8))
        };

        panel.Paint += (s, e) =>
        {
            using var pen = new Pen(Color.FromArgb(99, 102, 241), 1);
            e.Graphics.DrawRectangle(pen, 0, 0, panel.Width - 1, panel.Height - 1);
        };

        // Label: Cycle Hotkey
        var lblHotkey = new Label
        {
            Text = "Cycle Hotkey:",
            Location = ScalePoint(12, 16),
            AutoSize = true,
            Font = new Font("Segoe UI Semibold", 9.75F, FontStyle.Bold),
            ForeColor = Color.FromArgb(209, 213, 219)
        };
        panel.Controls.Add(lblHotkey);

        // Modifier ComboBox
        var cbMod = new ComboBox
        {
            Location = ScalePoint(115, 12),
            Size = ScaleSize(110, 25),
            DropDownStyle = ComboBoxStyle.DropDownList,
            BackColor = Color.FromArgb(48, 48, 54),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9.5F)
        };
        cbMod.Items.AddRange(new object[] { "CTRL", "CTRL + SHIFT" });
        cbMod.Text = config.Modifier.ToUpperInvariant();
        cbMod.SelectedIndexChanged += (s, e) => config.Modifier = cbMod.Text;
        panel.Controls.Add(cbMod);

        // Plus Label
        var lblPlus = new Label
        {
            Text = "+",
            Location = ScalePoint(230, 15),
            AutoSize = true,
            Font = new Font("Segoe UI Semibold", 10F, FontStyle.Bold),
            ForeColor = Color.FromArgb(209, 213, 219)
        };
        panel.Controls.Add(lblPlus);

        // Key ComboBox
        var cbKey = new ComboBox
        {
            Location = ScalePoint(250, 12),
            Size = ScaleSize(55, 25),
            DropDownStyle = ComboBoxStyle.DropDownList,
            BackColor = Color.FromArgb(48, 48, 54),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9.5F)
        };
        for (char c = 'A'; c <= 'Z'; c++) cbKey.Items.Add(c.ToString());
        for (char c = '0'; c <= '9'; c++) cbKey.Items.Add(c.ToString());
        cbKey.Text = config.Key.ToUpperInvariant();
        cbKey.SelectedIndexChanged += (s, e) => config.Key = cbKey.Text;
        panel.Controls.Add(cbKey);

        // Delete Button
        var btnDelete = new Button
        {
            Text = "✕",
            Location = new Point(panel.Width - Scale(45), Scale(11)),
            Size = ScaleSize(30, 26),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(48, 48, 54),
            ForeColor = Color.FromArgb(239, 68, 68),
            Font = new Font("Segoe UI Semibold", 9.5F, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        btnDelete.FlatAppearance.BorderSize = 0;
        btnDelete.Click += (s, e) =>
        {
            _tempShortcuts.Remove(config);
            flowLayoutPanelShortcuts.Controls.Remove(panel);
            UpdateFormLayout();
            UpdateAddCycleButtonState();
        };
        panel.Controls.Add(btnDelete);

        // Line 2: Monitors list label
        var lblMonitorsTitle = new Label
        {
            Text = "Monitors Cycle:",
            Location = ScalePoint(12, 54),
            AutoSize = true,
            Font = new Font("Segoe UI Semibold", 9.75F, FontStyle.Bold),
            ForeColor = Color.FromArgb(209, 213, 219)
        };
        panel.Controls.Add(lblMonitorsTitle);

        // Get monitors list
        var activeMonitors = MonitorFriendlyNameHelper.GetActiveMonitors();
        var screens = Screen.AllScreens;

        // Build list of active monitor items to populate the dropdowns
        var activeItems = new List<CycleDropdownItem>();
        activeItems.Add(new CycleDropdownItem
        {
            MonitorInfo = new CycleMonitorInfo
            {
                MonitorName = "[Disabled]",
                MonitorDeviceName = ""
            }
        });
        for (int i = 0; i < screens.Length; i++)
        {
            var s = screens[i];
            var info = activeMonitors.FirstOrDefault(m => string.Equals(m.DeviceName, s.DeviceName, StringComparison.OrdinalIgnoreCase));
            activeItems.Add(new CycleDropdownItem
            {
                MonitorInfo = new CycleMonitorInfo
                {
                    MonitorName = info?.FriendlyName ?? $"Display {i + 1}",
                    MonitorDeviceName = s.DeviceName
                }
            });
        }

        // Generate N pull-down menus
        var combos = new List<ComboBox>();
        int screensCount = screens.Length;
        int comboWidth = screensCount >= 5 ? Scale(85) : Scale(120);
        int labelWidth = Scale(18);
        int gap = Scale(10);
        int startX = Scale(120);

        void SaveSequenceFromDropdowns()
        {
            var seq = new List<CycleMonitorInfo>();
            foreach (var cb in combos)
            {
                if (cb.SelectedItem is CycleDropdownItem dropdownItem)
                {
                    seq.Add(new CycleMonitorInfo
                    {
                        MonitorName = dropdownItem.MonitorInfo.MonitorName,
                        MonitorDeviceName = dropdownItem.MonitorInfo.MonitorDeviceName
                    });
                }
            }
            config.MonitorSequence = seq;
        }

        for (int i = 0; i < screensCount; i++)
        {
            int index = i;
            
            // Label for the sequence index
            var lblSeq = new Label
            {
                Text = $"{i + 1}.",
                Location = new Point(startX + i * (labelWidth + comboWidth + gap), Scale(54)),
                Size = new Size(labelWidth, Scale(20)),
                Font = new Font("Segoe UI Semibold", 9.75F, FontStyle.Bold),
                ForeColor = Color.FromArgb(209, 213, 219),
                TextAlign = ContentAlignment.MiddleRight
            };
            panel.Controls.Add(lblSeq);

            // ComboBox for sequence index
            var cbSeq = new ComboBox
            {
                Location = new Point(lblSeq.Right + Scale(2), Scale(50)),
                Size = new Size(comboWidth, Scale(25)),
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Color.FromArgb(48, 48, 54),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9.5F)
            };

            cbSeq.Items.AddRange(activeItems.ToArray());
            combos.Add(cbSeq);

            // Initialize selection
            CycleDropdownItem? matchedItem = null;
            if (config.MonitorSequence != null && index < config.MonitorSequence.Count)
            {
                var savedInfo = config.MonitorSequence[index];
                matchedItem = activeItems.FirstOrDefault(item => 
                    string.Equals(item.MonitorInfo.MonitorName, savedInfo.MonitorName, StringComparison.OrdinalIgnoreCase));
                
                if (matchedItem == null)
                {
                    matchedItem = activeItems.FirstOrDefault(item => 
                        string.Equals(item.MonitorInfo.MonitorDeviceName, savedInfo.MonitorDeviceName, StringComparison.OrdinalIgnoreCase));
                }
            }

            if (matchedItem != null)
            {
                cbSeq.SelectedItem = matchedItem;
            }
            else if (cbSeq.Items.Count > 0)
            {
                cbSeq.SelectedIndex = Math.Min(index + 1, cbSeq.Items.Count - 1);
            }

            cbSeq.SelectedIndexChanged += (sender, e) => SaveSequenceFromDropdowns();
            panel.Controls.Add(cbSeq);
        }

        // Perform initial save of the selected sequence
        SaveSequenceFromDropdowns();

        return panel;
    }

    private void AddNewShortcutRow()
    {
        // Find a sensible default monitor name
        var primaryScreen = Screen.PrimaryScreen;
        var activeMonitors = MonitorFriendlyNameHelper.GetActiveMonitors();
        var primaryInfo = activeMonitors.FirstOrDefault(m => primaryScreen != null && string.Equals(m.DeviceName, primaryScreen.DeviceName, StringComparison.OrdinalIgnoreCase));
        string defaultMonitorDeviceName = primaryScreen?.DeviceName ?? "";
        string defaultExactName = primaryInfo?.FriendlyName ?? "";
        string defaultMonitor = defaultExactName;
        int defaultWidth = primaryInfo?.PhysicalWidth ?? primaryScreen?.Bounds.Width ?? 1920;
        int defaultHeight = primaryInfo?.PhysicalHeight ?? primaryScreen?.Bounds.Height ?? 1080;

        var config = new ShortcutConfig
        {
            Modifier = "Ctrl",
            Key = "A",
            MonitorName = defaultMonitor,
            MonitorDeviceName = defaultMonitorDeviceName,
            MonitorExactName = defaultExactName,
            MonitorWidth = defaultWidth,
            MonitorHeight = defaultHeight,
            IsCenter = true,
            X = 0,
            Y = 0
        };

        _tempShortcuts.Add(config);
        var rowPanel = CreateShortcutRowPanel(config);
        flowLayoutPanelShortcuts.Controls.Add(rowPanel);
        UpdateFormLayout();
        flowLayoutPanelShortcuts.ScrollControlIntoView(rowPanel);
    }

    private Panel CreateShortcutRowPanel(ShortcutConfig config)
    {
        var panel = new Panel
        {
            Size = new Size(flowLayoutPanelShortcuts.ClientSize.Width - Scale(10), Scale(90)),
            BackColor = Color.FromArgb(32, 32, 36),
            Margin = new Padding(0, 0, 0, Scale(8))
        };

        panel.Paint += (s, e) =>
        {
            using var pen = new Pen(Color.FromArgb(55, 65, 81), 1);
            e.Graphics.DrawRectangle(pen, 0, 0, panel.Width - 1, panel.Height - 1);
        };

        // Label: Hotkey
        var lblHotkey = new Label
        {
            Text = "Hotkey:",
            Location = ScalePoint(12, 16),
            AutoSize = true,
            Font = new Font("Segoe UI Semibold", 9.75F, FontStyle.Bold),
            ForeColor = Color.FromArgb(209, 213, 219)
        };
        panel.Controls.Add(lblHotkey);

        // Modifier ComboBox
        var cbMod = new ComboBox
        {
            Location = ScalePoint(72, 12),
            Size = ScaleSize(110, 25),
            DropDownStyle = ComboBoxStyle.DropDownList,
            BackColor = Color.FromArgb(48, 48, 54),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9.5F)
        };
        cbMod.Items.AddRange(new object[] { "CTRL", "CTRL + SHIFT" });
        cbMod.Text = config.Modifier.ToUpperInvariant();
        cbMod.SelectedIndexChanged += (s, e) => config.Modifier = cbMod.Text;
        panel.Controls.Add(cbMod);

        // Plus Label
        var lblPlus = new Label
        {
            Text = "+",
            Location = ScalePoint(187, 15),
            AutoSize = true,
            Font = new Font("Segoe UI Semibold", 10F, FontStyle.Bold),
            ForeColor = Color.FromArgb(209, 213, 219)
        };
        panel.Controls.Add(lblPlus);

        // Key ComboBox
        var cbKey = new ComboBox
        {
            Location = ScalePoint(207, 12),
            Size = ScaleSize(55, 25),
            DropDownStyle = ComboBoxStyle.DropDownList,
            BackColor = Color.FromArgb(48, 48, 54),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9.5F)
        };
        for (char c = 'A'; c <= 'Z'; c++) cbKey.Items.Add(c.ToString());
        for (char c = '0'; c <= '9'; c++) cbKey.Items.Add(c.ToString());
        cbKey.Text = config.Key.ToUpperInvariant();
        cbKey.SelectedIndexChanged += (s, e) => config.Key = cbKey.Text;
        panel.Controls.Add(cbKey);

        // Label: Monitor
        var lblMonitor = new Label
        {
            Text = "Monitor:",
            Location = ScalePoint(272, 16),
            AutoSize = true,
            Font = new Font("Segoe UI Semibold", 9.75F, FontStyle.Bold),
            ForeColor = Color.FromArgb(209, 213, 219)
        };
        panel.Controls.Add(lblMonitor);

        // Monitor ComboBox
        var cbMonitor = new ComboBox
        {
            Location = ScalePoint(342, 12),
            Size = ScaleSize(280, 25),
            DropDownStyle = ComboBoxStyle.DropDownList,
            BackColor = Color.FromArgb(48, 48, 54),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9.5F)
        };
        PopulateMonitors(cbMonitor, config);
        panel.Controls.Add(cbMonitor);

        var btnIdentifyMonitor = new Button
        {
            Text = "i",
            Location = ScalePoint(632, 11),
            Size = ScaleSize(28, 26),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(48, 48, 54),
            ForeColor = Color.FromArgb(129, 140, 248),
            Font = new Font("Segoe UI Semibold", 10F, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        btnIdentifyMonitor.FlatAppearance.BorderSize = 0;
        btnIdentifyMonitor.Click += (s, e) => ShowMonitorIdentifier(cbMonitor);
        panel.Controls.Add(btnIdentifyMonitor);

        // Delete Button
        var btnDelete = new Button
        {
            Text = "✕",
            Location = new Point(panel.Width - Scale(45), Scale(11)),
            Size = ScaleSize(30, 26),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(48, 48, 54),
            ForeColor = Color.FromArgb(239, 68, 68),
            Font = new Font("Segoe UI Semibold", 9.5F, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        btnDelete.FlatAppearance.BorderSize = 0;
        btnDelete.Click += (s, e) =>
        {
            _tempShortcuts.Remove(config);
            flowLayoutPanelShortcuts.Controls.Remove(panel);
            UpdateFormLayout();
        };
        panel.Controls.Add(btnDelete);

        // Line 2: Position Settings
        var lblPosition = new Label
        {
            Text = "Position:",
            Location = ScalePoint(12, 54),
            AutoSize = true,
            Font = new Font("Segoe UI Semibold", 9.75F, FontStyle.Bold),
            ForeColor = Color.FromArgb(209, 213, 219)
        };
        panel.Controls.Add(lblPosition);

        var rbCenter = new RadioButton
        {
            Text = "Center of Monitor",
            Location = ScalePoint(80, 52),
            AutoSize = true,
            Font = new Font("Segoe UI", 9.5F),
            ForeColor = Color.FromArgb(240, 240, 245),
            Checked = config.IsCenter
        };

        var rbSpecific = new RadioButton
        {
            Text = "Specific Position",
            Location = ScalePoint(215, 52),
            AutoSize = true,
            Font = new Font("Segoe UI", 9.5F),
            ForeColor = Color.FromArgb(240, 240, 245),
            Checked = !config.IsCenter
        };

        panel.Controls.Add(rbCenter);
        panel.Controls.Add(rbSpecific);

        // X and Y coords
        var lblX = new Label { Text = "X:", Location = ScalePoint(340, 54), AutoSize = true, Font = new Font("Segoe UI", 9.5F), ForeColor = Color.FromArgb(209, 213, 219) };
        var numX = new NumericUpDown
        {
            Location = ScalePoint(360, 50),
            Size = ScaleSize(60, 24),
            Minimum = 0,
            Maximum = 20000,
            Value = Math.Max(0, Math.Min(20000, config.X)),
            BackColor = Color.FromArgb(48, 48, 54),
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 9.5F)
        };

        var lblY = new Label { Text = "Y:", Location = ScalePoint(430, 54), AutoSize = true, Font = new Font("Segoe UI", 9.5F), ForeColor = Color.FromArgb(209, 213, 219) };
        var numY = new NumericUpDown
        {
            Location = ScalePoint(450, 50),
            Size = ScaleSize(60, 24),
            Minimum = 0,
            Maximum = 20000,
            Value = Math.Max(0, Math.Min(20000, config.Y)),
            BackColor = Color.FromArgb(48, 48, 54),
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 9.5F)
        };

        var btnDeclare = new Button
        {
            Text = "Declare [3s]",
            Location = ScalePoint(520, 48),
            Size = ScaleSize(100, 28),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(99, 102, 241), // Indigo
            ForeColor = Color.White,
            Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        btnDeclare.FlatAppearance.BorderSize = 0;

        panel.Controls.Add(lblX);
        panel.Controls.Add(numX);
        panel.Controls.Add(lblY);
        panel.Controls.Add(numY);
        panel.Controls.Add(btnDeclare);

        void UpdateControlsState()
        {
            bool isSpecific = rbSpecific.Checked;
            lblX.Enabled = isSpecific;
            numX.Enabled = isSpecific;
            lblY.Enabled = isSpecific;
            numY.Enabled = isSpecific;
            btnDeclare.Enabled = isSpecific;
        }

        rbCenter.CheckedChanged += (s, e) =>
        {
            config.IsCenter = rbCenter.Checked;
            UpdateControlsState();
        };

        rbSpecific.CheckedChanged += (s, e) =>
        {
            config.IsCenter = !rbSpecific.Checked;
            UpdateControlsState();
        };

        numX.ValueChanged += (s, e) => config.X = (int)numX.Value;
        numY.ValueChanged += (s, e) => config.Y = (int)numY.Value;

        btnDeclare.Click += (s, e) =>
        {
            this.Hide();
            
            using (var cdf = new CountdownForm())
            {
                if (cdf.ShowDialog() == DialogResult.OK)
                {
                    var screen = cdf.CapturedScreen;
                    config.IsCenter = false;
                    
                    var activeMonitors = MonitorFriendlyNameHelper.GetActiveMonitors();
                    var info = activeMonitors.FirstOrDefault(m => string.Equals(m.DeviceName, screen.DeviceName, StringComparison.OrdinalIgnoreCase));
                    
                    config.MonitorName = info?.FriendlyName ?? screen.DeviceName;
                    config.MonitorDeviceName = screen.DeviceName;
                    config.MonitorExactName = config.MonitorName;
                    config.MonitorWidth = info?.PhysicalWidth ?? screen.Bounds.Width;
                    config.MonitorHeight = info?.PhysicalHeight ?? screen.Bounds.Height;
                    config.X = cdf.CapturedPosition.X - screen.Bounds.X;
                    config.Y = cdf.CapturedPosition.Y - screen.Bounds.Y;

                    rbSpecific.Checked = true;
                    numX.Value = config.X;
                    numY.Value = config.Y;

                    PopulateMonitors(cbMonitor, config);
                }
            }
            
            this.Show();
            this.BringToFront();
        };

        UpdateControlsState();

        return panel;
    }

    private void PopulateMonitors(ComboBox cbMonitor, ShortcutConfig config)
    {
        cbMonitor.Items.Clear();

        var activeMonitors = MonitorFriendlyNameHelper.GetActiveMonitors();
        var screens = Screen.AllScreens;
        MonitorItem? selectedItem = null;
        bool foundInActive = false;

        for (int i = 0; i < screens.Length; i++)
        {
            var s = screens[i];
            var info = activeMonitors.FirstOrDefault(m => string.Equals(m.DeviceName, s.DeviceName, StringComparison.OrdinalIgnoreCase));
            
            string friendlyName = info?.FriendlyName ?? $"Display {i + 1}";
            int width = info?.PhysicalWidth ?? s.Bounds.Width;
            int height = info?.PhysicalHeight ?? s.Bounds.Height;
            
            var item = new MonitorItem
            {
                DeviceName = s.DeviceName,
                ExactName = friendlyName,
                Width = width,
                Height = height,
                DisplayText = $"{friendlyName}{(s.Primary ? " (Primary)" : "")} - {width}x{height}"
            };
            cbMonitor.Items.Add(item);

            if (IsConfiguredMonitorMatch(config, item))
            {
                selectedItem = item;
                foundInActive = true;
            }
        }

        if (!foundInActive && (!string.IsNullOrEmpty(config.MonitorName) || !string.IsNullOrEmpty(config.MonitorDeviceName)))
        {
            selectedItem = new MonitorItem
            {
                DeviceName = !string.IsNullOrWhiteSpace(config.MonitorDeviceName) ? config.MonitorDeviceName : config.MonitorName,
                ExactName = GetConfiguredMonitorExactName(config),
                Width = config.MonitorWidth,
                Height = config.MonitorHeight,
                DisplayText = $"{GetSavedMonitorDisplayName(config)} [Disconnected] - {config.MonitorWidth}x{config.MonitorHeight}"
            };
            cbMonitor.Items.Add(selectedItem);
        }

        if (selectedItem != null)
        {
            cbMonitor.SelectedItem = selectedItem;
            ApplyMonitorItemToConfig(config, selectedItem);
        }
        else if (cbMonitor.Items.Count > 0 && cbMonitor.Items[0] is MonitorItem firstItem)
        {
            cbMonitor.SelectedIndex = 0;
            ApplyMonitorItemToConfig(config, firstItem);
        }

        // Setup Selection Change Event
        cbMonitor.SelectedIndexChanged += (s, e) =>
        {
            if (cbMonitor.SelectedItem is MonitorItem item)
            {
                ApplyMonitorItemToConfig(config, item);
            }
        };
    }

    private static bool IsConfiguredMonitorMatch(ShortcutConfig config, MonitorItem item)
    {
        string exactName = GetConfiguredMonitorExactName(config);
        if (!string.IsNullOrWhiteSpace(exactName))
        {
            return string.Equals(item.ExactName, exactName, StringComparison.OrdinalIgnoreCase);
        }

        string deviceName = !string.IsNullOrWhiteSpace(config.MonitorDeviceName) ? config.MonitorDeviceName : config.MonitorName;
        return string.Equals(item.DeviceName, deviceName, StringComparison.OrdinalIgnoreCase);
    }

    private static void ApplyMonitorItemToConfig(ShortcutConfig config, MonitorItem item)
    {
        config.MonitorName = item.ExactName;
        config.MonitorDeviceName = item.DeviceName;
        config.MonitorExactName = item.ExactName;
        config.MonitorWidth = item.Width;
        config.MonitorHeight = item.Height;
    }

    private static string GetSavedMonitorDisplayName(ShortcutConfig config)
    {
        string exactName = GetConfiguredMonitorExactName(config);
        if (!string.IsNullOrWhiteSpace(exactName))
        {
            return exactName;
        }

        return "Display";
    }

    private static string GetConfiguredMonitorExactName(ShortcutConfig config)
    {
        if (!LooksLikeDisplayDeviceName(config.MonitorName))
        {
            return config.MonitorName;
        }

        return config.MonitorExactName;
    }

    private static bool LooksLikeDisplayDeviceName(string value)
    {
        return value.StartsWith(@"\\.\DISPLAY", StringComparison.OrdinalIgnoreCase);
    }

    private void ShowMonitorIdentifier(ComboBox cbMonitor)
    {
        if (cbMonitor.SelectedItem is not MonitorItem item)
        {
            return;
        }

        var screen = FindScreenByDeviceName(item.DeviceName);
        if (screen == null)
        {
            MessageBox.Show("The selected monitor is not currently connected, so it cannot be identified.",
                "Monitor Not Connected", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        CloseActiveMonitorIdentifier();

        _activeIdentifyMonitorForm = new IdentifyMonitorForm(screen);
        _activeIdentifyMonitorForm.FormClosed += (s, e) =>
        {
            if (ReferenceEquals(_activeIdentifyMonitorForm, s))
            {
                _activeIdentifyMonitorForm = null;
            }
        };
        _activeIdentifyMonitorForm.Show();
    }

    private static Screen? FindScreenByDeviceName(string deviceName)
    {
        foreach (var screen in Screen.AllScreens)
        {
            if (string.Equals(screen.DeviceName, deviceName, StringComparison.OrdinalIgnoreCase))
            {
                return screen;
            }
        }

        return null;
    }

    private void CloseActiveMonitorIdentifier()
    {
        if (_activeIdentifyMonitorForm == null || _activeIdentifyMonitorForm.IsDisposed)
        {
            _activeIdentifyMonitorForm = null;
            return;
        }

        _activeIdentifyMonitorForm.Close();
        _activeIdentifyMonitorForm = null;
    }

    private void UpdateFormLayout()
    {
        int targetWidth = flowLayoutPanelShortcuts.ClientSize.Width - Scale(10);
        foreach (Control control in flowLayoutPanelShortcuts.Controls)
        {
            if (control is Panel rowPanel)
            {
                rowPanel.Width = Math.Max(Scale(200), targetWidth);
                foreach (Control child in rowPanel.Controls)
                {
                    if (child is Button btn && btn.Text == "✕")
                    {
                        btn.Location = new Point(rowPanel.Width - Scale(45), btn.Location.Y);
                    }
                }
            }
        }
    }

    private void chkRunAtStartup_CheckedChanged(object? sender, EventArgs e)
    {
        if (_isSettingStartupCheckbox) return;

        bool targetState = chkRunAtStartup.Checked;
        bool success = RegistryStartupManager.SetStartup(targetState);
        
        if (success)
        {
            _settings.RunAtStartup = targetState;
            _settings.Save();
            
            string status = targetState ? "Enabled" : "Disabled";
            MessageBox.Show($"Registry startup setting was successfully {status.ToLower()} and verified!", 
                "Registry Verified", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        else
        {
            _isSettingStartupCheckbox = true;
            chkRunAtStartup.Checked = !targetState;
            _isSettingStartupCheckbox = false;
            MessageBox.Show("Failed to save and verify startup registry value. Please run as administrator or verify registry permissions.", 
                "Registry Sync Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void chkRunAsAdministrator_CheckedChanged(object? sender, EventArgs e)
    {
        if (_isSettingRunAsAdminCheckbox) return;

        bool targetState = chkRunAsAdministrator.Checked;
        bool previousState = _settings.RunAsAdministrator;
        _settings.RunAsAdministrator = targetState;

        if (!_settings.Save())
        {
            _settings.RunAsAdministrator = previousState;
            _isSettingRunAsAdminCheckbox = true;
            chkRunAsAdministrator.Checked = previousState;
            _isSettingRunAsAdminCheckbox = false;
            return;
        }

        bool started = Program.RestartApplication(
            runAsAdministrator: targetState,
            waitForCurrentProcessToExit: true);

        if (!started)
        {
            MessageBox.Show(
                targetState
                    ? "Settings saved, but the administrator restart was cancelled or failed."
                    : "Settings saved, but the application could not restart.",
                "Restart Failed",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        _allowExit = true;
        _notifyIcon.Visible = false;
        Application.Exit();
    }

    private void SaveAndActivate()
    {
        // 1. Validate duplicates
        var duplicateCheck = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var sc in _tempShortcuts)
        {
            string hotkeyCombination = $"{sc.Modifier.Trim()}+{sc.Key.Trim()}";
            if (!duplicateCheck.Add(hotkeyCombination))
            {
                MessageBox.Show($"Duplicate shortcut detected: '{hotkeyCombination}'. Each shortcut must be unique.", 
                    "Duplicate Shortcut", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
        }

        // 1b. Validate duplicate monitors for normal shortcuts
        var monitorCheck = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var sc in _tempShortcuts)
        {
            if (sc.IsCycle) continue;

            string monitorIdent = !string.IsNullOrEmpty(sc.MonitorDeviceName) ? sc.MonitorDeviceName : sc.MonitorName;
            if (string.IsNullOrEmpty(monitorIdent)) continue;

            if (!monitorCheck.Add(monitorIdent))
            {
                MessageBox.Show($"Duplicate monitor detected for shortcut: '{sc.MonitorName}'. Each normal shortcut must point to a unique monitor.", 
                    "Duplicate Monitor", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
        }

        // 1c. Validate duplicate monitors in cycle sequence
        foreach (var sc in _tempShortcuts)
        {
            if (sc.IsCycle && sc.MonitorSequence != null && sc.MonitorSequence.Count > 1)
            {
                var cycleMonitorCheck = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var m in sc.MonitorSequence)
                {
                    string monitorIdent = !string.IsNullOrEmpty(m.MonitorName) ? m.MonitorName : m.MonitorDeviceName;
                    if (string.IsNullOrEmpty(monitorIdent)) continue;

                    if (!cycleMonitorCheck.Add(monitorIdent))
                    {
                        MessageBox.Show($"Duplicate monitor detected in cycle sequence: '{m.MonitorName}'. All monitors in the cycle sequence must be unique.", 
                            "Duplicate Monitor in Cycle", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }
                }
            }
        }

        // 2. Commit temp shortcuts to main settings
        _settings.Shortcuts.Clear();
        if (_settings.Cycle == null)
        {
            _settings.Cycle = new List<CycleConfig>();
        }
        _settings.Cycle.Clear();

        foreach (var sc in _tempShortcuts)
        {
            if (sc.IsCycle)
            {
                _settings.Cycle.Add(new CycleConfig
                {
                    Modifier = sc.Modifier,
                    Key = sc.Key,
                    MonitorSequence = sc.MonitorSequence != null
                        ? sc.MonitorSequence.Select(m => new CycleMonitorInfo { MonitorName = m.MonitorName, MonitorDeviceName = m.MonitorDeviceName }).ToList()
                        : new List<CycleMonitorInfo>()
                });
            }
            else
            {
                _settings.Shortcuts.Add(new ShortcutConfig
                {
                    Modifier = sc.Modifier,
                    Key = sc.Key,
                    MonitorName = sc.MonitorName,
                    MonitorDeviceName = sc.MonitorDeviceName,
                    MonitorExactName = sc.MonitorExactName,
                    MonitorWidth = sc.MonitorWidth,
                    MonitorHeight = sc.MonitorHeight,
                    IsCenter = sc.IsCenter,
                    X = sc.X,
                    Y = sc.Y,
                    IsCycle = false
                });
            }
        }

        _settings.RunAtStartup = chkRunAtStartup.Checked;
        _settings.RunAsAdministrator = chkRunAsAdministrator.Checked;
        _settings.Save();

        // 3. Re-register Hotkeys
        RegisterAllShortcuts();

        // 4. Minimize to tray
        this.Hide();

        // Show a nice popup
        _notifyIcon.ShowBalloonTip(3000, "Shortcuts Activated", "Settings saved successfully. Mouse Position Reset is running in tray.", ToolTipIcon.Info);
    }

    private void RegisterAllShortcuts()
    {
        _hotKeyManager.UnregisterAll();

        int id = 1;
        for (int i = 0; i < _settings.Shortcuts.Count; i++)
        {
            var config = _settings.Shortcuts[i];
            bool success = _hotKeyManager.Register(id, config.Modifier, config.Key);
            if (!success)
            {
                MessageBox.Show($"Could not register shortcut '{config.Modifier} + {config.Key}'. It might be already registered by another program.", 
                    "Hotkey Registration Failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            id++;
        }

        if (_settings.Cycle != null)
        {
            for (int i = 0; i < _settings.Cycle.Count; i++)
            {
                var config = _settings.Cycle[i];
                bool success = _hotKeyManager.Register(id, config.Modifier, config.Key);
                if (!success)
                {
                    MessageBox.Show($"Could not register cycle shortcut '{config.Modifier} + {config.Key}'. It might be already registered by another program.", 
                        "Hotkey Registration Failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                id++;
            }
        }
    }

    private void OnHotKeyTriggered(int id)
    {
        int index = id - 1;
        if (index >= 0 && index < _settings.Shortcuts.Count)
        {
            var config = _settings.Shortcuts[index];
            MoveCursorToConfig(config);
        }
        else
        {
            int cycleIndex = index - _settings.Shortcuts.Count;
            if (_settings.Cycle != null && cycleIndex >= 0 && cycleIndex < _settings.Cycle.Count)
            {
                var config = _settings.Cycle[cycleIndex];
                CycleMousePosition(config);
            }
        }
    }

    private void CycleMousePosition(CycleConfig config)
    {
        Point currentPos = Cursor.Position;
        Screen currentScreen = Screen.FromPoint(currentPos);
        Screen[] allScreens = Screen.AllScreens;
        if (allScreens == null || allScreens.Length == 0) return;

        var activeMonitors = MonitorFriendlyNameHelper.GetActiveMonitors();
        var currentActiveMonitor = activeMonitors.FirstOrDefault(m => string.Equals(m.DeviceName, currentScreen.DeviceName, StringComparison.OrdinalIgnoreCase));
        string currentFriendlyName = currentActiveMonitor?.FriendlyName ?? currentScreen.DeviceName;

        // Filter out [Disabled] monitor positions from the sequence
        var activeSeq = config.MonitorSequence != null
            ? config.MonitorSequence.Where(m => !string.Equals(m.MonitorName, "[Disabled]", StringComparison.OrdinalIgnoreCase)).ToList()
            : new List<CycleMonitorInfo>();

        if (activeSeq.Count == 0)
        {
            int currentIndex = -1;
            for (int i = 0; i < allScreens.Length; i++)
            {
                if (string.Equals(allScreens[i].DeviceName, currentScreen.DeviceName, StringComparison.OrdinalIgnoreCase))
                {
                    currentIndex = i;
                    break;
                }
            }
            if (currentIndex == -1) currentIndex = 0;
            int nextIndex = (currentIndex + 1) % allScreens.Length;
            Screen fallbackScreen = allScreens[nextIndex];
            var bounds = fallbackScreen.Bounds;
            MoveCursorWithPulse(new Point(bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2));
            return;
        }

        int seqIndex = -1;
        for (int i = 0; i < activeSeq.Count; i++)
        {
            if (string.Equals(activeSeq[i].MonitorName, currentFriendlyName, StringComparison.OrdinalIgnoreCase))
            {
                seqIndex = i;
                break;
            }
        }

        if (seqIndex == -1)
        {
            for (int i = 0; i < activeSeq.Count; i++)
            {
                if (string.Equals(activeSeq[i].MonitorDeviceName, currentScreen.DeviceName, StringComparison.OrdinalIgnoreCase))
                {
                    seqIndex = i;
                    break;
                }
            }
        }

        if (seqIndex == -1)
        {
            seqIndex = 0;
        }

        int nextSeqIndex = (seqIndex + 1) % activeSeq.Count;
        var nextMonitorInfo = activeSeq[nextSeqIndex];

        Screen? targetScreen = null;
        var nextActiveMatch = activeMonitors.FirstOrDefault(m => string.Equals(m.FriendlyName, nextMonitorInfo.MonitorName, StringComparison.OrdinalIgnoreCase));
        if (nextActiveMatch != null)
        {
            targetScreen = allScreens.FirstOrDefault(s => string.Equals(s.DeviceName, nextActiveMatch.DeviceName, StringComparison.OrdinalIgnoreCase));
        }

        if (targetScreen == null)
        {
            foreach (var screen in allScreens)
            {
                if (string.Equals(screen.DeviceName, nextMonitorInfo.MonitorDeviceName, StringComparison.OrdinalIgnoreCase))
                {
                    targetScreen = screen;
                    break;
                }
            }
        }

        targetScreen ??= Screen.PrimaryScreen;

        if (targetScreen != null)
        {
            var bounds = targetScreen.Bounds;
            MoveCursorWithPulse(new Point(bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2));
        }
    }

    private void MoveCursorToConfig(ShortcutConfig config)
    {
        Screen? targetScreen = FindScreenForConfig(config) ?? Screen.PrimaryScreen;

        if (targetScreen == null) return;

        var bounds = targetScreen.Bounds;

        if (config.IsCenter)
        {
            int cx = bounds.X + bounds.Width / 2;
            int cy = bounds.Y + bounds.Height / 2;
            MoveCursorWithPulse(new Point(cx, cy));
        }
        else
        {
            // Query current physical resolution using EnumDisplaySettings
            int currentWidth = 0;
            int currentHeight = 0;
            var dm = new MonitorFriendlyNameHelper.DEVMODE();
            dm.dmSize = (ushort)Marshal.SizeOf(dm);
            if (MonitorFriendlyNameHelper.EnumDisplaySettings(targetScreen.DeviceName, -1, ref dm))
            {
                currentWidth = (int)dm.dmPelsWidth;
                currentHeight = (int)dm.dmPelsHeight;
            }
            else
            {
                currentWidth = bounds.Width;
                currentHeight = bounds.Height;
            }

            // Verify resolution matching using physical resolutions
            if (currentWidth == config.MonitorWidth && currentHeight == config.MonitorHeight)
            {
                int tx = bounds.X + config.X;
                int ty = bounds.Y + config.Y;
                MoveCursorWithPulse(new Point(tx, ty));
            }
            else
            {
                // Fallback to center if resolution shifted
                int cx = bounds.X + bounds.Width / 2;
                int cy = bounds.Y + bounds.Height / 2;
                MoveCursorWithPulse(new Point(cx, cy));
            }
        }
    }

    private void MoveCursorWithPulse(Point targetPoint)
    {
        Cursor.Position = targetPoint;
        CursorPulseForm.ShowPulse(targetPoint);
    }

    private static Screen? FindScreenForConfig(ShortcutConfig config)
    {
        string exactName = GetConfiguredMonitorExactName(config);
        if (!string.IsNullOrWhiteSpace(exactName))
        {
            var activeMonitors = MonitorFriendlyNameHelper.GetActiveMonitors();
            var info = activeMonitors.FirstOrDefault(m => string.Equals(m.FriendlyName, exactName, StringComparison.OrdinalIgnoreCase));
            if (info != null)
            {
                return FindScreenByDeviceName(info.DeviceName);
            }

            return null;
        }

        return FindScreenByDeviceName(!string.IsNullOrWhiteSpace(config.MonitorDeviceName) ? config.MonitorDeviceName : config.MonitorName);
    }

    private void ShowConfigForm()
    {
        StartMinimized = false;
        this.Show();
        this.WindowState = FormWindowState.Normal;
        this.BringToFront();
    }

    private void ExitApplication()
    {
        _allowExit = true;
        _notifyIcon.Visible = false;
        Application.Exit();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (!_allowExit && e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            this.Hide();
            _notifyIcon.ShowBalloonTip(2000, "Still Running", "Mouse Position Reset is minimized to the system tray.", ToolTipIcon.Info);
        }
        else
        {
            CloseActiveMonitorIdentifier();
            _hotKeyManager.Dispose();
            
            if (_trayIcon != null)
            {
                _notifyIcon.Icon = null;
                _notifyIcon.Dispose();
                DestroyIcon(_trayIcon.Handle);
                _trayIcon.Dispose();
            }
            
            base.OnFormClosing(e);
        }
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        if (this.WindowState == FormWindowState.Minimized)
        {
            this.Hide();
        }
    }
}
