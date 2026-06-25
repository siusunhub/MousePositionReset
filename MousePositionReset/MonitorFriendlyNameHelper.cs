using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;

namespace MousePositionReset;

public class MonitorInfo
{
    public string DeviceName { get; set; } = "";     // GDI Name, e.g., \\.\DISPLAY1
    public string FriendlyName { get; set; } = "";   // EDID Name, e.g., LG FULL HD
    public int PhysicalWidth { get; set; }           // Physical width (active resolution)
    public int PhysicalHeight { get; set; }          // Physical height (active resolution)
}

public static class MonitorFriendlyNameHelper
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct DISPLAY_DEVICE
    {
        public int cb;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string DeviceName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string DeviceString;
        public int StateFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string DeviceID;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string DeviceKey;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct DEVMODE
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string dmDeviceName;
        public ushort dmSpecVersion;
        public ushort dmDriverVersion;
        public ushort dmSize;
        public ushort dmDriverExtra;
        public uint dmFields;
        public int dmPositionX;
        public int dmPositionY;
        public uint dmDisplayOrientation;
        public uint dmDisplayFixedOutput;
        public short dmColor;
        public short dmDuplex;
        public short dmYResolution;
        public short dmTTOption;
        public short dmCollate;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string dmFormName;
        public ushort dmLogPixels;
        public uint dmBitsPerPel;
        public uint dmPelsWidth;
        public uint dmPelsHeight;
        public uint dmDisplayFlags;
        public uint dmNup;
        public uint dmDisplayFrequency;
        public uint dmICMMethod;
        public uint dmICMIntent;
        public uint dmMediaType;
        public uint dmDitherType;
        public uint dmReserved1;
        public uint dmReserved2;
        public uint dmPanningWidth;
        public uint dmPanningHeight;
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern bool EnumDisplayDevices(string? lpDevice, uint iDevNum, ref DISPLAY_DEVICE lpDisplayDevice, uint dwFlags);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern bool EnumDisplaySettings(string? lpszDeviceName, int iModeNum, ref DEVMODE lpDevMode);

    private const int ENUM_CURRENT_SETTINGS = -1;
    private const int DISPLAY_DEVICE_ATTACHED_TO_DESKTOP = 0x1;

    public static List<MonitorInfo> GetActiveMonitors()
    {
        var list = new List<MonitorInfo>();
        
        // 1. Gather friendly hardware names from WMI WmiMonitorID
        var wmiNamesByHardwareId = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            using var searcher = new ManagementObjectSearcher(@"Root\WMI", "SELECT * FROM WmiMonitorID");
            foreach (ManagementBaseObject queryObj in searcher.Get())
            {
                string? instanceName = queryObj["InstanceName"] as string;
                if (string.IsNullOrEmpty(instanceName)) continue;

                // Extract hardware code from instance ID (e.g. DISPLAY\BNQ7950\...)
                string[] parts = instanceName.Split('\\');
                if (parts.Length > 1)
                {
                    string hardwareId = parts[1];
                    ushort[]? nameArr = queryObj["UserFriendlyName"] as ushort[];
                    if (nameArr != null)
                    {
                        string friendlyName = new string(nameArr.Select(x => (char)x).ToArray()).TrimEnd('\0');
                        if (!string.IsNullOrEmpty(friendlyName))
                        {
                            wmiNamesByHardwareId[hardwareId] = friendlyName;
                        }
                    }
                }
            }
        }
        catch
        {
            // Fail silently, WMI query is optional
        }

        // 2. Query actual displays and physical active resolutions
        try
        {
            var adapter = new DISPLAY_DEVICE();
            adapter.cb = Marshal.SizeOf(adapter);

            uint adapterIndex = 0;
            while (EnumDisplayDevices(null, adapterIndex, ref adapter, 0))
            {
                if ((adapter.StateFlags & DISPLAY_DEVICE_ATTACHED_TO_DESKTOP) != 0)
                {
                    var monitor = new DISPLAY_DEVICE();
                    monitor.cb = Marshal.SizeOf(monitor);

                    uint monitorIndex = 0;
                    if (EnumDisplayDevices(adapter.DeviceName, monitorIndex, ref monitor, 0))
                    {
                        string gdiName = adapter.DeviceName;
                        string friendlyName = monitor.DeviceString; // Initial fallback

                        // Match using the PNP hardware manufacturer ID
                        if (!string.IsNullOrEmpty(monitor.DeviceID))
                        {
                            string[] parts = monitor.DeviceID.Split('\\');
                            if (parts.Length > 1)
                            {
                                string hardwareId = parts[1];
                                if (wmiNamesByHardwareId.TryGetValue(hardwareId, out string? wmiFriendlyName))
                                {
                                    friendlyName = wmiFriendlyName;
                                }
                            }
                        }

                        // Query physical active resolution
                        int width = 0;
                        int height = 0;
                        var dm = new DEVMODE();
                        dm.dmSize = (ushort)Marshal.SizeOf(dm);
                        if (EnumDisplaySettings(adapter.DeviceName, ENUM_CURRENT_SETTINGS, ref dm))
                        {
                            width = (int)dm.dmPelsWidth;
                            height = (int)dm.dmPelsHeight;
                        }

                        list.Add(new MonitorInfo
                        {
                            DeviceName = gdiName,
                            FriendlyName = friendlyName,
                            PhysicalWidth = width,
                            PhysicalHeight = height
                        });
                    }
                }

                adapterIndex++;
                adapter = new DISPLAY_DEVICE();
                adapter.cb = Marshal.SizeOf(adapter);
            }
        }
        catch
        {
            // Fail silently
        }

        return list;
    }
}
