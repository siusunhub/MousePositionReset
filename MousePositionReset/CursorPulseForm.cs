using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace MousePositionReset;

public sealed class CursorPulseForm : Form
{
    private const int WS_EX_TRANSPARENT = 0x00000020;
    private const int WS_EX_TOOLWINDOW = 0x00000080;
    private const int WS_EX_NOACTIVATE = 0x08000000;

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out NativePoint lpPoint);

    private static CursorPulseForm? _activePulse;

    private readonly System.Windows.Forms.Timer _timer;
    private readonly Stopwatch _stopwatch = new();
    private Point _cursorAnchor;
    private readonly int _durationMs;

    private CursorPulseForm(Point center, int durationMs = 1200)
    {
        _cursorAnchor = center;
        _durationMs = durationMs;

        int size = 260;
        Bounds = new Rectangle(center.X - size / 2, center.Y - size / 2, size, size);
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        ShowInTaskbar = false;
        TopMost = true;
        BackColor = Color.Lime;
        TransparencyKey = Color.Lime;
        DoubleBuffered = true;

        _timer = new System.Windows.Forms.Timer { Interval = 16 };
        _timer.Tick += Timer_Tick;
    }

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            CreateParams cp = base.CreateParams;
            cp.ExStyle |= WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE;
            return cp;
        }
    }

    public static void ShowPulse(Point center)
    {
        if (_activePulse != null && !_activePulse.IsDisposed)
        {
            _activePulse.Close();
        }

        _activePulse = new CursorPulseForm(center);
        _activePulse.FormClosed += (s, e) =>
        {
            if (ReferenceEquals(_activePulse, s))
            {
                _activePulse = null;
            }
        };
        _activePulse.Show();
        _activePulse.TopMost = true;
        _activePulse.BringToFront();
        _activePulse.StartAnimation();
    }

    private void StartAnimation()
    {
        _cursorAnchor = GetCurrentCursorPosition();
        _stopwatch.Start();
        _timer.Start();
        Invalidate();
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

        Invalidate();
    }

    private bool HasCursorMoved()
    {
        Point currentCursor = GetCurrentCursorPosition();

        return currentCursor.X != _cursorAnchor.X || currentCursor.Y != _cursorAnchor.Y;
    }

    private static Point GetCurrentCursorPosition()
    {
        if (GetCursorPos(out NativePoint currentCursor))
        {
            return new Point(currentCursor.X, currentCursor.Y);
        }

        return Cursor.Position;
    }

    private void StopAndClose()
    {
        _timer.Stop();
        Hide();
        Close();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);

        float progress = Math.Clamp(_stopwatch.ElapsedMilliseconds / (float)_durationMs, 0f, 1f);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

        DrawRing(e.Graphics, progress);
        DrawRing(e.Graphics, Math.Clamp(progress - 0.28f, 0f, 1f));
    }

    private void DrawRing(Graphics graphics, float progress)
    {
        if (progress <= 0f)
        {
            return;
        }

        float maxRadius = ClientSize.Width / 2f - 10f;
        float radius = 8f + (maxRadius - 8f) * progress;
        int alpha = Math.Max(120, (int)(255 * (1f - progress)));

        PointF center = new(ClientSize.Width / 2f, ClientSize.Height / 2f);
        float diameter = radius * 2f;
        using var pen = new Pen(Color.FromArgb(alpha, 139, 92, 246), 6f - 2f * progress);
        graphics.DrawEllipse(
            pen,
            center.X - radius,
            center.Y - radius,
            diameter,
            diameter);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _timer.Dispose();
        }

        base.Dispose(disposing);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;

        public NativePoint(Point point)
        {
            X = point.X;
            Y = point.Y;
        }
    }
}
