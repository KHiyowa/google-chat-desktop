using System.Diagnostics;
using System.Reflection;
using Microsoft.Toolkit.Uwp.Notifications;
using Windows.UI.Notifications;

namespace google_chat_desktop.features
{
    public class AboutPanel
    {
        public void ShowAbout()
        {
            var assembly = Assembly.GetExecutingAssembly();
            var title = assembly.GetCustomAttribute<AssemblyTitleAttribute>()?.Title ?? "N/A";
            var company = assembly.GetCustomAttribute<AssemblyCompanyAttribute>()?.Company ?? "N/A";
            var product = assembly.GetCustomAttribute<AssemblyProductAttribute>()?.Product ?? "N/A";
            var version = assembly.GetName().Version?.ToString() ?? "N/A";

            var message = $"Product Name: {product}\n" +
                          $"Company: {company}\n" +
                          $"Author: Hiyowa Kyobashi\n" +
                          $"Version: {version}" +
                          $"\nOriginally by: ankurk91";

            var toastContent = new ToastContentBuilder()
                .AddText("About")
                .AddText(message)
                .AddButton(new ToastButton()
                    .SetContent("Official GitHub")
                    .AddArgument("action", "openUrl")
                    .AddArgument("url", "https://github.com/KHiyowa/google-chat-desktop"))
                .AddButton(new ToastButton()
                    .SetContent("OK")
                    .AddArgument("action", "dismiss"))
                .GetToastContent();

            var toastNotification = new ToastNotification(toastContent.GetXml())
            {
                ExpirationTime = DateTimeOffset.Now.AddYears(1) // Set to not expire until user clicks
            };

            ToastNotificationManagerCompat.OnActivated += toastArgs =>
            {
                var args = ToastArguments.Parse(toastArgs.Argument);
                if (args["action"] == "openUrl")
                {
                    var url = args["url"];
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = url,
                        UseShellExecute = true
                    });
                }
            };

            ToastNotificationManagerCompat.CreateToastNotifier().Show(toastNotification);
        }
    }
}
