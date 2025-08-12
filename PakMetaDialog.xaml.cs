using System.Windows;
using System.Windows.Controls;

namespace BrickRigsModManager
{
    public partial class PakMetaDialog : Window
    {
        public string ModName { get; private set; }
        public string ModAuthor { get; private set; }
        public string ModVersion { get; private set; }
        public string ModDescription { get; private set; }
        public string ModCategory { get; private set; }

        public PakMetaDialog(string defaultName = null)
        {
            InitializeComponent();

            if (!string.IsNullOrEmpty(defaultName))
            {
                NameTextBox.Text = defaultName;
            }

            CategoryComboBox.SelectedIndex = 4; // Default to Miscellaneous
        }

        private void Create_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(NameTextBox.Text))
            {
                MessageBox.Show("Please enter a mod name.", "Missing Information",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            ModName = NameTextBox.Text.Trim();
            ModAuthor = string.IsNullOrWhiteSpace(AuthorTextBox.Text) ? "Unknown" : AuthorTextBox.Text.Trim();
            ModVersion = string.IsNullOrWhiteSpace(VersionTextBox.Text) ? "1.0" : VersionTextBox.Text.Trim();
            ModDescription = string.IsNullOrWhiteSpace(DescriptionTextBox.Text) ?
                "No description available" : DescriptionTextBox.Text.Trim();

            var selectedCategory = CategoryComboBox.SelectedItem as ComboBoxItem;
            ModCategory = selectedCategory?.Content.ToString() ?? "Miscellaneous";

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
