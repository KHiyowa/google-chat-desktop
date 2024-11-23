using System.Windows;

namespace google_chat_desktop.main.features
{
    internal class WindowSettings
    {
        public void LoadWindowSettings(Window window)
        {
            double top = Properties.Settings.Default.WindowTop;
            double left = Properties.Settings.Default.WindowLeft;
            double width = Properties.Settings.Default.WindowWidth;
            double height = Properties.Settings.Default.WindowHeight;
            string screenDeviceName = Properties.Settings.Default.ScreenDeviceName;

            // Set window position and size
            if (width != 0)
            {
                window.Width = width;
            }
            if (height != 0)
            {
                window.Height = height;
            }

            var screen = GetScreenByDeviceName(screenDeviceName);
            if (screen != null)
            {
                // Position the window relative to the upper left corner of the specified display
                window.Left = screen.WorkingArea.Left + left;
                window.Top = screen.WorkingArea.Top + top;
            }
            else
            {
                // Adjust to default position if display not found
                window.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            }
        }

        public void SaveWindowSettings(Window window)
        {
            var screen = Screen.FromRectangle(new Rectangle(
                (int)window.Left,
                (int)window.Top,
                (int)window.Width,
                (int)window.Height));

            if (screen != null)
            {
                // Window position is saved relative to the top left corner of the display
                Properties.Settings.Default.WindowTop = window.Top - screen.WorkingArea.Top;
                Properties.Settings.Default.WindowLeft = window.Left - screen.WorkingArea.Left;
                Properties.Settings.Default.WindowWidth = window.Width;
                Properties.Settings.Default.WindowHeight = window.Height;
                Properties.Settings.Default.ScreenDeviceName = screen.DeviceName;

                Properties.Settings.Default.Save();
            }
        }

        private Screen GetScreenByDeviceName(string deviceName)
        {
            foreach (var screen in Screen.AllScreens)
            {
                if (screen.DeviceName == deviceName)
                {
                    return screen;
                }
            }
            return null;
        }
    }
}
