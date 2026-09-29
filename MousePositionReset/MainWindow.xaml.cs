using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Application = System.Windows.Application;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using Button = System.Windows.Controls.Button;
using Color = System.Drawing.Color;
using ComboBox = System.Windows.Controls.ComboBox;
using Icon = System.Drawing.Icon;
using MessageBox = System.Windows.MessageBox;
using NotifyIcon = System.Windows.Forms.NotifyIcon;
using Pen = System.Drawing.Pen;
using Point = System.Drawing.Point;
using RadioButton = System.Windows.Controls.RadioButton;
using Screen = System.Windows.Forms.Screen;
using TextBox = System.Windows.Controls.TextBox;

namespace MousePositionReset;

public partial class MainWindow : Window
{
    private const string AppVersion = "v0.5";

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern bool DestroyIcon(IntPtr handle);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);

    [DllImport("user32.dll")]
    private static extern bool IsIconic(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int command);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr insertAfter, int x, int y, int width, int height, uint flags);

    private const int ResizeWindowHotkeyId = 1000;
    private const int SW_RESTORE = 9;
    private const uint SWP_NOZORDER = 0x0004;
    private const uint SWP_NOACTIVATE = 0x0010;

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    public class MonitorItem
    {
        public string DeviceName { get; set; } = "";
        public string ExactName { get; set; } = "";
        public int Width { get; set; }
        public int Height { get; set; }
        public string DisplayText { get; set; } = "";
        public override string ToString() => DisplayText;
    }

    public class CycleDropdownItem
    {
        public CycleMonitorInfo MonitorInfo { get; set; } = new();
        public override string ToString() => MonitorInfo.MonitorName;
    }

    private AppSettings _settings = new();
    private readonly List<ShortcutConfig> _tempShortcuts = new();
    private readonly HotKeyManager _hotKeyManager;

    private NotifyIcon _notifyIcon = null!;
    private System.Drawing.Icon? _trayIcon;
    private IdentifyMonitorWindow? _activeIdentifyMonitorWindow;
    private bool _allowExit = false;
    private bool _isSettingStartupCheckbox = false;
    private bool _isSettingRunAsAdminCheckbox = false;

    public bool StartMinimized = false;

    public MainWindow()
    {
        InitializeComponent();
        this.Title = $"Mouse Position Reset {AppVersion}";

        InitializeHeaderControls();

        _hotKeyManager = new HotKeyManager();
        _hotKeyManager.HotKeyPressed += OnHotKeyTriggered;

        InitializeTrayIcon();
        LoadConfiguration();

        this.Loaded += (s, e) =>
        {
            if (StartMinimized)
            {
                this.Hide();
            }
        };
    }

    private void InitializeHeaderControls()
    {
        cbResizeModifier.Items.Clear();
        cbResizeModifier.Items.Add("CTRL");
        cbResizeModifier.Items.Add("CTRL + SHIFT");

        cbResizeKey.Items.Clear();
        for (char c = 'A'; c <= 'Z'; c++) cbResizeKey.Items.Add(c.ToString());
        for (char c = '0'; c <= '9'; c++) cbResizeKey.Items.Add(c.ToString());
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

        var contextMenu = new System.Windows.Forms.ContextMenuStrip();
        var itemConfig = new System.Windows.Forms.ToolStripMenuItem("Configure Shortcuts...", null, (s, e) => ShowConfigForm());
        var itemExit = new System.Windows.Forms.ToolStripMenuItem("Exit", null, (s, e) => ExitApplication());

        contextMenu.Items.Add(itemConfig);
        contextMenu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
        contextMenu.Items.Add(itemExit);

        _notifyIcon.ContextMenuStrip = contextMenu;
        _notifyIcon.DoubleClick += (s, e) => ShowConfigForm();
    }

    private Icon CreateDynamicIcon()
    {
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
        return System.Drawing.Icon.FromHandle(hIcon);
    }

    private void LoadConfiguration()
    {
        _settings = AppSettings.Load();

        bool registryEnabled = RegistryStartupManager.IsStartupEnabled();
        if (_settings.RunAtStartup != registryEnabled)
        {
            RegistryStartupManager.SetStartup(_settings.RunAtStartup);
        }

        _isSettingStartupCheckbox = true;
        chkRunAtStartup.IsChecked = _settings.RunAtStartup;
        _isSettingStartupCheckbox = false;

        _isSettingRunAsAdminCheckbox = true;
        chkRunAsAdministrator.IsChecked = _settings.RunAsAdministrator;
        _isSettingRunAsAdminCheckbox = false;

        _settings.ResizeWindowHotkey ??= new ResizeWindowHotkeyConfig();
        cbResizeModifier.Text = _settings.ResizeWindowHotkey.Modifier.ToUpperInvariant();
        cbResizeKey.Text = _settings.ResizeWindowHotkey.Key.ToUpperInvariant();

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

        pnlShortcuts.Children.Clear();
        foreach (var sc in _tempShortcuts)
        {
            if (sc.IsCycle)
            {
                pnlShortcuts.Children.Add(CreateCycleRowPanel(sc));
            }
            else
            {
                pnlShortcuts.Children.Add(CreateShortcutRowPanel(sc));
            }
        }

        UpdateAddCycleButtonState();
        RegisterAllShortcuts();
    }

    private void UpdateAddCycleButtonState()
    {
        bool hasCycle = _tempShortcuts.Any(sc => sc.IsCycle);
        btnAddCycle.IsEnabled = !hasCycle;
    }

    private void btnAddShortcut_Click(object sender, RoutedEventArgs e)
    {
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
        pnlShortcuts.Children.Add(rowPanel);
        rowPanel.BringIntoView();
    }

    private void btnAddCycle_Click(object sender, RoutedEventArgs e)
    {
        if (_tempShortcuts.Any(sc => sc.IsCycle))
        {
            MessageBox.Show("Only one cycle setting is allowed.", "Limit Exceeded", MessageBoxButton.OK, MessageBoxImage.Warning);
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
        pnlShortcuts.Children.Add(rowPanel);
        rowPanel.BringIntoView();
        UpdateAddCycleButtonState();
    }

    private Border CreateShortcutRowPanel(ShortcutConfig config)
    {
        var border = new Border
        {
            Height = 90,
            Background = (Brush)Application.Current.Resources["CardBg"],
            BorderBrush = (Brush)Application.Current.Resources["CardBorder"],
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(3),
            Margin = new Thickness(0, 0, 0, 8)
        };

        var canvas = new Canvas { Height = 90 };
        border.Child = canvas;

        // Line 1: Hotkey
        var lblHotkey = new TextBlock
        {
            Text = "Hotkey:",
            FontSize = 13,
            FontWeight = FontWeights.Bold,
            Foreground = (Brush)Application.Current.Resources["TextSecondary"]
        };
        Canvas.SetLeft(lblHotkey, 12);
        Canvas.SetTop(lblHotkey, 16);
        canvas.Children.Add(lblHotkey);

        var cbMod = new ComboBox { Width = 110, Height = 25 };
        cbMod.Items.Add("CTRL");
        cbMod.Items.Add("CTRL + SHIFT");
        cbMod.Text = config.Modifier.ToUpperInvariant();
        cbMod.SelectionChanged += (s, e) =>
        {
            if (cbMod.SelectedItem != null)
                config.Modifier = cbMod.SelectedItem.ToString() ?? "";
        };
        Canvas.SetLeft(cbMod, 72);
        Canvas.SetTop(cbMod, 12);
        canvas.Children.Add(cbMod);

        var lblPlus = new TextBlock
        {
            Text = "+",
            FontFamily = new System.Windows.Media.FontFamily("Segoe UI Semibold"),
            FontSize = 13.33,
            FontWeight = FontWeights.Bold,
            Foreground = (Brush)Application.Current.Resources["TextSecondary"]
        };
        Canvas.SetLeft(lblPlus, 187);
        Canvas.SetTop(lblPlus, 15);
        canvas.Children.Add(lblPlus);

        var cbKey = new ComboBox { Width = 55, Height = 25 };
        for (char c = 'A'; c <= 'Z'; c++) cbKey.Items.Add(c.ToString());
        for (char c = '0'; c <= '9'; c++) cbKey.Items.Add(c.ToString());
        cbKey.Text = config.Key.ToUpperInvariant();
        cbKey.SelectionChanged += (s, e) =>
        {
            if (cbKey.SelectedItem != null)
                config.Key = cbKey.SelectedItem.ToString() ?? "";
        };
        Canvas.SetLeft(cbKey, 207);
        Canvas.SetTop(cbKey, 12);
        canvas.Children.Add(cbKey);

        var lblMonitor = new TextBlock
        {
            Text = "Monitor:",
            FontSize = 13,
            FontWeight = FontWeights.Bold,
            Foreground = (Brush)Application.Current.Resources["TextSecondary"]
        };
        Canvas.SetLeft(lblMonitor, 272);
        Canvas.SetTop(lblMonitor, 16);
        canvas.Children.Add(lblMonitor);

        var cbMonitor = new ComboBox { Width = 280, Height = 25 };
        PopulateMonitors(cbMonitor, config);
        Canvas.SetLeft(cbMonitor, 342);
        Canvas.SetTop(cbMonitor, 12);
        canvas.Children.Add(cbMonitor);

        var btnIdentify = new Button
        {
            Content = "i",
            Width = 28,
            Height = 26,
            Style = (Style)Application.Current.Resources["InfoSmallButtonStyle"]
        };
        btnIdentify.Click += (s, e) => ShowMonitorIdentifier(cbMonitor);
        Canvas.SetLeft(btnIdentify, 632);
        Canvas.SetTop(btnIdentify, 11);
        canvas.Children.Add(btnIdentify);

        var btnDelete = new Button
        {
            Content = "✕",
            Width = 30,
            Height = 26,
            Style = (Style)Application.Current.Resources["DangerSmallButtonStyle"]
        };
        btnDelete.Click += (s, e) =>
        {
            _tempShortcuts.Remove(config);
            pnlShortcuts.Children.Remove(border);
            UpdateAddCycleButtonState();
        };
        Canvas.SetLeft(btnDelete, 672);
        Canvas.SetTop(btnDelete, 11);
        canvas.Children.Add(btnDelete);

        // Line 2: Position
        var lblPosition = new TextBlock
        {
            Text = "Position:",
            FontSize = 13,
            FontWeight = FontWeights.Bold,
            Foreground = (Brush)Application.Current.Resources["TextSecondary"]
        };
        Canvas.SetLeft(lblPosition, 12);
        Canvas.SetTop(lblPosition, 54);
        canvas.Children.Add(lblPosition);

        string groupName = "PositionGroup_" + Guid.NewGuid().ToString("N");
        var rbCenter = new RadioButton
        {
            Content = "Center of Monitor",
            GroupName = groupName,
            IsChecked = config.IsCenter
        };
        Canvas.SetLeft(rbCenter, 80);
        Canvas.SetTop(rbCenter, 52);
        canvas.Children.Add(rbCenter);

        var rbSpecific = new RadioButton
        {
            Content = "Specific Position",
            GroupName = groupName,
            IsChecked = !config.IsCenter
        };
        Canvas.SetLeft(rbSpecific, 215);
        Canvas.SetTop(rbSpecific, 52);
        canvas.Children.Add(rbSpecific);

        var lblX = new TextBlock { Text = "X:", FontSize = 12.67, Foreground = (Brush)Application.Current.Resources["TextSecondary"] };
        Canvas.SetLeft(lblX, 340);
        Canvas.SetTop(lblX, 54);
        canvas.Children.Add(lblX);

        var txtX = new TextBox
        {
            Width = 60,
            Height = 24,
            Text = config.X.ToString()
        };
        Canvas.SetLeft(txtX, 360);
        Canvas.SetTop(txtX, 50);
        canvas.Children.Add(txtX);

        var lblY = new TextBlock { Text = "Y:", FontSize = 12.67, Foreground = (Brush)Application.Current.Resources["TextSecondary"] };
        Canvas.SetLeft(lblY, 430);
        Canvas.SetTop(lblY, 54);
        canvas.Children.Add(lblY);

        var txtY = new TextBox
        {
            Width = 60,
            Height = 24,
            Text = config.Y.ToString()
        };
        Canvas.SetLeft(txtY, 450);
        Canvas.SetTop(txtY, 50);
        canvas.Children.Add(txtY);

        var btnDeclare = new Button
        {
            Content = "Declare [3s]",
            Width = 100,
            Height = 28,
            Style = (Style)Application.Current.Resources["AccentButtonStyle"],
            FontSize = 12
        };
        Canvas.SetLeft(btnDeclare, 520);
        Canvas.SetTop(btnDeclare, 48);
        canvas.Children.Add(btnDeclare);

        void UpdateControlsState()
        {
            bool isSpecific = rbSpecific.IsChecked == true;
            lblX.IsEnabled = isSpecific;
            txtX.IsEnabled = isSpecific;
            lblY.IsEnabled = isSpecific;
            txtY.IsEnabled = isSpecific;
            btnDeclare.IsEnabled = isSpecific;
        }

        rbCenter.Checked += (s, e) =>
        {
            config.IsCenter = true;
            UpdateControlsState();
        };

        rbSpecific.Checked += (s, e) =>
        {
            config.IsCenter = false;
            UpdateControlsState();
        };

        txtX.TextChanged += (s, e) =>
        {
            if (int.TryParse(txtX.Text, out int xVal))
            {
                config.X = Math.Max(0, Math.Min(20000, xVal));
            }
        };

        txtY.TextChanged += (s, e) =>
        {
            if (int.TryParse(txtY.Text, out int yVal))
            {
                config.Y = Math.Max(0, Math.Min(20000, yVal));
            }
        };

        btnDeclare.Click += (s, e) =>
        {
            this.Hide();

            var cdf = new CountdownWindow();
            if (cdf.ShowDialog() == true)
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

                rbSpecific.IsChecked = true;
                txtX.Text = config.X.ToString();
                txtY.Text = config.Y.ToString();

                PopulateMonitors(cbMonitor, config);
            }

            this.Show();
            this.Activate();
        };

        UpdateControlsState();
        return border;
    }

    private Border CreateCycleRowPanel(ShortcutConfig config)
    {
        var border = new Border
        {
            Height = 90,
            Background = (Brush)Application.Current.Resources["CycleCardBg"],
            BorderBrush = (Brush)Application.Current.Resources["CycleCardBorder"],
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(3),
            Margin = new Thickness(0, 0, 0, 8)
        };

        var canvas = new Canvas { Height = 90 };
        border.Child = canvas;

        var lblHotkey = new TextBlock
        {
            Text = "Cycle Hotkey:",
            FontSize = 13,
            FontWeight = FontWeights.Bold,
            Foreground = (Brush)Application.Current.Resources["TextSecondary"]
        };
        Canvas.SetLeft(lblHotkey, 12);
        Canvas.SetTop(lblHotkey, 16);
        canvas.Children.Add(lblHotkey);

        var cbMod = new ComboBox { Width = 110, Height = 25 };
        cbMod.Items.Add("CTRL");
        cbMod.Items.Add("CTRL + SHIFT");
        cbMod.Text = config.Modifier.ToUpperInvariant();
        cbMod.SelectionChanged += (s, e) =>
        {
            if (cbMod.SelectedItem != null)
                config.Modifier = cbMod.SelectedItem.ToString() ?? "";
        };
        Canvas.SetLeft(cbMod, 115);
        Canvas.SetTop(cbMod, 12);
        canvas.Children.Add(cbMod);

        var lblPlus = new TextBlock
        {
            Text = "+",
            FontFamily = new System.Windows.Media.FontFamily("Segoe UI Semibold"),
            FontSize = 13.33,
            FontWeight = FontWeights.Bold,
            Foreground = (Brush)Application.Current.Resources["TextSecondary"]
        };
        Canvas.SetLeft(lblPlus, 230);
        Canvas.SetTop(lblPlus, 15);
        canvas.Children.Add(lblPlus);

        var cbKey = new ComboBox { Width = 55, Height = 25 };
        for (char c = 'A'; c <= 'Z'; c++) cbKey.Items.Add(c.ToString());
        for (char c = '0'; c <= '9'; c++) cbKey.Items.Add(c.ToString());
        cbKey.Text = config.Key.ToUpperInvariant();
        cbKey.SelectionChanged += (s, e) =>
        {
            if (cbKey.SelectedItem != null)
                config.Key = cbKey.SelectedItem.ToString() ?? "";
        };
        Canvas.SetLeft(cbKey, 250);
        Canvas.SetTop(cbKey, 12);
        canvas.Children.Add(cbKey);

        var btnDelete = new Button
        {
            Content = "✕",
            Width = 30,
            Height = 26,
            Style = (Style)Application.Current.Resources["DangerSmallButtonStyle"]
        };
        btnDelete.Click += (s, e) =>
        {
            _tempShortcuts.Remove(config);
            pnlShortcuts.Children.Remove(border);
            UpdateAddCycleButtonState();
        };
        Canvas.SetLeft(btnDelete, 672);
        Canvas.SetTop(btnDelete, 11);
        canvas.Children.Add(btnDelete);

        var lblMonitorsTitle = new TextBlock
        {
            Text = "Monitors Cycle:",
            FontSize = 13,
            FontWeight = FontWeights.Bold,
            Foreground = (Brush)Application.Current.Resources["TextSecondary"]
        };
        Canvas.SetLeft(lblMonitorsTitle, 12);
        Canvas.SetTop(lblMonitorsTitle, 54);
        canvas.Children.Add(lblMonitorsTitle);

        var activeMonitors = MonitorFriendlyNameHelper.GetActiveMonitors();
        var screens = Screen.AllScreens;

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

        var combos = new List<ComboBox>();
        int screensCount = screens.Length;
        int comboWidth = screensCount >= 5 ? 85 : 120;
        int labelWidth = 18;
        int gap = 10;
        int startX = 120;

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

            var lblSeq = new TextBlock
            {
                Text = $"{i + 1}.",
                Width = labelWidth,
                Height = 20,
                FontSize = 13,
                FontWeight = FontWeights.Bold,
                Foreground = (Brush)Application.Current.Resources["TextSecondary"],
                TextAlignment = TextAlignment.Right
            };
            Canvas.SetLeft(lblSeq, startX + i * (labelWidth + comboWidth + gap));
            Canvas.SetTop(lblSeq, 54);
            canvas.Children.Add(lblSeq);

            var cbSeq = new ComboBox
            {
                Width = comboWidth,
                Height = 25
            };

            foreach (var item in activeItems) cbSeq.Items.Add(item);
            combos.Add(cbSeq);

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

            cbSeq.SelectionChanged += (sender, e) => SaveSequenceFromDropdowns();
            Canvas.SetLeft(cbSeq, startX + i * (labelWidth + comboWidth + gap) + labelWidth + 2);
            Canvas.SetTop(cbSeq, 50);
            canvas.Children.Add(cbSeq);
        }

        SaveSequenceFromDropdowns();
        return border;
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

        cbMonitor.SelectionChanged += (s, e) =>
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
                "Monitor Not Connected", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        CloseActiveMonitorIdentifier();

        _activeIdentifyMonitorWindow = new IdentifyMonitorWindow(screen);
        _activeIdentifyMonitorWindow.Closed += (s, e) =>
        {
            if (ReferenceEquals(_activeIdentifyMonitorWindow, s))
            {
                _activeIdentifyMonitorWindow = null;
            }
        };
        _activeIdentifyMonitorWindow.Show();
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
        if (_activeIdentifyMonitorWindow != null)
        {
            try { _activeIdentifyMonitorWindow.Close(); } catch { }
            _activeIdentifyMonitorWindow = null;
        }
    }

    private void chkRunAtStartup_Click(object sender, RoutedEventArgs e)
    {
        if (_isSettingStartupCheckbox) return;

        bool targetState = chkRunAtStartup.IsChecked == true;
        bool success = RegistryStartupManager.SetStartup(targetState);

        if (success)
        {
            _settings.RunAtStartup = targetState;
            _settings.Save();

            string status = targetState ? "Enabled" : "Disabled";
            MessageBox.Show($"Registry startup setting was successfully {status.ToLower()} and verified!",
                "Registry Verified", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        else
        {
            _isSettingStartupCheckbox = true;
            chkRunAtStartup.IsChecked = !targetState;
            _isSettingStartupCheckbox = false;
            MessageBox.Show("Failed to save and verify startup registry value. Please run as administrator or verify registry permissions.",
                "Registry Sync Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void chkRunAsAdministrator_Click(object sender, RoutedEventArgs e)
    {
        if (_isSettingRunAsAdminCheckbox) return;

        bool targetState = chkRunAsAdministrator.IsChecked == true;
        bool previousState = _settings.RunAsAdministrator;
        _settings.RunAsAdministrator = targetState;

        if (!_settings.Save())
        {
            _settings.RunAsAdministrator = previousState;
            _isSettingRunAsAdminCheckbox = true;
            chkRunAsAdministrator.IsChecked = previousState;
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
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        _allowExit = true;
        _notifyIcon.Visible = false;
        System.Windows.Application.Current.Shutdown();
    }

    private void btnSave_Click(object sender, RoutedEventArgs e)
    {
        // 1. Validate duplicates
        var duplicateCheck = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var sc in _tempShortcuts)
        {
            string hotkeyCombination = $"{sc.Modifier.Trim()}+{sc.Key.Trim()}";
            if (!duplicateCheck.Add(hotkeyCombination))
            {
                MessageBox.Show($"Duplicate shortcut detected: '{hotkeyCombination}'. Each shortcut must be unique.",
                    "Duplicate Shortcut", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
        }

        string resizeHotkeyCombination = $"{cbResizeModifier.Text.Trim()}+{cbResizeKey.Text.Trim()}";
        if (!duplicateCheck.Add(resizeHotkeyCombination))
        {
            MessageBox.Show($"Duplicate shortcut detected: '{resizeHotkeyCombination}'. Each shortcut must be unique.",
                "Duplicate Shortcut", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
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
                    "Duplicate Monitor", MessageBoxButton.OK, MessageBoxImage.Warning);
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
                            "Duplicate Monitor in Cycle", MessageBoxButton.OK, MessageBoxImage.Warning);
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

        _settings.RunAtStartup = chkRunAtStartup.IsChecked == true;
        _settings.RunAsAdministrator = chkRunAsAdministrator.IsChecked == true;
        _settings.ResizeWindowHotkey = new ResizeWindowHotkeyConfig
        {
            Modifier = cbResizeModifier.Text,
            Key = cbResizeKey.Text
        };
        _settings.Save();

        // 3. Re-register Hotkeys
        RegisterAllShortcuts();

        // 4. Minimize to tray
        this.Hide();

        _notifyIcon.ShowBalloonTip(3000, "Shortcuts Activated", "Settings saved successfully. Mouse Position Reset is running in tray.", System.Windows.Forms.ToolTipIcon.Info);
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
                    "Hotkey Registration Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
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
                        "Hotkey Registration Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
                id++;
            }
        }

        if (!_hotKeyManager.Register(ResizeWindowHotkeyId, _settings.ResizeWindowHotkey.Modifier, _settings.ResizeWindowHotkey.Key))
        {
            MessageBox.Show($"Could not register resize shortcut '{_settings.ResizeWindowHotkey.Modifier} + {_settings.ResizeWindowHotkey.Key}'. It might be already registered by another program.",
                "Hotkey Registration Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void OnHotKeyTriggered(int id)
    {
        if (id == ResizeWindowHotkeyId)
        {
            ResizeForegroundWindow();
            return;
        }

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

    private static void ResizeForegroundWindow()
    {
        IntPtr windowHandle = GetForegroundWindow();
        if (windowHandle == IntPtr.Zero || !GetWindowRect(windowHandle, out _)) return;

        if (IsIconic(windowHandle))
        {
            ShowWindow(windowHandle, SW_RESTORE);
        }

        var workArea = Screen.FromPoint(System.Windows.Forms.Cursor.Position).WorkingArea;
        int width = (int)Math.Round(workArea.Width * 0.8);
        int height = (int)Math.Round(workArea.Height * 0.8);
        int x = workArea.X + (workArea.Width - width) / 2;
        int y = workArea.Y + (workArea.Height - height) / 2;

        SetWindowPos(windowHandle, IntPtr.Zero, x, y, width, height, SWP_NOZORDER | SWP_NOACTIVATE);
    }

    private void CycleMousePosition(CycleConfig config)
    {
        Point currentPos = System.Windows.Forms.Cursor.Position;
        Screen currentScreen = Screen.FromPoint(currentPos);
        Screen[] allScreens = Screen.AllScreens;
        if (allScreens == null || allScreens.Length == 0) return;

        var activeMonitors = MonitorFriendlyNameHelper.GetActiveMonitors();
        var currentActiveMonitor = activeMonitors.FirstOrDefault(m => string.Equals(m.DeviceName, currentScreen.DeviceName, StringComparison.OrdinalIgnoreCase));
        string currentFriendlyName = currentActiveMonitor?.FriendlyName ?? currentScreen.DeviceName;

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

            if (currentWidth == config.MonitorWidth && currentHeight == config.MonitorHeight)
            {
                int tx = bounds.X + config.X;
                int ty = bounds.Y + config.Y;
                MoveCursorWithPulse(new Point(tx, ty));
            }
            else
            {
                int cx = bounds.X + bounds.Width / 2;
                int cy = bounds.Y + bounds.Height / 2;
                MoveCursorWithPulse(new Point(cx, cy));
            }
        }
    }

    private void MoveCursorWithPulse(Point targetPoint)
    {
        System.Windows.Forms.Cursor.Position = targetPoint;
        CursorPulseWindow.ShowPulse(targetPoint);
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

    public void ShowConfigForm()
    {
        StartMinimized = false;
        this.Show();
        this.WindowState = WindowState.Normal;
        this.Activate();
    }

    public void ExitApplication()
    {
        _allowExit = true;
        _notifyIcon.Visible = false;
        System.Windows.Application.Current.Shutdown();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_allowExit)
        {
            e.Cancel = true;
            this.Hide();
            _notifyIcon.ShowBalloonTip(2000, "Still Running", "Mouse Position Reset is minimized to the system tray.", System.Windows.Forms.ToolTipIcon.Info);
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

            base.OnClosing(e);
        }
    }

    protected override void OnStateChanged(EventArgs e)
    {
        base.OnStateChanged(e);
        if (this.WindowState == WindowState.Minimized)
        {
            this.Hide();
        }
    }
}
