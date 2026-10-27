using System.Drawing;
using System.Runtime.InteropServices;

namespace TopMaskAppBar;

internal sealed record DisplayTarget(string DeviceName, string FriendlyName, Rectangle Bounds, bool IsPrimary, bool IsInternal)
{
    public string Label
    {
        get
        {
            var kind = IsInternal ? "內建螢幕" : IsPrimary ? "主要螢幕" : "外接螢幕";
            return $"{kind} — {FriendlyName} ({Bounds.Width} × {Bounds.Height})";
        }
    }

    public override string ToString() => Label;
}

internal static class DisplayCatalog
{
    private sealed record DisplayConnection(string FriendlyName, bool IsInternal);

    public static IReadOnlyList<DisplayTarget> GetDisplays()
    {
        var connections = GetActiveDisplayConnections();
        var displays = new List<DisplayTarget>();
        NativeMethods.MonitorEnumProc callback = (monitor, _, _, _) =>
        {
            var info = new NativeMethods.MonitorInfoEx
            {
                Size = (uint)Marshal.SizeOf<NativeMethods.MonitorInfoEx>(),
                DeviceName = new string('\0', 32)
            };

            if (NativeMethods.GetMonitorInfo(monitor, ref info))
            {
                var rect = info.Monitor;
                var bounds = Rectangle.FromLTRB(rect.Left, rect.Top, rect.Right, rect.Bottom);
                var connection = connections.GetValueOrDefault(info.DeviceName);
                var friendlyName = string.IsNullOrWhiteSpace(connection?.FriendlyName)
                    ? info.DeviceName
                    : connection.FriendlyName;

                displays.Add(new DisplayTarget(
                    info.DeviceName,
                    friendlyName,
                    bounds,
                    (info.Flags & 1) != 0,
                    connection?.IsInternal ?? false));
            }
            return true;
        };

        if (!NativeMethods.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, callback, IntPtr.Zero))
            throw new InvalidOperationException($"無法列舉顯示器 (Win32 {Marshal.GetLastWin32Error()})。");

        return displays.OrderByDescending(display => display.IsInternal)
            .ThenByDescending(display => display.IsPrimary)
            .ThenBy(display => display.DeviceName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static Dictionary<string, DisplayConnection> GetActiveDisplayConnections()
    {
        var result = new Dictionary<string, DisplayConnection>(StringComparer.OrdinalIgnoreCase);

        for (var attempt = 0; attempt < 4; attempt++)
        {
            var status = NativeMethods.GetDisplayConfigBufferSizes(NativeMethods.QdcOnlyActivePaths, out var pathCount, out var modeCount);
            if (status != 0 || pathCount == 0)
                return result;

            var paths = new NativeMethods.DisplayConfigPathInfo[pathCount];
            var modeBytes = checked((int)Math.Max(1, modeCount) * 64);
            var modes = Marshal.AllocHGlobal(modeBytes);
            try
            {
                status = NativeMethods.QueryDisplayConfig(NativeMethods.QdcOnlyActivePaths,
                    ref pathCount, paths, ref modeCount, modes, IntPtr.Zero);
            }
            finally
            {
                Marshal.FreeHGlobal(modes);
            }

            if (status == NativeMethods.ErrorInsufficientBuffer)
                continue;
            if (status != 0)
                return result;

            for (var index = 0; index < pathCount; index++)
            {
                var path = paths[index];
                var sourceNameRequest = new NativeMethods.DisplayConfigSourceDeviceName
                {
                    Header = new NativeMethods.DisplayConfigDeviceInfoHeader
                    {
                        Type = NativeMethods.DisplayConfigGetSourceName,
                        Size = (uint)Marshal.SizeOf<NativeMethods.DisplayConfigSourceDeviceName>(),
                        AdapterId = path.SourceInfo.AdapterId,
                        Id = path.SourceInfo.Id
                    },
                    ViewGdiDeviceName = new string('\0', 32)
                };

                if (NativeMethods.DisplayConfigGetDeviceInfo(ref sourceNameRequest) != 0 ||
                    string.IsNullOrWhiteSpace(sourceNameRequest.ViewGdiDeviceName))
                    continue;

                var targetNameRequest = new NativeMethods.DisplayConfigTargetDeviceName
                {
                    Header = new NativeMethods.DisplayConfigDeviceInfoHeader
                    {
                        Type = NativeMethods.DisplayConfigGetTargetName,
                        Size = (uint)Marshal.SizeOf<NativeMethods.DisplayConfigTargetDeviceName>(),
                        AdapterId = path.TargetInfo.AdapterId,
                        Id = path.TargetInfo.Id
                    },
                    MonitorFriendlyDeviceName = new string('\0', 64),
                    MonitorDevicePath = new string('\0', 128)
                };

                var hasTargetName = NativeMethods.DisplayConfigGetDeviceInfo(ref targetNameRequest) == 0;
                var technology = path.TargetInfo.OutputTechnology;
                var internalPanel = technology is NativeMethods.DisplayConfigOutputTechnologyLvds
                    or NativeMethods.DisplayConfigOutputTechnologyDisplayPortEmbedded
                    or NativeMethods.DisplayConfigOutputTechnologyUdiEmbedded
                    or NativeMethods.DisplayConfigOutputTechnologyInternal;
                var name = hasTargetName ? targetNameRequest.MonitorFriendlyDeviceName?.TrimEnd('\0', ' ') : null;

                result[sourceNameRequest.ViewGdiDeviceName.TrimEnd('\0', ' ')] =
                    new DisplayConnection(string.IsNullOrWhiteSpace(name) ? sourceNameRequest.ViewGdiDeviceName.TrimEnd('\0', ' ') : name, internalPanel);
            }

            return result;
        }

        return result;
    }
}
