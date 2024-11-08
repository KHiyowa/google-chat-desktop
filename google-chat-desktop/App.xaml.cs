using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
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
            var assembly = Assembly.GetExecutingAssembly();
            var appGuid = assembly.GetCustomAttribute<GuidAttribute>()?.Value;

            mutex = new Mutex(true, appGuid);

            if (!mutex.WaitOne(TimeSpan.Zero, true))
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

            // MainWindowの初期化と表示
            CreateMainWindow();
        }

        private void CreateMainWindow()
        {
            var mainWindow = google_chat_desktop.MainWindow.Instance;
            mainWindow.InitializeNotifyIcon();
            mainWindow.Show();
        }

        public void RelaunchApplication()
        {
            // 現在の実行ファイルのパスを取得
            var exePath = Process.GetCurrentProcess().MainModule.FileName;

            // 新しいプロセスを起動
            Process.Start(new ProcessStartInfo(exePath)
            {
                UseShellExecute = true
            });

            // 現在のプロセスを終了
            Application.Current.Shutdown();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            mutex?.ReleaseMutex();
            base.OnExit(e);
        }
    }
}
