using Microsoft.Win32;

namespace MousePositionReset;

public static class RegistryStartupManager
{
    private const string RegistryKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "MousePositionReset";

    public static bool IsStartupEnabled()
    {
        string? exePath = Environment.ProcessPath;
        if (string.IsNullOrEmpty(exePath)) return false;

        using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath);
        if (key == null) return false;
        string? value = key.GetValue(ValueName) as string;
        return string.Equals(value, exePath, StringComparison.OrdinalIgnoreCase);
    }

    public static bool SetStartup(bool enable)
    {
        string? exePath = Environment.ProcessPath;
        if (string.IsNullOrEmpty(exePath)) return false;

        try
        {
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath, true);
            if (key == null) return false;

            if (enable)
            {
                key.SetValue(ValueName, exePath);
            }
            else
            {
                key.DeleteValue(ValueName, false);
            }

            // Verify if the write/delete was successful (Double confirm)
            using RegistryKey? verifyKey = Registry.CurrentUser.OpenSubKey(RegistryKeyPath);
            if (verifyKey == null) return false;
            string? value = verifyKey.GetValue(ValueName) as string;
            bool success = enable
                ? string.Equals(value, exePath, StringComparison.OrdinalIgnoreCase)
                : value == null;

            return success;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to configure registry startup: {ex.Message}", "Registry Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return false;
        }
    }
}
