using System.Drawing;
using System.Windows.Forms;

namespace MousePositionReset;

public class CountdownForm : Form
{
    private readonly Label _label;
    private readonly System.Windows.Forms.Timer _timer;
    private int _secondsLeft = 3;
    
    public Point CapturedPosition { get; private set; }
    public Screen CapturedScreen { get; private set; } = Screen.PrimaryScreen ?? Screen.AllScreens[0];

    public CountdownForm()
    {
        this.FormBorderStyle = FormBorderStyle.None;
        this.StartPosition = FormStartPosition.Manual;
        this.Bounds = SystemInformation.VirtualScreen;
        this.BackColor = Color.FromArgb(15, 15, 20);
        this.Opacity = 0.85;
        this.TopMost = true;
        this.ShowInTaskbar = false;
        this.Cursor = Cursors.Cross;

        _label = new Label
        {
            Dock = DockStyle.Fill,
            ForeColor = Color.FromArgb(99, 102, 241), // Modern Indigo Accent
            Font = new Font("Segoe UI Semibold", 28F, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleCenter,
            Text = "Move mouse to target position...\n\nCapturing in 3"
        };
        this.Controls.Add(_label);

        _timer = new System.Windows.Forms.Timer { Interval = 1000 };
        _timer.Tick += Timer_Tick;
        this.Load += (s, e) => {
            // Tick sound for start
            try { Console.Beep(800, 100); } catch {}
            _timer.Start();
        };
    }

    private void Timer_Tick(object? sender, EventArgs e)
    {
        _secondsLeft--;
        if (_secondsLeft > 0)
        {
            try
            {
                Console.Beep(800, 100);
            }
            catch { }
            _label.Text = $"Move mouse to target position...\n\nCapturing in {_secondsLeft}";
        }
        else
        {
            _timer.Stop();
            try
            {
                Console.Beep(1200, 300);
            }
            catch { }
            CapturedPosition = Cursor.Position;
            CapturedScreen = Screen.FromPoint(CapturedPosition);
            this.DialogResult = DialogResult.OK;
            this.Close();
        }
    }
}
