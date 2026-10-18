using System.Diagnostics;
using System.Runtime.InteropServices;
using FramePilot.Core.Abstractions;
using FramePilot.Core.Metrics;
using FramePilot.Core.Models;

namespace FramePilot.Windows;

/// <summary>
/// Reads the real system counters from Windows.
/// </summary>
/// <remarks>
/// CPU utilisation is derived from <c>GetSystemTimes</c>, memory from
/// <c>GlobalMemoryStatusEx</c> and the drive information from
/// <c>GetDiskFreeSpaceEx</c> (via <see cref="DriveInfo"/>). No value is
/// estimated or simulated.
/// </remarks>
public sealed class WindowsSystemMetricsSource : ISystemMetricsSource
{
    public ProcessorTimes GetProcessorTimes()
    {
        if (!GetSystemTimes(out var idle, out var kernel, out var user))
        {
            return default;
        }

        return new ProcessorTimes(
            FileTimeToTimeSpan(idle),
            FileTimeToTimeSpan(kernel),
            FileTimeToTimeSpan(user));
    }

    public MemorySnapshot GetMemory()
    {
        var status = new MEMORYSTATUSEX
        {
            dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>(),
        };

        if (!GlobalMemoryStatusEx(ref status))
        {
            return MemorySnapshot.Unavailable;
        }

        return MemoryCalculator.Compute(
            (long)status.ullTotalPhys,
            (long)status.ullAvailPhys);
    }

    public IReadOnlyList<DiskSnapshot> GetDisks()
    {
        var result = new List<DiskSnapshot>();

        DriveInfo[] drives;
        try
        {
            drives = DriveInfo.GetDrives();
        }
        catch (Exception)
        {
            return result;
        }

        foreach (var drive in drives)
        {
            try
            {
                if (!drive.IsReady)
                {
                    continue;
                }

                if (drive.DriveType is not (DriveType.Fixed or DriveType.Removable))
                {
                    continue;
                }

                var total = drive.TotalSize;
                var available = drive.AvailableFreeSpace;
                var used = total - available;

                result.Add(new DiskSnapshot(
                    drive.Name,
                    drive.DriveType.ToString(),
                    total,
                    available,
                    used,
                    total > 0 ? 100.0 * used / total : 0));
            }
            catch (Exception)
            {
                // A drive that disappears or is not ready is skipped.
            }
        }

        return result;
    }

    public IReadOnlyList<ProcessCpuSample> GetProcessSamples()
    {
        var result = new List<ProcessCpuSample>();

        Process[] processes;
        try
        {
            processes = Process.GetProcesses();
        }
        catch (Exception)
        {
            return result;
        }

        foreach (var process in processes)
        {
            try
            {
                var processorTime = process.TotalProcessorTime;
                var workingSet = process.WorkingSet64;
                var privateBytes = process.PrivateMemorySize64;
                var hasMainWindow = process.MainWindowHandle != IntPtr.Zero;

                string? path = null;
                try
                {
                    path = process.MainModule?.FileName;
                }
                catch (Exception)
                {
                    // Access to the main module can be denied for protected
                    // processes; the rest of the information is still usable.
                }

                string name;
                try
                {
                    name = process.ProcessName;
                }
                catch (Exception)
                {
                    name = "<unknown>";
                }

                result.Add(new ProcessCpuSample(
                    ProcessId: process.Id,
                    Name: name,
                    ExecutablePath: path,
                    ProcessorTime: processorTime,
                    WorkingSetBytes: workingSet,
                    PrivateBytes: privateBytes,
                    HasExited: false,
                    HasMainWindow: hasMainWindow));
            }
            catch (Exception)
            {
                // The process exited while it was being inspected.
            }
            finally
            {
                process.Dispose();
            }
        }

        return result;
    }

    public SystemInfo GetSystemInfo() => WindowsSystemInfoProvider.Read(GetMemory().TotalBytes);

    private static TimeSpan FileTimeToTimeSpan(FILETIME fileTime)
    {
        long value = ((long)fileTime.dwHighDateTime << 32) | fileTime.dwLowDateTime;
        return TimeSpan.FromTicks(value * 10);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct FILETIME
    {
        public uint dwLowDateTime;
        public uint dwHighDateTime;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MEMORYSTATUSEX
    {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetSystemTimes(out FILETIME lpIdleTime, out FILETIME lpKernelTime, out FILETIME lpUserTime);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);
}
