using System.Diagnostics;
using System.Security.Principal;
using System.Threading;

namespace MousePositionReset;

static class Program
{
    private static Mutex? _mutex;

    /// <summary>
    ///  The main entry point for the application.
    /// </summary>
    [STAThread]
    static void Main(string[] args)
    {
        WaitForPreviousProcessExit(args);

        AppSettings startupSettings = AppSettings.Load();
        if (startupSettings.RunAsAdministrator && !IsRunningAsAdministrator())
        {
            RestartApplication(runAsAdministrator: true);
            return;
        }

        // Single instance check
        const string mutexName = "Global\\MousePositionReset_Mutex_Unique_12345";
        _mutex = new Mutex(true, mutexName, out bool createdNew);
        if (!createdNew)
        {
            MessageBox.Show("Mouse Position Reset is already running.", "Already Running", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        ApplicationConfiguration.Initialize();

        // Check if config file exists
        string configPath = AppSettings.GetConfigPath();
        bool configExists = File.Exists(configPath);

        var mainForm = new Form1();
        if (configExists)
        {
            mainForm.StartMinimized = true;
        }

        Application.Run(mainForm);
    }

    internal static bool IsRunningAsAdministrator()
    {
        using WindowsIdentity identity = WindowsIdentity.GetCurrent();
        WindowsPrincipal principal = new(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }

    internal static bool RestartApplication(bool runAsAdministrator, bool waitForCurrentProcessToExit = false)
    {
        string? exePath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(exePath))
        {
            return false;
        }

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = exePath,
                UseShellExecute = true,
                WorkingDirectory = AppContext.BaseDirectory
            };

            if (waitForCurrentProcessToExit)
            {
                startInfo.Arguments = $"--wait-for-process-exit={Environment.ProcessId}";
            }

            if (runAsAdministrator && !IsRunningAsAdministrator())
            {
                startInfo.Verb = "runas";
            }

            Process.Start(startInfo);
            return true;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return false;
        }
        catch
        {
            return false;
        }
    }

    private static void WaitForPreviousProcessExit(string[] args)
    {
        const string argumentPrefix = "--wait-for-process-exit=";
        string? waitArgument = args.FirstOrDefault(arg => arg.StartsWith(argumentPrefix, StringComparison.OrdinalIgnoreCase));
        if (waitArgument == null)
        {
            return;
        }

        string processIdText = waitArgument[argumentPrefix.Length..];
        if (!int.TryParse(processIdText, out int processId) || processId <= 0 || processId == Environment.ProcessId)
        {
            return;
        }

        try
        {
            using Process process = Process.GetProcessById(processId);
            process.WaitForExit(10000);
        }
        catch
        {
        }
    }

    internal static void ReleaseSingleInstanceMutex()
    {
        if (_mutex == null)
        {
            return;
        }

        try
        {
            _mutex.ReleaseMutex();
        }
        catch (ApplicationException)
        {
        }

        _mutex.Dispose();
        _mutex = null;
    }
}
