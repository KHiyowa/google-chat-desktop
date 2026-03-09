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
        }

        public static void ShowSettings()
        {
            var window = new SettingsWindow
            {
                Owner = System.Windows.Application.Current.MainWindow
            };
            window.ShowDialog();
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            Properties.Settings.Default.OpeningBrackets = TxtOpeningBrackets.Text;
            Properties.Settings.Default.ClosingBrackets = TxtClosingBrackets.Text;
            Properties.Settings.Default.Save();
            this.Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}