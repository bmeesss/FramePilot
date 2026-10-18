using System.Runtime.InteropServices;

namespace FramePilot.Windows;

/// <summary>
/// Counts the physical processor cores with the documented
/// <c>GetLogicalProcessorInformationEx</c> API.
/// </summary>
/// <remarks>
/// The <c>CentralProcessor</c> registry key contains one entry per <em>logical</em>
/// processor, so it cannot be used to report the number of physical cores.
/// </remarks>
public static class PhysicalProcessorCounter
{
    private const int RelationProcessorCore = 9;
    private const int LtpRelationshipAllCores = 0x200;

    public static int? GetPhysicalCoreCount()
    {
        try
        {
            uint length = 0;

            // The first call fails and reports the required buffer size.
            GetLogicalProcessorInformationEx(RelationProcessorCore, IntPtr.Zero, ref length);

            if (length <= 0)
            {
                return null;
            }

            var buffer = Marshal.AllocHGlobal((int)length);

            try
            {
                if (!GetLogicalProcessorInformationEx(RelationProcessorCore, buffer, ref length))
                {
                    return null;
                }

                var offset = 0;
                var count = 0;

                while (offset + 8 <= (int)length)
                {
                    var relationship = Marshal.ReadInt32(buffer, offset);
                    var size = Marshal.ReadInt32(buffer, offset + 4);

                    if (size <= 0)
                    {
                        break;
                    }

                    if (relationship == RelationProcessorCore)
                    {
                        var flags = Marshal.ReadInt32(buffer, offset + 8);
                        if ((flags & LtpRelationshipAllCores) != 0)
                        {
                            count++;
                        }
                    }

                    offset += size;
                }

                return count > 0 ? count : null;
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }
        catch (Exception)
        {
            return null;
        }
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetLogicalProcessorInformationEx(
        int relationship,
        IntPtr buffer,
        ref uint returnedLength);
}
