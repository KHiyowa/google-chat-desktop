using System.Diagnostics;
using Microsoft.Web.WebView2.Core;

// This code is based on https://github.com/ankurk91/google-chat-electron/blob/2.20.0/src/main/features/externalLinks.ts

namespace google_chat_desktop.main.features
{
    internal class ExternalLinks
    {
        private readonly string[] allowedDomains = { "accounts.google.com", "accounts.youtube.com", "chat.google.com", "mail.google.com" };

        public void HandleNewWindowRequested(object sender, CoreWebView2NewWindowRequestedEventArgs e)
        {
            Uri uri = new Uri(e.Uri);

            // If the URL is not a valid HTTP or HTTPS URL
            if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            {
                e.Handled = true;
                Process.Start(new ProcessStartInfo(e.Uri) { UseShellExecute = true });
                return;
            }

            // If the URL is not in the list of allowed host domains
            if (!Array.Exists(allowedDomains, domain => uri.Host.Contains(domain)))
            {
                e.Handled = true;
                Process.Start(new ProcessStartInfo(e.Uri) { UseShellExecute = true });
                return;
            }

            // If the URL is 'mail.google.com' but does not start with https://mail.google.com/chat
            if (uri.Host == "mail.google.com" && !uri.AbsoluteUri.StartsWith("https://mail.google.com/chat"))
            {
                e.Handled = true;
                Process.Start(new ProcessStartInfo(e.Uri) { UseShellExecute = true });
                return;
            }

            // If the URL is 'chat.google.com' but contains https://chat.google.com/u/0/api/get_attachment_url
            if (uri.Host == "chat.google.com" && uri.AbsoluteUri.Contains("https://chat.google.com/u/0/api/get_attachment_url"))
            {
                e.Handled = true;
                Process.Start(new ProcessStartInfo(e.Uri) { UseShellExecute = true });
                return;
            }

            // If the domain is allowed, open within WebView
            e.Handled = false;
        }
    }
}
