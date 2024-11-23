using System.Windows;
using Microsoft.Web.WebView2.Core;
using System.Diagnostics;
using System.Windows.Controls;
using Microsoft.Toolkit.Uwp.Notifications;
using google_chat_desktop.features;

using Application = System.Windows.Application;
using System.Text.Json.Serialization;
using Windows.UI.Notifications;
using System.Security.Cryptography;


namespace google_chat_desktop
{
    public partial class MainWindow : Window
    {
        private static MainWindow instance;
        private static readonly object lockObject = new object();
        private static readonly string appDirectory = AppDomain.CurrentDomain.BaseDirectory;
        private static readonly string tempFolderPath = System.IO.Path.Combine(appDirectory, "temp");
        private static readonly Dictionary<string, Uri> onMemoryIconCache = new Dictionary<string, Uri>();

        private NotifyIcon notifyIcon;
        private ContextMenu contextMenu;
        private ExternalLinks externalLinks;
        private WindowSettings windowSettings;
        private AboutPanel aboutPanel;

        private const string iconCacheFolderName = "iconCache";
        private const string ChatUrl = "https://mail.google.com/chat/";
        private readonly Icon iconBadge = new Icon("resources/icons/badge/windows.ico");
        private readonly Icon iconNormal = new Icon("resources/icons/normal/windows.ico");
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

            // 一時フォルダとアイコンキャッシュフォルダが存在しない場合は作成
            string iconCachePath = System.IO.Path.Combine(tempFolderPath, iconCacheFolderName);
            if (!System.IO.Directory.Exists(iconCachePath))
            {
                System.IO.Directory.CreateDirectory(iconCachePath);
            }
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
                // Function to fetch and convert image to Base64
                async function getBase64Image(url) {
                    const response = await fetch(url);
                    const blob = await response.blob();
                    const mimeType = blob.type; // 画像のMIMEタイプを取得
                    return new Promise((resolve, reject) => {
                        const reader = new FileReader();
                        reader.onloadend = () => resolve({ base64: reader.result.split(',')[1], mimeType });
                        reader.onerror = reject;
                        reader.readAsDataURL(blob);
                    });
                }

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

                    // Fetch icon data if available
                    (async () => {
                        let iconData = null;
                        if (options.icon) {
                            try {
                                iconData = await getBase64Image(options.icon);
                            } catch (error) {
                                console.error('Error fetching icon:', error);
                            }
                        }

                        const message = {
                            type: 'notification',
                            title,
                            options: {
                                ...options,
                                iconBase64: iconData ? iconData.base64 : null,
                                iconMimeType: iconData ? iconData.mimeType : null
                            }
                        };

                        console.log('Notification:', JSON.stringify(message));
                        window.chrome.webview.postMessage(JSON.stringify(message));
                    })();

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

                // Function to get the current favicon URL
                function getFaviconUrl() {
                    const link = document.querySelector(""link[rel~='icon']"");
                    return link ? link.href : null;
                }

                // Function to evaluate favicon state
                function evaluateFaviconState(faviconUrl) {
                    const fileName = faviconUrl.split('/').pop().toLowerCase();
                    if (fileName.includes('chat') && fileName.includes('new') && fileName.includes('notif')) {
                        return 'badge';
                    } else if (fileName.includes('chat')) {
                        return 'normal';
                    } else {
                        return 'offline';
                    }
                }

                // Function to monitor favicon changes
                async function monitorFavicon() {
                    let lastFaviconUrl = getFaviconUrl();

                    // 初回実行時に現在のfaviconの状態を通知
                    if (lastFaviconUrl) {
                        const initialFaviconState = evaluateFaviconState(lastFaviconUrl);
                        const initialMessage = {
                            type: 'favicon',
                            state: initialFaviconState
                        };
                        window.chrome.webview.postMessage(JSON.stringify(initialMessage));
                    }

                    setInterval(async () => {
                        const currentFaviconUrl = getFaviconUrl();
                        if (currentFaviconUrl && currentFaviconUrl !== lastFaviconUrl) {
                            lastFaviconUrl = currentFaviconUrl;
                            const faviconState = evaluateFaviconState(currentFaviconUrl);
                            const message = {
                                type: 'favicon',
                                state: faviconState
                            };
                            window.chrome.webview.postMessage(JSON.stringify(message));
                        }
                    }, 1000); // 1秒ごとにチェック
                }

                // Start monitoring favicon changes
                monitorFavicon();

                ";
                await webView.CoreWebView2.ExecuteScriptAsync(script);
            }
            else if (e.IsSuccess && !webView.CoreWebView2.Source.StartsWith(ChatUrl))
            {
            }
            else
            {
                Debug.WriteLine($"Navigation failed with error code {e.WebErrorStatus}");
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
                Debug.WriteLine($"Message received: {message}");

                // エスケープされたJSON文字列を元の形式に戻す
                string unescapedMessage = System.Text.Json.JsonSerializer.Deserialize<string>(message);
                Debug.WriteLine($"Unescaped message: {unescapedMessage}");

                // Parse the message to extract type and data
                var messageData = System.Text.Json.JsonSerializer.Deserialize<FaviconData>(unescapedMessage);
                if (messageData != null)
                {
                    switch (messageData.Type)
                    {
                        case "notification":
                            {
                                var notificationData = System.Text.Json.JsonSerializer.Deserialize<NotificationData>(unescapedMessage);
                                if (notificationData != null)
                                {
                                    var iconUri = CreateImageUri(notificationData.Options.IconBase64, notificationData.Options.IconMimeType);
                                    ShowNotification(
                                        title: notificationData.Title,
                                        message: notificationData.Options.Body,
                                        tag: notificationData.Options.Tag,
                                        iconUri: iconUri
                                    );
                                }
                                break;
                            }

                        case "favicon":
                            {
                                // ここでfaviconの状態に応じた処理を行う
                                Debug.WriteLine($"Favicon state: {messageData.State}");

                                // タスクトレイアイコンを更新
                                switch (messageData.State)
                                {
                                    case "badge":
                                        notifyIcon.Icon = iconBadge;
                                        break;
                                    case "normal":
                                        notifyIcon.Icon = iconNormal;
                                        break;
                                    case "offline":
                                        notifyIcon.Icon = iconOffline;
                                        break;
                                }
                                break;
                            }

                        default:
                            Debug.WriteLine($"Unknown message type: {messageData.Type}");
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error processing web message: {ex.Message}");
            }
        }


        private void ShowNotification(string title, string message, string? tag = null, Uri? iconUri = null)
        {
            var toastBuilder = new ToastContentBuilder()
                .AddText(title)
                .AddText(message)
                .AddAudio(new ToastAudio { Silent = true }); // Disable notification sound

            if (!string.IsNullOrEmpty(tag))
            {
                toastBuilder.AddArgument("tag", tag); // Add tag as an argument
            }

            if (iconUri != null)
            {
                toastBuilder.AddAppLogoOverride(iconUri);
            }

            // Create ToastNotification instance
            var toastContent = toastBuilder.GetToastContent();
            var toastNotification = new ToastNotification(toastContent.GetXml());

            // Show the notification
            ToastNotificationManagerCompat.CreateToastNotifier().Show(toastNotification);
        }

        private Uri? CreateImageUri(string? iconBase64, string? iconMimeType)
        {
            if (string.IsNullOrEmpty(iconBase64) || string.IsNullOrEmpty(iconMimeType))
            {
                return null;
            }

            try
            {
                // キャッシュに存在するか確認
                if (onMemoryIconCache.TryGetValue(iconBase64, out Uri cachedUri))
                {
                    return cachedUri;
                }

                // Base64データをバイト配列に変換
                byte[] imageBytes = Convert.FromBase64String(iconBase64);

                // SHA256ハッシュを計算
                using (SHA256 sha256 = SHA256.Create())
                {
                    byte[] hashBytes = sha256.ComputeHash(imageBytes);
                    string hashString = BitConverter.ToString(hashBytes).Replace("-", "").ToLower();

                    // MIMEタイプからファイル拡張子を取得
                    string fileExtension = System.Text.RegularExpressions.Regex.Match(iconMimeType, @"image/(?<ext>\w+)").Groups["ext"].Value;

                    // アイコンキャッシュフォルダ内の一時ファイルのパスを生成
                    string iconCachePath = System.IO.Path.Combine(tempFolderPath, iconCacheFolderName);
                    string tempFilePath = System.IO.Path.Combine(iconCachePath, $"{hashString}.{fileExtension}");

                    // ファイルが既に存在するか確認
                    if (!System.IO.File.Exists(tempFilePath))
                    {
                        // バイト配列をファイルに書き込む
                        System.IO.File.WriteAllBytes(tempFilePath, imageBytes);
                    }

                    // ファイルのUriをキャッシュに追加
                    Uri fileUri = new Uri(tempFilePath);
                    onMemoryIconCache[iconBase64] = fileUri;

                    return fileUri;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error creating image URI: {ex.Message}");
                return null;
            }
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

        private class FaviconData
        {
            [JsonPropertyName("type")]
            public string Type { get; set; }

            [JsonPropertyName("state")]
            public string State { get; set; }
        }

        private class NotificationData
    {
        [JsonPropertyName("title")]
        public string Title { get; set; }

        [JsonPropertyName("options")]
        public NotificationOptions Options { get; set; }
    }

        public class NotificationOptions
        {
            [JsonPropertyName("body")]
            public string Body { get; set; }

            [JsonPropertyName("silent")]
            public bool? Silent { get; set; }

            [JsonPropertyName("tag")]
            public string? Tag { get; set; }

            [JsonPropertyName("iconBase64")]
            public string? IconBase64 { get; set; }

            [JsonPropertyName("iconMimeType")]
            public string? IconMimeType { get; set; }
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

        private void DeleteTempFolder()
        {
            try
            {
                if (System.IO.Directory.Exists(tempFolderPath))
                {
                    System.IO.Directory.Delete(tempFolderPath, true);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error deleting temp folder {tempFolderPath}: {ex.Message}");
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
            DeleteTempFolder();
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
