using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using Screen = System.Windows.Forms.Screen;

namespace MousePositionReset;

public partial class IdentifyMonitorWindow : Window
{
    private const uint SWP_SHOWWINDOW = 0x0040;
    private const uint SWP_NOACTIVATE = 0x0010;
    private static readonly IntPtr HWND_TOPMOST = new(-1);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    private readonly DispatcherTimer _timer;
    private readonly Screen _screen;

    public IdentifyMonitorWindow(Screen screen)
    {
        InitializeComponent();
        _screen = screen;

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        _timer.Tick += (s, e) =>
        {
            _timer.Stop();
            Close();
        };

        this.Loaded += (s, e) =>
        {
            var area = _screen.WorkingArea;
            int margin = 24;
            int size = 190;
            var hwnd = new WindowInteropHelper(this).Handle;
            SetWindowPos(hwnd, HWND_TOPMOST, area.Left + margin, area.Bottom - size - margin, size, size, SWP_SHOWWINDOW | SWP_NOACTIVATE);

            _timer.Start();
        };
    }
}
