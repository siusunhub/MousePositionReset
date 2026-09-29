using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using Point = System.Drawing.Point;
using Screen = System.Windows.Forms.Screen;
using SystemInformation = System.Windows.Forms.SystemInformation;

namespace MousePositionReset;

public partial class CountdownWindow : Window
{
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT lpPoint);

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    private const uint SWP_SHOWWINDOW = 0x0040;
    private static readonly IntPtr HWND_TOPMOST = new(-1);

    private readonly DispatcherTimer _timer;
    private int _secondsLeft = 3;

    public Point CapturedPosition { get; private set; }
    public Screen CapturedScreen { get; private set; } = Screen.PrimaryScreen ?? Screen.AllScreens[0];

    public CountdownWindow()
    {
        InitializeComponent();

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += Timer_Tick;

        this.Loaded += (s, e) =>
        {
            var virtualScreen = SystemInformation.VirtualScreen;
            var hwnd = new WindowInteropHelper(this).Handle;
            SetWindowPos(hwnd, HWND_TOPMOST, virtualScreen.Left, virtualScreen.Top, virtualScreen.Width, virtualScreen.Height, SWP_SHOWWINDOW);

            try { Console.Beep(800, 100); } catch { }
            _timer.Start();
        };
    }

    private void Timer_Tick(object? sender, EventArgs e)
    {
        _secondsLeft--;
        if (_secondsLeft > 0)
        {
            try { Console.Beep(800, 100); } catch { }
            lblCountdown.Text = $"Move mouse to target position...\n\nCapturing in {_secondsLeft}";
        }
        else
        {
            _timer.Stop();
            try { Console.Beep(1200, 300); } catch { }

            if (GetCursorPos(out POINT pt))
            {
                CapturedPosition = new Point(pt.X, pt.Y);
            }
            else
            {
                CapturedPosition = System.Windows.Forms.Cursor.Position;
            }

            CapturedScreen = Screen.FromPoint(CapturedPosition);
            this.DialogResult = true;
            this.Close();
        }
    }
}
