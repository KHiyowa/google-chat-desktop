using System.Reflection;
using System.Windows;

namespace google_chat_desktop.features
{
    public class AboutPanel
    {
        private static bool isEventHandlerRegistered = false;

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
                  $"Version: {version}\n\n" +
                  $"Originally by: ankurk91";

            System.Windows.MessageBox.Show(message, "About", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}
