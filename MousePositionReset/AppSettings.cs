using System.Text.Json;
using System.Text.Json.Serialization;

namespace MousePositionReset;

public class AppSettings
{
    public bool RunAtStartup { get; set; }
    public bool RunAsAdministrator { get; set; }
    public ResizeWindowHotkeyConfig ResizeWindowHotkey { get; set; } = new();
    public List<ShortcutConfig> Shortcuts { get; set; } = new();
    public List<CycleConfig> Cycle { get; set; } = new();

    public static string GetConfigPath()
    {
        string exePath = Environment.ProcessPath ?? AppDomain.CurrentDomain.BaseDirectory;
        return Path.ChangeExtension(exePath, ".json");
    }

    public static AppSettings Load()
    {
        string path = GetConfigPath();
        if (!File.Exists(path))
        {
            return new AppSettings();
        }

        try
        {
            string json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
        }
        catch
        {
            return new AppSettings();
        }
    }

    public bool Save()
    {
        string path = GetConfigPath();
        try
        {
            string json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(path, json);
            return true;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to save settings: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return false;
        }
    }
}

public class ResizeWindowHotkeyConfig
{
    public string Modifier { get; set; } = "CTRL";
    public string Key { get; set; } = "R";
}

public class ShortcutConfig
{
    public string Modifier { get; set; } = "Ctrl"; // "Ctrl" or "Ctrl+Shift"
    public string Key { get; set; } = "A";         // A-Z, 0-9
    public string MonitorName { get; set; } = "";
    [JsonIgnore]
    public string MonitorDeviceName { get; set; } = "";
    public string MonitorExactName { get; set; } = "";
    public int MonitorWidth { get; set; }
    public int MonitorHeight { get; set; }
    public bool IsCenter { get; set; } = true;
    public int X { get; set; }
    public int Y { get; set; }
    public bool IsCycle { get; set; } = false;
    [JsonIgnore]
    public List<CycleMonitorInfo> MonitorSequence { get; set; } = new();

    public override bool Equals(object? obj)
    {
        if (obj is ShortcutConfig other)
        {
            return string.Equals(Modifier, other.Modifier, StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(Key, other.Key, StringComparison.OrdinalIgnoreCase);
        }
        return false;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(
            Modifier.ToLowerInvariant(),
            Key.ToLowerInvariant()
        );
    }
}

public class CycleConfig
{
    public string Modifier { get; set; } = "Ctrl";
    public string Key { get; set; } = "A";
    public List<CycleMonitorInfo> MonitorSequence { get; set; } = new();
}

public class CycleMonitorInfo
{
    public string MonitorName { get; set; } = "";
    public string MonitorDeviceName { get; set; } = "";
}
