using System.Runtime.InteropServices;

namespace MousePositionReset;

public class HotKeyWindow : NativeWindow, IDisposable
{
    private const int WM_HOTKEY = 0x0312;

    public event Action<int>? HotKeyPressed;

    public HotKeyWindow()
    {
        CreateHandle(new CreateParams());
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WM_HOTKEY)
        {
            int id = m.WParam.ToInt32();
            HotKeyPressed?.Invoke(id);
        }
        base.WndProc(ref m);
    }

    public void Dispose()
    {
        DestroyHandle();
    }
}

public class HotKeyManager : IDisposable
{
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    private const uint MOD_CONTROL = 0x0002;
    private const uint MOD_SHIFT = 0x0004;

    private readonly HotKeyWindow _window;
    private readonly List<int> _registeredIds = new();

    public event Action<int>? HotKeyPressed;

    public HotKeyManager()
    {
        _window = new HotKeyWindow();
        _window.HotKeyPressed += (id) => HotKeyPressed?.Invoke(id);
    }

    public void UnregisterAll()
    {
        foreach (int id in _registeredIds)
        {
            UnregisterHotKey(_window.Handle, id);
        }
        _registeredIds.Clear();
    }

    public bool Register(int id, string modifier, string keyStr)
    {
        uint mod = 0;
        if (modifier.Equals("CTRL", StringComparison.OrdinalIgnoreCase))
        {
            mod = MOD_CONTROL;
        }
        else if (modifier.Equals("CTRL + SHIFT", StringComparison.OrdinalIgnoreCase) || 
                 modifier.Equals("CTRL+SHIFT", StringComparison.OrdinalIgnoreCase))
        {
            mod = MOD_CONTROL | MOD_SHIFT;
        }

        uint vk = GetVirtualKey(keyStr);
        if (vk == 0) return false;

        bool success = RegisterHotKey(_window.Handle, id, mod, vk);
        if (success)
        {
            _registeredIds.Add(id);
        }
        return success;
    }

    private uint GetVirtualKey(string keyStr)
    {
        if (string.IsNullOrEmpty(keyStr)) return 0;

        if (keyStr.Length == 1)
        {
            char c = char.ToUpper(keyStr[0]);
            if ((c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9'))
            {
                return c;
            }
        }
        return 0;
    }

    public void Dispose()
    {
        UnregisterAll();
        _window.Dispose();
    }
}
