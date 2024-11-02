using System.Windows;
using Microsoft.Web.WebView2.Core;
using System.Diagnostics;
using System.Windows.Controls;
using Microsoft.Toolkit.Uwp.Notifications;
using google_chat_desktop.features;

using Application = System.Windows.Application;


namespace google_chat_desktop
{
    public partial class MainWindow : Window
    {
        private NotifyIcon notifyIcon;
        private ContextMenu contextMenu;
        private ExternalLinks externalLinks;
        private WindowSettings windowSettings;
        private AboutPanel aboutPanel;

        public MainWindow()
        {
            InitializeComponent();
            windowSettings = new WindowSettings();
            windowSettings.LoadWindowSettings(this);
            InitializeWebView();
            aboutPanel = new AboutPanel();

            // Saves settings when window size or position is changed
            this.SizeChanged += MainWindow_SizeChanged;
            this.LocationChanged += MainWindow_LocationChanged;
        }

        private async void InitializeWebView()
        {
            await webView.EnsureCoreWebView2Async(null);
            webView.CoreWebView2.NewWindowRequested += CoreWebView2_NewWindowRequested;
            webView.CoreWebView2.WebMessageReceived += CoreWebView2_WebMessageReceived;
            webView.CoreWebView2.PermissionRequested += CoreWebView2_PermissionRequested;

            //  WebView2 Configuration
            var settings = webView.CoreWebView2.Settings;
            settings.AreDefaultContextMenusEnabled = true;
            settings.AreDefaultScriptDialogsEnabled = true;
            settings.IsStatusBarEnabled = true;
            settings.IsWebMessageEnabled = true;
            settings.IsZoomControlEnabled = true;
            settings.AreDevToolsEnabled = true;

            // Add a script that checks and requests permission for notifications
            string script = @"
                if ('serviceWorker' in navigator) {
                    navigator.serviceWorker.ready.then(function(registration) {
                        console.log('Service Worker is ready:', registration);
                    }).catch(function(error) {
                        console.error('Service Worker error:', error.message, error);
                    });
                }

                // Confirm and request permission for notifications
                if (Notification.permission === 'default') {
                    Notification.requestPermission().then(function(permission) {
                        if (permission === 'granted') {
                            console.log('Notification permission granted.');
                        } else {
                            console.log('Notification permission denied.');
                        }
                    });
                } else {
                    console.log('Notification permission:', Notification.permission);
                }
                ";
            webView.CoreWebView2.ExecuteScriptAsync(script);
        }

        private void CoreWebView2_PermissionRequested(object sender, CoreWebView2PermissionRequestedEventArgs e)
        {
            if (e.PermissionKind == CoreWebView2PermissionKind.Notifications)
            {
                e.State = CoreWebView2PermissionState.Allow;
                Debug.WriteLine("Notification permission granted.");
            }
            else
            {
                e.State = CoreWebView2PermissionState.Deny;
                Debug.WriteLine($"Permission {e.PermissionKind} denied.");
            }
        }


        // Handle console messages in CoreWebView2_WebMessageReceived method
        private void CoreWebView2_WebMessageReceived(object sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            string message = e.TryGetWebMessageAsString();
            Debug.WriteLine($"Console message: {message}");
            ShowNotification("Google Chat Desktop", message);
        }

        private void CoreWebView2_NewWindowRequested(object sender, CoreWebView2NewWindowRequestedEventArgs e)
        {
            if (externalLinks == null)
            {
                externalLinks = new ExternalLinks();
            }
            externalLinks.HandleNewWindowRequested(sender, e);
        }

        public void InitializeNotifyIcon()
        {
            contextMenu = new ContextMenu();

            MenuItem toggleMenuItem = new MenuItem { Header = "Toggle" };
            toggleMenuItem.Click += ToggleWindow;
            contextMenu.Items.Add(toggleMenuItem);

            MenuItem quitMenuItem = new MenuItem { Header = "Quit" };
            quitMenuItem.Click += ExitApplication;
            contextMenu.Items.Add(quitMenuItem);

            notifyIcon = new NotifyIcon
            {
                Icon = new Icon("resources/icons/normal/windows.ico"),
                Visible = true,
                ContextMenuStrip = new ContextMenuStrip()
            };

            notifyIcon.ContextMenuStrip.Items.Add("Toggle", null, (s, e) => ToggleWindow(s, e));
            notifyIcon.ContextMenuStrip.Items.Add("Quit", null, (s, e) => ExitApplication(s, e));
            notifyIcon.DoubleClick += (s, e) => ToggleWindow(s, e);
        }

        private void ShowNotification(string title, string message)
        {
            var toastBuilder = new ToastContentBuilder()
                .AddText(title)
                .AddText(message);

            toastBuilder.Show();
        }

        private void ToggleWindow(object sender, EventArgs e)
        {
            if (this.Visibility == Visibility.Visible)
            {
                this.Hide();
            }
            else
            {
                this.Show();
                this.WindowState = WindowState.Normal;
            }
        }

        public void ExitApplication(object sender, EventArgs e)
        {
            notifyIcon.Visible = false;
            notifyIcon.Dispose();
            Application.Current.Shutdown();
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            e.Cancel = true;
            this.Hide();
        }

        private void Quit_Click(object sender, RoutedEventArgs e)
        {
            ExitApplication(sender, e);
        }

        private void Reload_Click(object sender, RoutedEventArgs e)
        {
            webView.Reload();
        }

        private void About_Click(object sender, RoutedEventArgs e)
        {
            aboutPanel.ShowAbout();
        }


        private void MainWindow_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            windowSettings.SaveWindowSettings(this);
        }

        private void MainWindow_LocationChanged(object sender, EventArgs e)
        {
            windowSettings.SaveWindowSettings(this);
        }
    }
}
