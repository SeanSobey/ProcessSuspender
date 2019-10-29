using System;
using System.Configuration;
using System.Diagnostics;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.Threading;
using System.Linq;

namespace ProcessSuspender
{
    static class Program
    {
        const int WH_KEYBOARD_LL = 13;
        const int WM_KEYDOWN = 0x0100;

        static LowLevelKeyboardProc KeyboardProc = HookCallback;
        static IntPtr KeyboardProcHookID = IntPtr.Zero;

        static string ProcessName = null;
        static int ProcessID = -1;
        static int SuspendTime = 5000;
        static Keys Key = Keys.Pa1;

        delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        static extern IntPtr GetModuleHandle(string lpModuleName);

        [Flags]
        public enum ThreadAccess : int
        {
            TERMINATE = (0x0001),
            SUSPEND_RESUME = (0x0002),
            GET_CONTEXT = (0x0008),
            SET_CONTEXT = (0x0010),
            SET_INFORMATION = (0x0020),
            QUERY_INFORMATION = (0x0040),
            SET_THREAD_TOKEN = (0x0080),
            IMPERSONATE = (0x0100),
            DIRECT_IMPERSONATION = (0x0200)
        }

        enum ShowCommand : int
        {
            HIDE = 0,
            SHOWNORMAL = 1,
            SHOWMINIMIZED = 2,
            SHOWMAXIMIZED = 3,
            SHOWNOACTIVATE = 4,
            SHOW = 5,
            MINIMIZE = 6,
            SHOWMINNOACTIVE = 7,
            SHOWNA = 8,
            RESTORE = 9,
            SHOWDEFAULT = 10,
            FORCEMINIMIZE = 11,
        }

        [DllImport("kernel32.dll")]
        static extern IntPtr OpenThread(ThreadAccess dwDesiredAccess, bool bInheritHandle, uint dwThreadId);
        
        [DllImport("kernel32.dll")]
        static extern uint SuspendThread(IntPtr hThread);
        
        [DllImport("kernel32.dll")]
        static extern int ResumeThread(IntPtr hThread);
        
        [DllImport("kernel32", CharSet = CharSet.Auto, SetLastError = true)]
        static extern bool CloseHandle(IntPtr handle);

        // https://docs.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-showwindow
        [DllImport("user32.dll")]
        static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        static void Main(string[] args)
        {
            try
            {
                ParseConfig();
                KeyboardProcHookID = SetHook(KeyboardProc);
                Application.Run();
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine(exception.ToString());
                Console.WriteLine("Press any key to exit...");
                Console.ReadKey();
            }
            finally
            {
                if (KeyboardProcHookID != IntPtr.Zero)
                    UnhookWindowsHookEx(KeyboardProcHookID);
            }
        }

        static void ParseConfig()
        {
            if (int.TryParse(ConfigurationManager.AppSettings["ProcessID"], out int processId))
                ProcessID = processId;
            else
                ProcessName = ConfigurationManager.AppSettings["ProcessName"];
            SuspendTime = Int32.Parse(ConfigurationManager.AppSettings["SuspendTime"]);
            try
            {
                Key = (Keys)Enum.Parse(typeof(Keys), ConfigurationManager.AppSettings["Key"]);
            }
            catch (Exception)
            {
                Console.WriteLine("Available Keys:");
                Console.WriteLine(string.Join(Environment.NewLine, Enum.GetNames(typeof(Keys))));
                throw;
            }
        }

        static IntPtr SetHook(LowLevelKeyboardProc proc)
        {
            using (var process = Process.GetCurrentProcess())
            using (var module = process.MainModule)
            {
                return SetWindowsHookEx(WH_KEYBOARD_LL, proc, GetModuleHandle(module.ModuleName), 0);
            }
        }

        static IntPtr HookCallback(Int32 nCode, IntPtr wParam, IntPtr lParam)
        {
            try
            {
                if (nCode >= 0 && wParam == (IntPtr)WM_KEYDOWN)
                {
                    var key = (Keys)Marshal.ReadInt32(lParam);
                    if (key != Key)
                        return CallNextHookEx(KeyboardProcHookID, nCode, wParam, lParam);
                    Run();
                }
                return CallNextHookEx(KeyboardProcHookID, nCode, wParam, lParam);
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine(exception.Message);
                Console.WriteLine("Press any key to crash...");
                Console.ReadKey();
                throw;
            }
        }

        private static void Run()
        {
            var processes = ProcessName != null
                ? Process.GetProcessesByName(ProcessName)
                : new[] { Process.GetProcessById(ProcessID) };
            if (!processes.Any())
            {
                throw new Exception($"Unable to find any process with the name of '{ProcessName}'!");
            }
            if (processes.Count() > 1)
            {
                throw new Exception($"Found more than one process with the name of '{ProcessName}'!");
            }
            var process = processes.Single();
            var currentProcess = Process.GetCurrentProcess();
            Console.WriteLine($"Hiding the '{process.ProcessName}' process");
            ShowWindow(process.MainWindowHandle, (int)ShowCommand.FORCEMINIMIZE);
            Console.WriteLine($"Restoring this '{currentProcess.ProcessName}' process");
            ShowWindow(currentProcess.MainWindowHandle, (int)ShowCommand.RESTORE);
            Console.WriteLine($"Suspending the '{process.ProcessName}' process");
            SuspendProcess(process);
            Console.WriteLine($"Sleeping for {SuspendTime}ms");
            Thread.Sleep(SuspendTime);
            Console.WriteLine($"Resuming the '{process.ProcessName}' process");
            ResumeProcess(process);
            Console.WriteLine($"Hiding the current '{currentProcess.ProcessName}' process");
            ShowWindow(currentProcess.MainWindowHandle, (int)ShowCommand.MINIMIZE);
            Console.WriteLine($"Restoring the '{process.ProcessName}' process");
            ShowWindow(process.MainWindowHandle, (int)ShowCommand.RESTORE);
        }

        static void SuspendProcess(Process process)
        {
            foreach (ProcessThread thread in process.Threads)
            {
                var openThread = OpenThread(ThreadAccess.SUSPEND_RESUME, false, (uint)thread.Id);
                if (openThread == IntPtr.Zero)
                {
                    continue;
                }
                SuspendThread(openThread);
                CloseHandle(openThread);
            }
        }

        static void ResumeProcess(Process process)
        {
            if (process.ProcessName == string.Empty)
                return;
            foreach (ProcessThread thread in process.Threads)
            {
                var openThread = OpenThread(ThreadAccess.SUSPEND_RESUME, false, (uint)thread.Id);
                if (openThread == IntPtr.Zero)
                {
                    continue;
                }
                var suspendCount = 0;
                do
                {
                    suspendCount = ResumeThread(openThread);
                } while (suspendCount > 0);
                CloseHandle(openThread);
            }
        }
    }
}
