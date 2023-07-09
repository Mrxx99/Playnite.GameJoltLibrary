using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;

namespace GameJoltLibrary
{
    public partial class GameJoltLibrarySettingsView : UserControl
    {
        public GameJoltLibrarySettingsView()
        {
            InitializeComponent();
        }

        private void Hyperlink_Click(object sender, RoutedEventArgs e)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "https://github.com/Mrxx99/Playnite.GameJoltLibrary",
                UseShellExecute = true
            });
        }
    }
}