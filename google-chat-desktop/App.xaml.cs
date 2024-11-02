using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;

using Application = System.Windows.Application;

namespace google_chat_desktop
{
    public partial class App : Application
    {
        private static Mutex? mutex;

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        private const int SW_RESTORE = 9;

        protected override void OnStartup(StartupEventArgs e)
        {
            const string appName = "GoogleChatDesktopApp";
            bool createdNew;

            mutex = new Mutex(true, appName, out createdNew);

            if (!createdNew)
            {
                // 既にアプリケーションが起動している場合
                var currentProcess = Process.GetCurrentProcess();
                var runningProcess = Process.GetProcessesByName(currentProcess.ProcessName)
                                            .FirstOrDefault(p => p.Id != currentProcess.Id);

                if (runningProcess != null)
                {
                    IntPtr hWnd = runningProcess.MainWindowHandle;
                    if (hWnd != IntPtr.Zero)
                    {
                        ShowWindow(hWnd, SW_RESTORE);
                        SetForegroundWindow(hWnd);
                    }
                }
                Application.Current.Shutdown();
                return;
            }

            base.OnStartup(e);

            // MainWindowの初期化
            var mainWindow = new MainWindow();
            mainWindow.InitializeNotifyIcon();
            mainWindow.Show();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            mutex?.ReleaseMutex();
            base.OnExit(e);
        }
    }
}

