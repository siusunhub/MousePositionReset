using System.Drawing;
using System.Windows.Forms;

namespace MousePositionReset;

public class IdentifyMonitorForm : Form
{
    private readonly System.Windows.Forms.Timer _timer;

    public IdentifyMonitorForm(Screen screen)
    {
        int size = ScaleForDpi(190);
        int margin = ScaleForDpi(24);
        var area = screen.WorkingArea;

        this.FormBorderStyle = FormBorderStyle.None;
        this.StartPosition = FormStartPosition.Manual;
        this.Bounds = new Rectangle(
            area.Left + margin,
            area.Bottom - size - margin,
            size,
            size);
        this.BackColor = Color.FromArgb(24, 24, 28);
        this.ForeColor = Color.White;
        this.Opacity = 0.92;
        this.TopMost = true;
        this.ShowInTaskbar = false;

        _timer = new System.Windows.Forms.Timer { Interval = 3000 };
        _timer.Tick += (s, e) =>
        {
            _timer.Stop();
            this.Close();
        };

        this.Load += (s, e) => _timer.Start();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);

        e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        using var borderPen = new Pen(Color.FromArgb(99, 102, 241), ScaleForDpi(4));
        using var iconPen = new Pen(Color.White, ScaleForDpi(8))
        {
            StartCap = System.Drawing.Drawing2D.LineCap.Round,
            EndCap = System.Drawing.Drawing2D.LineCap.Round
        };

        int padding = ScaleForDpi(5);
        e.Graphics.DrawRectangle(borderPen, padding, padding, this.ClientSize.Width - padding * 2 - 1, this.ClientSize.Height - padding * 2 - 1);

        int monitorWidth = ScaleForDpi(105);
        int monitorHeight = ScaleForDpi(68);
        int monitorX = (this.ClientSize.Width - monitorWidth) / 2;
        int monitorY = ScaleForDpi(52);
        var monitorRect = new Rectangle(monitorX, monitorY, monitorWidth, monitorHeight);

        e.Graphics.DrawRectangle(iconPen, monitorRect);

        int standTop = monitorRect.Bottom + ScaleForDpi(12);
        int standCenter = monitorRect.Left + monitorRect.Width / 2;
        e.Graphics.DrawLine(iconPen, standCenter, monitorRect.Bottom, standCenter, standTop);
        e.Graphics.DrawLine(iconPen, standCenter - ScaleForDpi(28), standTop, standCenter + ScaleForDpi(28), standTop);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _timer.Dispose();
        }

        base.Dispose(disposing);
    }

    private int ScaleForDpi(int value) => (int)Math.Round(value * this.DeviceDpi / 96f);
}
