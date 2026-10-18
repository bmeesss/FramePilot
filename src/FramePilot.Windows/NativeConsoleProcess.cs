using System.Runtime.InteropServices;
using System.Text;

namespace FramePilot.Windows;

/// <summary>
/// Starts a console program in its own (hidden) console and can ask it to
/// terminate cleanly.
/// </summary>
/// <remarks>
/// <para>
/// The process is created with <c>CREATE_NEW_CONSOLE</c> and
/// <c>CREATE_NEW_PROCESS_GROUP</c>, which gives it a console of its own. The
/// console window is hidden from the start, so no window flashes up.
/// </para>
/// <para>
/// The <c>CTRL_BREAK_EVENT</c> is sent to the process group of the child
/// only. PresentMon handles that event by stopping the recording and running
/// its normal shutdown, so the CSV file is closed completely instead of being
/// cut in the middle of a row.
/// </para>
/// </remarks>
public static class NativeConsoleProcess
{
    private const uint CreateNewConsole = 0x00000010;
    private const uint CreateNewProcessGroup = 0x00000200;
    private const uint CreateUnicodeEnvironment = 0x00000400;
    private const uint StartfUseShowWindow = 0x00000001;
    private const short SwHide = 0;
    private const uint CtrlBreakEvent = 1;

    /// <summary>
    /// Starts the program and returns its process id, or <c>null</c> when the
    /// process could not be created.
    /// </summary>
    public static int? Start(string fileName, IReadOnlyList<string> arguments)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentNullException.ThrowIfNull(arguments);

        var commandLine = new StringBuilder();
        commandLine.Append('"').Append(fileName).Append('"');

        foreach (var argument in arguments)
        {
            commandLine.Append(' ').Append(QuoteArgument(argument));
        }

        var startupInfo = new STARTUPINFO
        {
            cb = Marshal.SizeOf<STARTUPINFO>(),
            dwFlags = StartfUseShowWindow,
            wShowWindow = SwHide,
        };

        var processInfo = new PROCESS_INFORMATION();

        var created = CreateProcessW(
            null,
            commandLine,
            IntPtr.Zero,
            IntPtr.Zero,
            false,
            CreateNewConsole | CreateNewProcessGroup | CreateUnicodeEnvironment,
            IntPtr.Zero,
            null,
            ref startupInfo,
            out processInfo);

        if (!created)
        {
            return null;
        }

        try
        {
            return processInfo.dwProcessId;
        }
        finally
        {
            if (processInfo.hThread != IntPtr.Zero)
            {
                CloseHandle(processInfo.hThread);
            }

            if (processInfo.hProcess != IntPtr.Zero)
            {
                CloseHandle(processInfo.hProcess);
            }
        }
    }

    /// <summary>
    /// Asks the process to stop by sending a console break event to its
    /// process group. Returns false when the event could not be sent.
    /// </summary>
    public static bool TryRequestStop(int processId)
    {
        if (processId <= 0)
        {
            return false;
        }

        if (!AttachConsole((uint)processId))
        {
            return false;
        }

        try
        {
            return GenerateConsoleCtrlEvent(CtrlBreakEvent, (uint)processId);
        }
        finally
        {
            FreeConsole();
        }
    }

    /// <summary>
    /// Quotes an argument following the rules of the Windows command line
    /// parser.
    /// </summary>
    public static string QuoteArgument(string? argument)
    {
        if (argument is null)
        {
            return "\"\"";
        }

        if (argument.Length > 0 && argument.IndexOfAny(new[] { ' ', '\t', '"' }) < 0)
        {
            return argument;
        }

        var builder = new StringBuilder("\"");
        var backslashes = 0;

        foreach (var c in argument)
        {
            if (c == '\\')
            {
                backslashes++;
                continue;
            }

            if (c == '"')
            {
                builder.Append('\\', (backslashes * 2) + 1);
                builder.Append('"');
                backslashes = 0;
                continue;
            }

            builder.Append('\\', backslashes);
            backslashes = 0;
            builder.Append(c);
        }

        builder.Append('\\', backslashes * 2);
        builder.Append('"');

        return builder.ToString();
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct STARTUPINFO
    {
        public int cb;
        public IntPtr lpReserved;
        public IntPtr lpDesktop;
        public IntPtr lpTitle;
        public int dwX;
        public int dwY;
        public int dwXSize;
        public int dwYSize;
        public int dwXCountChars;
        public int dwYCountChars;
        public int dwFillAttribute;
        public uint dwFlags;
        public short wShowWindow;
        public short cbReserved2;
        public IntPtr lpReserved2;
        public IntPtr hStdInput;
        public IntPtr hStdOutput;
        public IntPtr hStdError;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PROCESS_INFORMATION
    {
        public IntPtr hProcess;
        public IntPtr hThread;
        public int dwProcessId;
        public int dwThreadId;
    }

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreateProcessW(
        string? lpApplicationName,
        StringBuilder lpCommandLine,
        IntPtr lpProcessAttributes,
        IntPtr lpThreadAttributes,
        [MarshalAs(UnmanagedType.Bool)] bool bInheritHandles,
        uint dwCreationFlags,
        IntPtr lpEnvironment,
        string? lpCurrentDirectory,
        ref STARTUPINFO lpStartupInfo,
        out PROCESS_INFORMATION lpProcessInformation);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr hObject);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AttachConsole(uint dwProcessId);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool FreeConsole();

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GenerateConsoleCtrlEvent(uint dwCtrlEvent, uint dwProcessGroupId);
}
