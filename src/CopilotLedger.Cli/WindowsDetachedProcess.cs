using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Win32.SafeHandles;

namespace CopilotLedger.Cli;

// Starts a process that inherits exactly one handle: an inheritable handle to the NUL device, used as its standard
// input, output, and error. Process.Start passes bInheritHandles = TRUE without a handle list, so the child would
// inherit every inheritable handle in this process, including the caller's output pipe, and a caller that reads that
// pipe to the end (x=$(gh-ledger daemon start)) would wait until the daemon exits.
[SupportedOSPlatform("windows")]
internal static class WindowsDetachedProcess
{
    private const int StartfUseStdHandles = 0x00000100;
    private const uint CreateNoWindow = 0x08000000;
    private const uint ExtendedStartupInfoPresent = 0x00080000;
    private const uint CreateBreakawayFromJob = 0x01000000;
    private const int ErrorInsufficientBuffer = 122;
    private static readonly IntPtr ProcThreadAttributeHandleList = 0x00020002;

    public static int Start(
        string fileName,
        IReadOnlyList<string> arguments,
        string workingDirectory,
        bool breakawayFromJob)
    {
        using SafeFileHandle nul = File.OpenHandle(
            @"\\.\NUL",
            FileMode.Open,
            FileAccess.ReadWrite,
            FileShare.ReadWrite | FileShare.Inheritable);
        IntPtr size = IntPtr.Zero;
        if (InitializeProcThreadAttributeList(IntPtr.Zero, 1, 0, ref size)
            || Marshal.GetLastPInvokeError() != ErrorInsufficientBuffer)
        {
            throw new Win32Exception();
        }

        IntPtr attributeList = Marshal.AllocHGlobal(size);
        IntPtr handleList = Marshal.AllocHGlobal(IntPtr.Size);
        IntPtr commandLine = Marshal.StringToHGlobalUni(
            WindowsCommandLine.Build(fileName, arguments));
        bool initialized = false;
        try
        {
            if (!InitializeProcThreadAttributeList(attributeList, 1, 0, ref size))
            {
                throw new Win32Exception();
            }

            initialized = true;
            Marshal.WriteIntPtr(handleList, nul.DangerousGetHandle());
            if (!UpdateProcThreadAttribute(
                    attributeList,
                    0,
                    ProcThreadAttributeHandleList,
                    handleList,
                    IntPtr.Size,
                    IntPtr.Zero,
                    IntPtr.Zero))
            {
                throw new Win32Exception();
            }

            StartupInfoEx startupInfo = new()
            {
                StartupInfo = new StartupInfo
                {
                    Size = Marshal.SizeOf<StartupInfoEx>(),
                    Flags = StartfUseStdHandles,
                    StandardInput = nul.DangerousGetHandle(),
                    StandardOutput = nul.DangerousGetHandle(),
                    StandardError = nul.DangerousGetHandle(),
                },
                AttributeList = attributeList,
            };
            // A job that kills on close would otherwise stop the daemon with the caller (#78).
            if (!CreateProcess(
                    null,
                    commandLine,
                    IntPtr.Zero,
                    IntPtr.Zero,
                    true,
                    CreateNoWindow
                    | ExtendedStartupInfoPresent
                    | (breakawayFromJob ? CreateBreakawayFromJob : 0),
                    IntPtr.Zero,
                    workingDirectory,
                    ref startupInfo,
                    out ProcessInformation process))
            {
                throw new Win32Exception();
            }

            CloseHandle(process.Thread);
            CloseHandle(process.Process);
            return process.ProcessId;
        }
        finally
        {
            if (initialized)
            {
                DeleteProcThreadAttributeList(attributeList);
            }

            Marshal.FreeHGlobal(commandLine);
            Marshal.FreeHGlobal(handleList);
            Marshal.FreeHGlobal(attributeList);
        }
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool InitializeProcThreadAttributeList(
        IntPtr attributeList,
        int attributeCount,
        int flags,
        ref IntPtr size);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UpdateProcThreadAttribute(
        IntPtr attributeList,
        uint flags,
        IntPtr attribute,
        IntPtr value,
        IntPtr size,
        IntPtr previousValue,
        IntPtr returnSize);

    [DllImport("kernel32.dll")]
    private static extern void DeleteProcThreadAttributeList(IntPtr attributeList);

    [DllImport("kernel32.dll", EntryPoint = "CreateProcessW", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreateProcess(
        string? applicationName,
        IntPtr commandLine,
        IntPtr processAttributes,
        IntPtr threadAttributes,
        [MarshalAs(UnmanagedType.Bool)] bool inheritHandles,
        uint creationFlags,
        IntPtr environment,
        string? currentDirectory,
        ref StartupInfoEx startupInfo,
        out ProcessInformation processInformation);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr handle);

    [StructLayout(LayoutKind.Sequential)]
    private struct StartupInfo
    {
        public int Size;
        public IntPtr Reserved;
        public IntPtr Desktop;
        public IntPtr Title;
        public int X;
        public int Y;
        public int XSize;
        public int YSize;
        public int XCountChars;
        public int YCountChars;
        public int FillAttribute;
        public int Flags;
        public short ShowWindow;
        public short Reserved2Size;
        public IntPtr Reserved2;
        public IntPtr StandardInput;
        public IntPtr StandardOutput;
        public IntPtr StandardError;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct StartupInfoEx
    {
        public StartupInfo StartupInfo;
        public IntPtr AttributeList;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ProcessInformation
    {
        public IntPtr Process;
        public IntPtr Thread;
        public int ProcessId;
        public int ThreadId;
    }
}
