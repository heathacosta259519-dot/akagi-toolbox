using System.Diagnostics;
using System.Runtime.InteropServices;

namespace ContextMenuEditor.Services;

public static class ExplorerService
{
    private const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;
    private const uint TOKEN_QUERY = 0x0008;
    private const uint TOKEN_DUPLICATE = 0x0002;
    private const uint TOKEN_ASSIGN_PRIMARY = 0x0001;
    private const uint CREATE_UNICODE_ENVIRONMENT = 0x0400;
    private const uint LOGON_WITH_PROFILE = 0x00000001;
    private const int SW_SHOWNORMAL = 1;

    private enum SecurityImpersonationLevel
    {
        SecurityAnonymous,
        SecurityIdentification,
        SecurityImpersonation,
        SecurityDelegation,
    }

    private enum TokenType
    {
        TokenPrimary = 1,
        TokenImpersonation,
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct StartupInfo
    {
        public int cb;
        public string? lpReserved;
        public string? lpDesktop;
        public string? lpTitle;
        public int dwX;
        public int dwY;
        public int dwXSize;
        public int dwYSize;
        public int dwXCountChars;
        public int dwYCountChars;
        public int dwFillAttribute;
        public int dwFlags;
        public short wShowWindow;
        public short cbReserved2;
        public IntPtr lpReserved2;
        public IntPtr hStdInput;
        public IntPtr hStdOutput;
        public IntPtr hStdError;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ProcessInformation
    {
        public IntPtr hProcess;
        public IntPtr hThread;
        public int dwProcessId;
        public int dwThreadId;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(uint desiredAccess, bool inheritHandle, int processId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr handle);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool OpenProcessToken(IntPtr processHandle, uint desiredAccess, out IntPtr tokenHandle);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool DuplicateTokenEx(
        IntPtr existingToken,
        uint desiredAccess,
        IntPtr tokenAttributes,
        SecurityImpersonationLevel impersonationLevel,
        TokenType tokenType,
        out IntPtr newToken);

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CreateProcessWithTokenW(
        IntPtr token,
        uint logonFlags,
        string? applicationName,
        string? commandLine,
        uint creationFlags,
        IntPtr environment,
        string? currentDirectory,
        ref StartupInfo startupInfo,
        out ProcessInformation processInformation);

    [DllImport("shell32.dll")]
    private static extern void SHChangeNotify(int eventId, uint flags, IntPtr item1, IntPtr item2);

    public static void NotifyShellChanged() => SHChangeNotify(0x08000000, 0x0000, IntPtr.Zero, IntPtr.Zero);

    public static bool RestartExplorer()
    {
        var token = TryAcquireExplorerToken();
        try
        {
            foreach (var process in Process.GetProcessesByName("explorer"))
            {
                try
                {
                    process.Kill();
                    process.WaitForExit(5000);
                }
                catch
                {
                }
                finally
                {
                    process.Dispose();
                }
            }

            for (var attempt = 0; attempt < 20; attempt++)
            {
                Thread.Sleep(250);
                if (Process.GetProcessesByName("explorer").Length > 0)
                {
                    return true;
                }
            }

            if (TryStartWithToken(token))
            {
                return true;
            }

            Process.Start(new ProcessStartInfo("explorer.exe") { UseShellExecute = true });
            return true;
        }
        finally
        {
            if (token != IntPtr.Zero)
            {
                CloseHandle(token);
            }
        }
    }

    private static IntPtr TryAcquireExplorerToken()
    {
        foreach (var process in Process.GetProcessesByName("explorer"))
        {
            var processHandle = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, process.Id);
            process.Dispose();
            if (processHandle == IntPtr.Zero)
            {
                continue;
            }

            try
            {
                if (!OpenProcessToken(processHandle, TOKEN_QUERY | TOKEN_DUPLICATE, out var tokenHandle))
                {
                    continue;
                }

                try
                {
                    if (DuplicateTokenEx(
                            tokenHandle,
                            TOKEN_ASSIGN_PRIMARY | TOKEN_DUPLICATE | TOKEN_QUERY,
                            IntPtr.Zero,
                            SecurityImpersonationLevel.SecurityImpersonation,
                            TokenType.TokenPrimary,
                            out var primaryToken))
                    {
                        return primaryToken;
                    }
                }
                finally
                {
                    CloseHandle(tokenHandle);
                }
            }
            finally
            {
                CloseHandle(processHandle);
            }
        }

        return IntPtr.Zero;
    }

    private static bool TryStartWithToken(IntPtr token)
    {
        if (token == IntPtr.Zero)
        {
            return false;
        }

        var startupInfo = new StartupInfo
        {
            cb = Marshal.SizeOf<StartupInfo>(),
            lpDesktop = @"winsta0\default",
            dwFlags = SW_SHOWNORMAL,
        };

        try
        {
            if (!CreateProcessWithTokenW(
                    token,
                    LOGON_WITH_PROFILE,
                    null,
                    "explorer.exe",
                    CREATE_UNICODE_ENVIRONMENT,
                    IntPtr.Zero,
                    null,
                    ref startupInfo,
                    out var processInformation))
            {
                return false;
            }

            CloseHandle(processInformation.hProcess);
            CloseHandle(processInformation.hThread);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
