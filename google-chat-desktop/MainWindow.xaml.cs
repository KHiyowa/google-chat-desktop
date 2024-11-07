using System.Windows;
using Microsoft.Web.WebView2.Core;
using System.Diagnostics;
using System.Windows.Controls;
using Microsoft.Toolkit.Uwp.Notifications;
using google_chat_desktop.features;

using Application = System.Windows.Application;
using System.Text.Json.Serialization;
using Windows.UI.Notifications;


namespace google_chat_desktop
{
    public partial class MainWindow : Window
    {
        private static MainWindow instance;
        private static readonly object lockObject = new object();

        private NotifyIcon notifyIcon;
        private ContextMenu contextMenu;
        private ExternalLinks externalLinks;
        private WindowSettings windowSettings;
        private AboutPanel aboutPanel;

        private const string ChatUrl = "https://mail.google.com/chat/";
        private readonly Icon iconOnline = new Icon("resources/icons/normal/windows.ico");
        private readonly Icon iconOffline = new Icon("resources/icons/offline/windows.ico");

        public static MainWindow Instance
        {
            get
            {
                lock (lockObject)
                {
                    if (instance == null)
                    {
                        instance = new MainWindow();
                    }
                    return instance;
                }
            }
        }

        private MainWindow()
        {
            InitializeComponent();
            windowSettings = new WindowSettings();
            windowSettings.LoadWindowSettings(this);
            InitializeWebView();
            aboutPanel = new AboutPanel();

            // Saves settings when window size or position is changed
            this.SizeChanged += MainWindow_SizeChanged;
            this.LocationChanged += MainWindow_LocationChanged;

            // Add event handler for toast notification activation
            ToastNotificationManagerCompat.OnActivated += ToastNotificationManagerCompat_OnActivated;
        }

        private async void InitializeWebView()
        {
            await webView.EnsureCoreWebView2Async(null);
            webView.CoreWebView2.NewWindowRequested += CoreWebView2_NewWindowRequested;
            webView.CoreWebView2.WebMessageReceived += CoreWebView2_WebMessageReceived;
            webView.CoreWebView2.PermissionRequested += CoreWebView2_PermissionRequested;
            webView.CoreWebView2.NavigationCompleted += CoreWebView2_NavigationCompleted;

            // WebView2 Configuration
            var settings = webView.CoreWebView2.Settings;
            settings.AreDefaultContextMenusEnabled = true;
            settings.AreDefaultScriptDialogsEnabled = true;
            settings.IsStatusBarEnabled = true;
            settings.IsWebMessageEnabled = true;
            settings.IsZoomControlEnabled = true;
            settings.IsPasswordAutosaveEnabled = true;
            settings.IsGeneralAutofillEnabled = true;

            #if DEBUG
            settings.AreDevToolsEnabled = true;
            #else
            settings.AreDevToolsEnabled = false;
            #endif
        }

        private async void CoreWebView2_NavigationCompleted(object sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            if (e.IsSuccess && webView.CoreWebView2.Source.StartsWith(ChatUrl))
            {
                // Add a script that overrides the Notification object
                string script = @"
    // TransparentNotification object
    class TransparentNotification extends EventTarget {
        constructor(title, options) {
            super();
            this.title = title;
            this.options = options;
        }

        click() {
            const event = new Event('click');
            this.dispatchEvent(event);
        }

        close() {
            const event = new Event('close');
            this.dispatchEvent(event);
        }
    }

    // Override the Notification object
    const OriginalNotification = window.Notification;
    const notifications = new Map();
    window.Notification = function(title, options) {
        // Create a notification object but do not show it
        const notification = new TransparentNotification(title, options);
        if (options.tag) {
            notifications.set(options.tag, notification);
        }
        console.log('Notification:', JSON.stringify({ title, options }));
        window.chrome.webview.postMessage(JSON.stringify({ title, options }));
        return notification;
    };
    window.Notification.permission = OriginalNotification.permission;
    window.Notification.requestPermission = OriginalNotification.requestPermission.bind(OriginalNotification);

    // Listen for notification click events from C#
    window.addEventListener('notificationClick', function(event) {
        const tag = event.detail.tag;
        const notification = notifications.get(tag);
        if (notification) {
            notification.dispatchEvent(new Event('click'));
            notifications.delete(tag);
        }
    });
    // Listen for notification close events from C#
    window.addEventListener('notificationClose', function(event) {
        const tag = event.detail.tag;
        const notification = notifications.get(tag);
        if (notification) {
            notification.dispatchEvent(new Event('close'));
            notifications.delete(tag);
        }
    });

    ";
                await webView.CoreWebView2.ExecuteScriptAsync(script);

                // Change online icon
                notifyIcon.Icon = iconOnline;
            }
            else if (e.IsSuccess && !webView.CoreWebView2.Source.StartsWith(ChatUrl))
            {
                // Change offline icon
                notifyIcon.Icon = iconOffline;
            }
            else
            {
                Debug.WriteLine($"Navigation failed with error code {e.WebErrorStatus}");
                // Change offline icon
                notifyIcon.Icon = iconOffline;
            }
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

        private void CoreWebView2_WebMessageReceived(object sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            try
            {
                string message = e.WebMessageAsJson;
                Debug.WriteLine($"Push notification received: {message}");

                // エスケープされたJSON文字列を元の形式に戻す
                string unescapedMessage = System.Text.Json.JsonSerializer.Deserialize<string>(message);
                Debug.WriteLine($"Unescaped message: {unescapedMessage}");

                // Parse the message to extract title and options
                var notificationData = System.Text.Json.JsonSerializer.Deserialize<NotificationData>(unescapedMessage);
                if (notificationData != null)
                {
                    ShowNotification(notificationData.Title, notificationData.Options.Body, notificationData.Options.Tag);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error processing web message: {ex.Message}");
            }
        }

        private void ShowNotification(string title, string message, string? tag)
        {
            var toastBuilder = new ToastContentBuilder()
                .AddText(title)
                .AddText(message)
                .AddAudio(new ToastAudio { Silent = true }); // Disable notification sound

            if (!string.IsNullOrEmpty(tag))
            {
                toastBuilder.AddArgument("tag", tag); // Add tag as an argument
            }

            // Create ToastNotification instance
            var toastContent = toastBuilder.GetToastContent();
            var toastNotification = new ToastNotification(toastContent.GetXml());

            // Show the notification
            ToastNotificationManagerCompat.CreateToastNotifier().Show(toastNotification);
        }


        private void ToastNotificationManagerCompat_OnActivated(ToastNotificationActivatedEventArgsCompat e)
        {
            // Parse the arguments
            var args = ToastArguments.Parse(e.Argument);

            if (args.Contains("tag"))
            {
                string tag = args["tag"];
                string script = $"window.dispatchEvent(new CustomEvent('notificationClick', {{ detail: {{ tag: '{tag}' }} }}));";
                Dispatcher.Invoke(async () => await webView.CoreWebView2.ExecuteScriptAsync(script));
            }

            // ウィンドウが非表示の場合は表示し、アクティブにする
            ShowAndActivateWindow();
        }


        private class NotificationData
    {
        [JsonPropertyName("title")]
        public string Title { get; set; }

        [JsonPropertyName("options")]
        public NotificationOptions Options { get; set; }
    }

        private class NotificationOptions
        {
            [JsonPropertyName("body")]
            public string Body { get; set; }

            [JsonPropertyName("silent")]
            public bool Silent { get; set; }

            [JsonPropertyName("tag")]
            public string? Tag { get; set; }
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
                Icon = new Icon("resources/icons/offline/windows.ico"),
                Visible = true,
                ContextMenuStrip = new ContextMenuStrip()
            };

            notifyIcon.ContextMenuStrip.Items.Add("Toggle", null, (s, e) => ToggleWindow(s, e));
            notifyIcon.ContextMenuStrip.Items.Add("Quit", null, (s, e) => ExitApplication(s, e));
            notifyIcon.DoubleClick += (s, e) => ShowAndActivateWindow();
        }

        private void ShowAndActivateWindow()
        {
            Dispatcher.Invoke(() =>
            {
                if (this.Visibility == Visibility.Hidden)
                {
                    this.Show();
                }
                if (this.WindowState == WindowState.Minimized)
                {
                    this.WindowState = WindowState.Normal;
                }
                this.Activate();
            });
        }

        public void DisposeNotifyIcon()
        {
            if (notifyIcon != null)
            {
                notifyIcon.Visible = false;
                notifyIcon.Dispose();
                notifyIcon = null;
            }
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
                if (this.WindowState == WindowState.Minimized)
                {
                    this.WindowState = WindowState.Normal;
                }
            }
        }

        public void ExitApplication(object sender, EventArgs e)
        {
            DisposeNotifyIcon();
            Application.Current.Shutdown();
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            e.Cancel = true;
            this.Hide();
        }

        private void Relaunch_Click(object sender, RoutedEventArgs e)
        {
            var app = (App)Application.Current;
            app.RelaunchApplication();
        }

        private void Quit_Click(object sender, RoutedEventArgs e)
        {
            ExitApplication(sender, e);
        }

        private void Reload_Click(object sender, RoutedEventArgs e)
        {
            webView.Reload();
        }

        private void OfficialGitHub_Click(object sender, RoutedEventArgs e)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "https://github.com/KHiyowa/google-chat-desktop",
                UseShellExecute = true
            });
        }

        private void About_Click(object sender, RoutedEventArgs e)
        {
            aboutPanel.ShowAbout();
        }


        private void MainWindow_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (this.WindowState == WindowState.Normal)
            {
                windowSettings.SaveWindowSettings(this);
            }
        }

        private void MainWindow_LocationChanged(object sender, EventArgs e)
        {
            windowSettings.SaveWindowSettings(this);
        }
    }
}
