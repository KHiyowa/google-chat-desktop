using CommunityToolkit.WinUI.Notifications;
using google_chat_desktop.main.features;
using Microsoft.Web.WebView2.Core;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;
using Windows.UI.Notifications;
using Application = System.Windows.Application;

namespace google_chat_desktop
{
    public partial class MainWindow : Window
    {
        private static MainWindow? instance;
        private static readonly Lock lockObject = new();
        private static readonly string appDirectory = AppDomain.CurrentDomain.BaseDirectory;
        private static readonly string tempFolderPath = Path.Combine(appDirectory, "temp");
        private static readonly Dictionary<string, Uri> onMemoryIconCache = []; // Key: SHA256 Hash
        private static readonly List<FileStream> _iconStreams = [];

        private NotifyIcon? notifyIcon;
        private ExternalLinks? externalLinks;
        private readonly WindowSettings windowSettings;
        private readonly AboutPanel aboutPanel;

        private const string iconCacheFolderName = "iconCache";
        private const string ChatUrl = "https://chat.google.com/";
        private readonly Icon iconBadge = new("resources/icons/badge/windows.ico");
        private readonly Icon iconNormal = new("resources/icons/normal/windows.ico");
        private readonly Icon iconOffline = new("resources/icons/offline/windows.ico");


        public static MainWindow Instance
        {
            get
            {
                lock (lockObject)
                {
                    instance ??= new MainWindow();
                    return instance;
                }
            }
        }

        private MainWindow()
        {
            InitializeComponent();
            windowSettings = new WindowSettings();
            WindowSettings.LoadWindowSettings(this);
            InitializeWebView();
            aboutPanel = new AboutPanel();

            // Saves settings when window size or position is changed
            this.SizeChanged += MainWindow_SizeChanged;
            this.LocationChanged += MainWindow_LocationChanged;

            // Add event handler for toast notification activation
            ToastNotificationManagerCompat.OnActivated += ToastNotificationManagerCompat_OnActivated;

            // 一時フォルダとアイコンキャッシュフォルダが存在しない場合は作成
            string iconCachePath = Path.Combine(tempFolderPath, iconCacheFolderName);
            if (!Directory.Exists(iconCachePath))
            {
                Directory.CreateDirectory(iconCachePath);
            }
        }

        private async void InitializeWebView()
        {
            await webView.EnsureCoreWebView2Async(null);

            // Netskope環境等でのパフォーマンス低下対策として、起動時にログインクッキー以外（キャッシュ、Service Worker等）を削除する
            try
            {
                // LocalStorageやIndexedDBを消すとセッション切れと判定されることがあるため、純粋なキャッシュのみを削除する
                await webView.CoreWebView2.Profile.ClearBrowsingDataAsync(CoreWebView2BrowsingDataKinds.DiskCache | CoreWebView2BrowsingDataKinds.CacheStorage);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to clear browsing data: {ex.Message}");
            }

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

        private async void CoreWebView2_NavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            if (e.IsSuccess && webView.CoreWebView2.Source.StartsWith(ChatUrl))
            {
                // Load and execute the preload.js script
                string scriptPath = "main/load/load.js";
                string script = await File.ReadAllTextAsync(scriptPath);
                await webView.CoreWebView2.ExecuteScriptAsync(script);
            }
            else if (e.IsSuccess && !webView.CoreWebView2.Source.StartsWith(ChatUrl))
            {
                if (notifyIcon != null) notifyIcon.Icon = iconOffline;
            }
            else
            {
                Debug.WriteLine($"Navigation failed with error code {e.WebErrorStatus}");
                if (notifyIcon != null) notifyIcon.Icon = iconOffline;
            }
        }


        private void CoreWebView2_PermissionRequested(object? sender, CoreWebView2PermissionRequestedEventArgs e)
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

        private void CoreWebView2_WebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            try
            {
                string message = e.WebMessageAsJson;
                Debug.WriteLine($"Message received: {message}");

                // Unescape the JSON string which is stringified twice from JS
                string? unescapedMessage = JsonSerializer.Deserialize<string>(message);
                if (unescapedMessage == null) return;
                Debug.WriteLine($"Unescaped message: {unescapedMessage}");

                // Parse once to avoid redundant deserialization
                using (JsonDocument doc = JsonDocument.Parse(unescapedMessage))
                {
                    JsonElement root = doc.RootElement;
                    if (!root.TryGetProperty("type", out JsonElement typeElement) || typeElement.GetString() is not { } messageType)
                    {
                        Debug.WriteLine("Received web message without a 'type' property.");
                        return;
                    }

                    switch (messageType)
                    {
                        case "notification":
                            {
                                var notificationData = root.Deserialize<NotificationData>();
                                if (notificationData?.Title == null || notificationData.Options?.Body == null) break;

                                var iconUri = CreateImageUri(notificationData.Options.IconBase64, notificationData.Options.IconMimeType);
                                ShowNotification(
                                    title: notificationData.Title,
                                    message: notificationData.Options.Body,
                                    tag: notificationData.Options.Tag,
                                    iconUri: iconUri
                                );
                                break;
                            }

                        case "favicon":
                            {
                                var faviconData = root.Deserialize<FaviconData>();
                                if (faviconData?.State == null) break;

                                Debug.WriteLine($"Favicon state: {faviconData.State}");

                                // タスクトレイアイコンを更新
                                if (notifyIcon != null)
                                {
                                    switch (faviconData.State)
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
                                }
                                break;
                            }

                        default:
                            Debug.WriteLine($"Unknown message type: {messageType}");
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
            var toastNotification = new ToastNotification(toastBuilder.GetXml());

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
                // Base64データをバイト配列に変換
                byte[] imageBytes = Convert.FromBase64String(iconBase64);

                // SHA256ハッシュを計算
                using (SHA256 sha256 = SHA256.Create())
                {
                    byte[] hashBytes = sha256.ComputeHash(imageBytes);
                    string hashString = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();

                    // メモリキャッシュをハッシュキーで確認
                    if (onMemoryIconCache.TryGetValue(hashString, out Uri? cachedUri))
                    {
                        return cachedUri;
                    }

                    // MIMEタイプからファイル拡張子を取得
                    string fileExtension = System.Text.RegularExpressions.Regex.Match(iconMimeType, @"image/(?<ext>\w+)").Groups["ext"].Value;

                    // アイコンキャッシュフォルダ内の一時ファイルのパスを生成
                    string iconCachePath = Path.Combine(tempFolderPath, iconCacheFolderName);
                    string tempFilePath = Path.Combine(iconCachePath, $"{hashString}.{fileExtension}");

                    // FileOptions.DeleteOnClose を使用してファイルを作成し、プロセス終了時に自動削除されるようにする
                    var fs = new FileStream(tempFilePath, FileMode.Create, FileAccess.ReadWrite, FileShare.Read, 4096, FileOptions.DeleteOnClose);
                    fs.Write(imageBytes, 0, imageBytes.Length);
                    fs.Flush();
                    _iconStreams.Add(fs);

                    // ファイルのUriをキャッシュに追加
                    Uri fileUri = new(tempFilePath);
                    onMemoryIconCache[hashString] = fileUri;

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
                Dispatcher.Invoke(async () =>
                {
                    if (webView.CoreWebView2 != null)
                    {
                        await webView.CoreWebView2.ExecuteScriptAsync(script);
                    }
                });
            }

            // ウィンドウが非表示の場合は表示し、アクティブにする
            ShowAndActivateWindow();
        }

        private class FaviconData
        {
            [JsonPropertyName("type")]
            public string? Type { get; set; }

            [JsonPropertyName("state")]
            public string? State { get; set; }
        }

        private class NotificationData
    {
        [JsonPropertyName("title")]
        public string? Title { get; set; }

        [JsonPropertyName("options")]
        public NotificationOptions? Options { get; set; }
    }

        public class NotificationOptions
        {
            [JsonPropertyName("body")]
            public string? Body { get; set; }

            [JsonPropertyName("silent")]
            public bool? Silent { get; set; }

            [JsonPropertyName("tag")]
            public string? Tag { get; set; }

            [JsonPropertyName("iconBase64")]
            public string? IconBase64 { get; set; }

            [JsonPropertyName("iconMimeType")]
            public string? IconMimeType { get; set; }
        }

        private void CoreWebView2_NewWindowRequested(object? sender, CoreWebView2NewWindowRequestedEventArgs e)
        {
            if (externalLinks == null)
            {
                externalLinks = new ExternalLinks();
            }
            externalLinks.HandleNewWindowRequested(sender, e);
        }

        public void InitializeNotifyIcon()
        {
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
                if (Directory.Exists(tempFolderPath))
                {
                    Directory.Delete(tempFolderPath, true);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error deleting temp folder {tempFolderPath}: {ex.Message}");
            }
        }

        private void ToggleWindow(object? sender, EventArgs e)
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

        public void ExitApplication(object? sender, EventArgs e)
        {
            DisposeNotifyIcon();

            foreach (var stream in _iconStreams)
            {
                stream.Dispose();
            }
            _iconStreams.Clear();

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
            AboutPanel.ShowAbout();
        }


        private void MainWindow_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (this.WindowState == WindowState.Normal)
            {
                WindowSettings.SaveWindowSettings(this);
            }
        }

        private void MainWindow_LocationChanged(object? sender, EventArgs e)
        {
            WindowSettings.SaveWindowSettings(this);
        }
    }
}
