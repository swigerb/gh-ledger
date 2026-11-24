using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace CopilotLedger.Tests.TestSupport;

[SupportedOSPlatform("windows")]
internal static class WindowsJobHarness
{
    private const uint WaitObject0 = 0;
    private const uint WaitTimeout = 0x102;
    private const uint CreateSuspended = 0x4;
    private const uint CreateNoWindow = 0x08000000;
    private const uint CreateUnicodeEnvironment = 0x400;
    private const int StartfUseStdHandles = 0x00000100;

    // Runs fileName with arguments, suspended, in a new job whose extended limit flags are limitFlags; resumes it; waits
    // up to timeout for it to exit; then closes the job handle (which kills what's left in a KILL_ON_JOB_CLOSE job).
    // environment is the child's whole environment. Its standard output and error go to outputPath. Returns its exit code.
    public static int RunInJobThenCloseJob(
        string fileName, IReadOnlyList<string> arguments, IReadOnlyDictionary<string, string> environment,
        uint limitFlags, string outputPath, TimeSpan timeout)
    {
        IntPtr job = CreateJobObjectW(IntPtr.Zero, null);
        if (job == IntPtr.Zero)
        {
            throw new Win32Exception();
        }

        try
        {
            JobObjectExtendedLimitInformation limits = new()
            {
                BasicLimitInformation = new JobObjectBasicLimitInformation
                {
                    LimitFlags = limitFlags,
                },
            };
            if (!SetInformationJobObject(
                    job,
                    9,
                    ref limits,
                    Marshal.SizeOf<JobObjectExtendedLimitInformation>()))
            {
                throw new Win32Exception();
            }

            string commandLine = "\"" + fileName + "\" " + string.Join(' ', arguments);
            IntPtr commandLinePointer = Marshal.StringToHGlobalUni(commandLine);
            IntPtr environmentPointer = BuildEnvironmentBlock(environment);
            try
            {
                using SafeFileHandle output = File.OpenHandle(
                    outputPath,
                    FileMode.Create,
                    FileAccess.Write,
                    FileShare.ReadWrite | FileShare.Inheritable);
                StartupInfo startupInfo = new()
                {
                    Size = Marshal.SizeOf<StartupInfo>(),
                    Flags = StartfUseStdHandles,
                    StandardInput = output.DangerousGetHandle(),
                    StandardOutput = output.DangerousGetHandle(),
                    StandardError = output.DangerousGetHandle(),
                };
                if (!CreateProcessW(
                        null,
                        commandLinePointer,
                        IntPtr.Zero,
                        IntPtr.Zero,
                        true,
                        CreateSuspended | CreateNoWindow | CreateUnicodeEnvironment,
                        environmentPointer,
                        null,
                        ref startupInfo,
                        out ProcessInformation process))
                {
                    throw new Win32Exception();
                }

                try
                {
                    if (!AssignProcessToJobObject(job, process.Process))
                    {
                        throw new Win32Exception();
                    }

                    if (ResumeThread(process.Thread) == uint.MaxValue)
                    {
                        throw new Win32Exception();
                    }

                    uint milliseconds = checked((uint)timeout.TotalMilliseconds);
                    uint wait = WaitForSingleObject(process.Process, milliseconds);
                    if (wait == WaitTimeout)
                    {
                        throw new TimeoutException();
                    }

                    if (wait != WaitObject0
                        || !GetExitCodeProcess(process.Process, out int exitCode))
                    {
                        throw new Win32Exception();
                    }

                    return exitCode;
                }
                finally
                {
                    CloseHandle(process.Thread);
                    CloseHandle(process.Process);
                }
            }
            finally
            {
                Marshal.FreeHGlobal(commandLinePointer);
                Marshal.FreeHGlobal(environmentPointer);
            }
        }
        finally
        {
            CloseHandle(job);
        }
    }

    private static IntPtr BuildEnvironmentBlock(
        IReadOnlyDictionary<string, string> environment)
    {
        StringBuilder builder = new();
        foreach (KeyValuePair<string, string> variable in
                 environment.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            builder.Append(variable.Key);
            builder.Append('=');
            builder.Append(variable.Value);
            builder.Append('\0');
        }

        builder.Append('\0');
        char[] characters = builder.ToString().ToCharArray();
        IntPtr block = Marshal.AllocHGlobal(characters.Length * sizeof(char));
        Marshal.Copy(characters, 0, block, characters.Length);
        return block;
    }

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr CreateJobObjectW(
        IntPtr jobAttributes,
        string? name);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetInformationJobObject(
        IntPtr job,
        int jobObjectInformationClass,
        ref JobObjectExtendedLimitInformation jobObjectInformation,
        int jobObjectInformationLength);

    [DllImport("kernel32.dll", EntryPoint = "CreateProcessW", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreateProcessW(
        string? applicationName,
        IntPtr commandLine,
        IntPtr processAttributes,
        IntPtr threadAttributes,
        [MarshalAs(UnmanagedType.Bool)] bool inheritHandles,
        uint creationFlags,
        IntPtr environment,
        string? currentDirectory,
        ref StartupInfo startupInfo,
        out ProcessInformation processInformation);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AssignProcessToJobObject(
        IntPtr job,
        IntPtr process);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint ResumeThread(IntPtr thread);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint WaitForSingleObject(
        IntPtr handle,
        uint milliseconds);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetExitCodeProcess(
        IntPtr process,
        out int exitCode);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr handle);

    [StructLayout(LayoutKind.Sequential)]
    private struct JobObjectBasicLimitInformation
    {
        public long PerProcessUserTimeLimit;
        public long PerJobUserTimeLimit;
        public uint LimitFlags;
        public nuint MinimumWorkingSetSize;
        public nuint MaximumWorkingSetSize;
        public uint ActiveProcessLimit;
        public nuint Affinity;
        public uint PriorityClass;
        public uint SchedulingClass;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct IoCounters
    {
        public ulong ReadOperationCount;
        public ulong WriteOperationCount;
        public ulong OtherOperationCount;
        public ulong ReadTransferCount;
        public ulong WriteTransferCount;
        public ulong OtherTransferCount;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct JobObjectExtendedLimitInformation
    {
        public JobObjectBasicLimitInformation BasicLimitInformation;
        public IoCounters IoInfo;
        public nuint ProcessMemoryLimit;
        public nuint JobMemoryLimit;
        public nuint PeakProcessMemoryUsed;
        public nuint PeakJobMemoryUsed;
    }

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
    private struct ProcessInformation
    {
        public IntPtr Process;
        public IntPtr Thread;
        public int ProcessId;
        public int ThreadId;
    }
}
