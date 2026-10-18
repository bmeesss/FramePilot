using System.Runtime.Versioning;
using FramePilot.Core.Models;
using Microsoft.Win32;

namespace FramePilot.Windows;

/// <summary>
/// Reads static information about the operating system and the hardware.
/// Every value comes from the Windows registry or from the runtime; nothing
/// is guessed.
/// </summary>
public static class WindowsSystemInfoProvider
{
    private const string CurrentVersionKey = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion";
    private const string ProcessorKey = @"HARDWARE\DESCRIPTION\System\CentralProcessor";
    private const string BiosKey = @"HARDWARE\DESCRIPTION\System\BIOS";

    public static SystemInfo Read(long totalPhysicalMemoryBytes = 0)
    {
        var version = Environment.OSVersion.Version;

        var productName = ReadString(CurrentVersionKey, "ProductName");
        var displayVersion = ReadString(CurrentVersionKey, "DisplayVersion");
        var editionId = ReadString(CurrentVersionKey, "EditionID");
        var installationType = ReadString(CurrentVersionKey, "InstallationType");
        var buildNumber = ReadString(CurrentVersionKey, "CurrentBuildNumber");
        var ubr = ReadString(CurrentVersionKey, "UBR");

        var operatingSystem = productName ?? RuntimeInformation.OSDescription;

        // The registry still reports "Windows 10" on Windows 11, the build
        // number is the reliable part.
        if (version.Build >= 22000 && operatingSystem.Contains("Windows 10", StringComparison.OrdinalIgnoreCase))
        {
            operatingSystem = operatingSystem.Replace("Windows 10", "Windows 11", StringComparison.OrdinalIgnoreCase);
        }

        var osVersion = version.Build > 0
            ? $"{version.Major}.{version.Minor}.{version.Build}" + (string.IsNullOrEmpty(ubr) ? string.Empty : $".{ubr}")
            : version.ToString();

        var processorName = ReadString(ProcessorKey + @"\0", "ProcessorNameString");
        var manufacturer = ReadString(BiosKey, "SystemManufacturer");
        var model = ReadString(BiosKey, "SystemProductName");
        var biosVersion = ReadMultiString(BiosKey, "BIOSVersion");

        return new SystemInfo(
            operatingSystem,
            osVersion,
            displayVersion,
            editionId ?? installationType,
            RuntimeInformation.OSArchitecture.ToString(),
            processorName,
            Environment.ProcessorCount,
            CountPhysicalProcessors(),
            totalPhysicalMemoryBytes,
            Environment.MachineName,
            manufacturer,
            model,
            biosVersion);
    }

    private static int? CountPhysicalProcessors() => PhysicalProcessorCounter.GetPhysicalCoreCount();

    [SupportedOSPlatform("windows")]
    private static string? ReadString(string keyPath, string valueName)
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(keyPath);
            return key?.GetValue(valueName)?.ToString();
        }
        catch (Exception)
        {
            return null;
        }
    }

    [SupportedOSPlatform("windows")]
    private static string? ReadMultiString(string keyPath, string valueName)
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(keyPath);
            if (key?.GetValue(valueName) is string[] values && values.Length > 0)
            {
                return string.Join(" ", values);
            }

            return key?.GetValue(valueName)?.ToString();
        }
        catch (Exception)
        {
            return null;
        }
    }
}
