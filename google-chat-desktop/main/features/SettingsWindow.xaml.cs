using System.Configuration;
using System.Windows;

namespace google_chat_desktop.main.features
{
    public partial class SettingsWindow : Window
    {
        public SettingsWindow()
        {
            InitializeComponent();
            TxtOpeningBrackets.Text = Properties.Settings.Default.OpeningBrackets;
            TxtClosingBrackets.Text = Properties.Settings.Default.ClosingBrackets;
            TxtIgnoredWords.Text = Properties.Settings.Default.IgnoredWords;
        }

        public static void ShowSettings()
        {
            var window = new SettingsWindow
            {
                Owner = System.Windows.Application.Current.MainWindow
            };
            window.ShowDialog();
        }

        private void Reset_Click(object sender, RoutedEventArgs e)
        {
            TxtOpeningBrackets.Text = Properties.Settings.Default.Properties["OpeningBrackets"]?.DefaultValue as string ?? "";
            TxtClosingBrackets.Text = Properties.Settings.Default.Properties["ClosingBrackets"]?.DefaultValue as string ?? "";
            TxtIgnoredWords.Text = Properties.Settings.Default.Properties["IgnoredWords"]?.DefaultValue as string ?? "";
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            Properties.Settings.Default.OpeningBrackets = TxtOpeningBrackets.Text;
            Properties.Settings.Default.ClosingBrackets = TxtClosingBrackets.Text;
            Properties.Settings.Default.IgnoredWords = TxtIgnoredWords.Text;
            Properties.Settings.Default.Save();
            this.Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
