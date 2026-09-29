using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Point = System.Drawing.Point;

namespace MousePositionReset;

public partial class CursorPulseWindow : Window
{
    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_TRANSPARENT = 0x00000020;
    private const int WS_EX_TOOLWINDOW = 0x00000080;
    private const int WS_EX_NOACTIVATE = 0x08000000;
    private const uint SWP_SHOWWINDOW = 0x0040;
    private const uint SWP_NOACTIVATE = 0x0010;
    private static readonly IntPtr HWND_TOPMOST = new(-1);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

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

    private static CursorPulseWindow? _activePulse;

    private readonly DispatcherTimer _timer;
    private readonly Stopwatch _stopwatch = new();
    private Point _cursorAnchor;
    private readonly int _durationMs;
    private readonly Point _center;

    public CursorPulseWindow(Point center, int durationMs = 1200)
    {
        InitializeComponent();
        _center = center;
        _cursorAnchor = center;
        _durationMs = durationMs;

        _timer = new DispatcherTimer(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(16) };
        _timer.Tick += Timer_Tick;
    }

    public static void ShowPulse(Point center)
    {
        if (_activePulse != null)
        {
            try
            {
                _activePulse.Close();
            }
            catch { }
            _activePulse = null;
        }

        _activePulse = new CursorPulseWindow(center);
        _activePulse.Closed += (s, e) =>
        {
            if (ReferenceEquals(_activePulse, s))
            {
                _activePulse = null;
            }
        };

        _activePulse.Show();
        _activePulse.StartAnimation();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        var hwnd = new WindowInteropHelper(this).Handle;
        int exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
        SetWindowLong(hwnd, GWL_EXSTYLE, exStyle | WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE);

        SetWindowPos(hwnd, HWND_TOPMOST, _center.X - 130, _center.Y - 130, 260, 260, SWP_SHOWWINDOW | SWP_NOACTIVATE);
    }

    private void StartAnimation()
    {
        _cursorAnchor = GetCurrentCursorPosition();
        _stopwatch.Start();
        _timer.Start();
        UpdateRings();
    }

    private void Timer_Tick(object? sender, EventArgs e)
    {
        if (_stopwatch.ElapsedMilliseconds > 150 && HasCursorMoved())
        {
            StopAndClose();
            return;
        }

        if (_stopwatch.ElapsedMilliseconds >= _durationMs)
        {
            StopAndClose();
            return;
        }

        UpdateRings();
    }

    private void UpdateRings()
    {
        float progress = Math.Clamp(_stopwatch.ElapsedMilliseconds / (float)_durationMs, 0f, 1f);

        UpdateRingElement(Ring1, progress);
        UpdateRingElement(Ring2, Math.Clamp(progress - 0.28f, 0f, 1f));
    }

    private void UpdateRingElement(System.Windows.Shapes.Ellipse ellipse, float progress)
    {
        if (progress <= 0f)
        {
            ellipse.Visibility = Visibility.Collapsed;
            return;
        }

        ellipse.Visibility = Visibility.Visible;

        float maxRadius = 130f - 10f;
        float radius = 8f + (maxRadius - 8f) * progress;
        float diameter = radius * 2f;
        double strokeThickness = Math.Max(1.0, 6.0 - 2.0 * progress);
        byte alpha = (byte)Math.Max(120, (int)(255 * (1f - progress)));

        ellipse.Width = diameter;
        ellipse.Height = diameter;
        ellipse.StrokeThickness = strokeThickness;
        ellipse.Stroke = new SolidColorBrush(System.Windows.Media.Color.FromArgb(alpha, 139, 92, 246));

        Canvas.SetLeft(ellipse, 130 - radius);
        Canvas.SetTop(ellipse, 130 - radius);
    }

    private bool HasCursorMoved()
    {
        Point currentCursor = GetCurrentCursorPosition();
        return currentCursor.X != _cursorAnchor.X || currentCursor.Y != _cursorAnchor.Y;
    }

    private static Point GetCurrentCursorPosition()
    {
        if (GetCursorPos(out POINT currentCursor))
        {
            return new Point(currentCursor.X, currentCursor.Y);
        }

        return System.Windows.Forms.Cursor.Position;
    }

    private void StopAndClose()
    {
        _timer.Stop();
        Close();
    }
}
