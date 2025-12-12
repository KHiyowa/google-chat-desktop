using System.Diagnostics;
using Microsoft.Web.WebView2.Core;
using System.Windows;

namespace google_chat_desktop.main.features
{
    internal class ExternalLinks
    {
        private readonly string[] allowedDomains = { "accounts.google.com", "accounts.youtube.com", "chat.google.com", "mail.google.com" };

        public void HandleNewWindowRequested(object sender, CoreWebView2NewWindowRequestedEventArgs e)
        {
            Uri? uri = null;
            bool shouldOpenExternally = true;

            try
            {
                uri = new Uri(e.Uri);
            }
            catch
            {
            }

            if (uri != null)
            {
                bool isHttp = (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
                bool isAllowedDomain = Array.Exists(allowedDomains, domain => uri.Host.Contains(domain));

                bool isMailChat = (uri.Host == "mail.google.com" && uri.AbsoluteUri.StartsWith("https://mail.google.com/chat"));
                bool isChatApi = (uri.Host == "chat.google.com" && uri.AbsoluteUri.Contains("https://chat.google.com/u/0/api/get_attachment_url"));

                if (isHttp && isAllowedDomain && !isMailChat && !isChatApi)
                {
                    shouldOpenExternally = false;
                }
            }

            if (shouldOpenExternally)
            {
                e.Handled = true; // open externally
                OpenUrlSafely(e.Uri);
            }
            else
            {
                e.Handled = false; // open in WebView
            }
        }


        private static void OpenUrlSafely(string url)
        {
            try
            {
                // 1: Normal run
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            catch (Exception ex1)
            {
                // 2: Via cmd start
                try
                {
                    // /c : Close the command prompt after the command is run
                    // start : Open with default associated application
                    // "" : Dummy title for the cmd window
                    // "{url}" : Handle URLs with spaces
                    ProcessStartInfo psi = new ProcessStartInfo
                    {
                        FileName = "cmd",
                        Arguments = $"/c start \"\" \"{url}\"",
                        WindowStyle = ProcessWindowStyle.Hidden,
                        CreateNoWindow = true,
                        UseShellExecute = false
                    };
                    Process.Start(psi);
                }
                catch (Exception ex2)
                {
                    // Notify user about the failure
                    string errorMsg = $"Cannot open link\n\n" +
                                      $"URL: {url}\n\n" +
                                      $"[Error]\n" +
                                      $"Normal run {ex1.Message}\n" +
                                      $"Via cmd {ex2.Message}\n\n" +
                                      $"Please report developer with screen shot of this window!";

                    System.Windows.MessageBox.Show(errorMsg, "External link error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }
}