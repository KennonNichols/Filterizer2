using System.Data.SQLite;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Filterizer2
{
    public partial class CreateTagWindow : Window
    {
        public CreateTagWindow()
        {
            InitializeComponent();

            // Populate the ComboBox with TagType enum values
            TagTypeComboBox.ItemsSource = Enum.GetValues(typeof(TagCategory)).Cast<TagCategory>();
            TagTypeComboBox.SelectedIndex = 0; // Select the first item by default

            // Set the initial colors and description based on the default selection
            UpdateUIForSelectedTagType((TagCategory)TagTypeComboBox.SelectedItem);
        }

        private void TagTypeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Update the UI when a new TagType is selected
            if (TagTypeComboBox.SelectedItem is TagCategory selectedTagType)
            {
                UpdateUIForSelectedTagType(selectedTagType);
            }
        }

        private void UpdateUIForSelectedTagType(TagCategory tagType)
        {
            // Get the color, title, and description for the selected TagType
            var color = tagType.GetColor();
            var description = tagType.GetDescription();

            // Update the border colors
            MainBorder.BorderBrush = new SolidColorBrush(color);
            TagNameTextBox.BorderBrush = new SolidColorBrush(color);
            TagTypeComboBox.BorderBrush = new SolidColorBrush(color);
            TagDescriptionTextBox.BorderBrush = new SolidColorBrush(color);

            // Update the description text block
            TagDescriptionTextBlock.Text = description;
            TagDescriptionTextBlock.Foreground = new SolidColorBrush(color);
        }

        private void CreateTagButton_Click(object sender, RoutedEventArgs e)
        {
            string tagName = TagNameTextBox.Text.Trim();
            TagCategory selectedTagType = (TagCategory)TagTypeComboBox.SelectedItem;
            string tagDescription = TagDescriptionTextBox.Text.Trim();

            if (string.IsNullOrEmpty(tagName))
            {
                MessageBox.Show("Tag Name cannot be empty.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var newTag = new TagItem
            {
                Name = tagName,
                Category = selectedTagType,
                Description = tagDescription
            };

            AddTagToDatabase(newTag);

            this.Close();
        }

        private void AddTagToDatabase(TagItem tag)
        {
            using (var connection = ManagementHelpers.GetAndOpenDatabaseConnection())
            {
                string insertTagQuery = @"
                    INSERT INTO Tags (Name, Category, Description)
                    VALUES (@name, @category, @description);";

                using (var command = new SQLiteCommand(insertTagQuery, connection))
                {
                    command.Parameters.AddWithValue("@name", tag.Name);
                    command.Parameters.AddWithValue("@category", (int)tag.Category);
                    command.Parameters.AddWithValue("@description", tag.Description);
                    command.ExecuteNonQuery();
                }
            }
        }
    }
}