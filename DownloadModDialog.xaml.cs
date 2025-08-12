using System.Windows;

namespace BrickRigsModManager
{
    public partial class DownloadModDialog : Window
    {
        public string ModUrl { get; private set; }

        public DownloadModDialog()
        {
            InitializeComponent();
        }

        private void Download_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(UrlTextBox.Text))
            {
                MessageBox.Show("Please enter a valid URL.", "Invalid URL",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            ModUrl = UrlTextBox.Text.Trim();
            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
