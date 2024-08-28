using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Filterizer2
{
    public partial class TagDictionaryWindow : Window
    {
        private List<TagItem> _allTags = new List<TagItem>();

        public TagDictionaryWindow()
        {
            InitializeComponent();
            LoadAllTags();
        }

        private void LoadAllTags()
        {
            _allTags = TagRepository.GetTags(); // Assume this method gets all tags from the database
            TagsListBox.ItemsSource = _allTags;
        }

        // Handles the filtering of tags as the user types
        private void TagFilterTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            string filterText = TagFilterTextBox.Text.ToLower();
            var filteredTags = _allTags
                .Where(tag => tag.Name.Contains(filterText, StringComparison.CurrentCultureIgnoreCase) || 
                              tag.Aliases.Any(alias => alias.Contains(filterText, StringComparison.CurrentCultureIgnoreCase)))
                .ToList();

            TagsListBox.ItemsSource = filteredTags;
        }

        // Handles the display of tag details when a tag is selected
        private void TagsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateUI();
        }

        private void UpdateUI()
        {
            if (TagsListBox.SelectedItem is TagItem selectedTag)
            {
                TagTitleTextBlock.Text = selectedTag.Name;
                TagDescriptionTextBlock.Text = selectedTag.Description;
                TagAliasesTextBlock.Text = selectedTag.Aliases.Any() 
                    ? "Aliases: " + string.Join(", ", selectedTag.Aliases) 
                    : "No Aliases";

                // Set the border color based on the TagType
                TagDetailsBorder.BorderBrush = new SolidColorBrush(selectedTag.Category.GetColor());

                DeleteTagButton.IsEnabled = true;
                EditTagButton.IsEnabled = true;
            }
            else
            {
                // Clear the details if no tag is selected
                TagTitleTextBlock.Text = string.Empty;
                TagDescriptionTextBlock.Text = string.Empty;
                TagAliasesTextBlock.Text = string.Empty;
                TagDetailsBorder.BorderBrush = Brushes.Gray;
                
                DeleteTagButton.IsEnabled = false;
                EditTagButton.IsEnabled = false;
            }
        }
        
        private void DeleteTagButton_Click(object sender, RoutedEventArgs e)
        {
            if (TagsListBox.SelectedItem is TagItem selectedTag)
            {
                if (ManagementHelpers.ShowConfirmationDialog($"Delete the tag '{selectedTag.Name}'"))
                {
                    TagRepository.DeleteTag(selectedTag);
                }
            }
        }

        private void EditTagButton_Click(object sender, RoutedEventArgs e)
        {
            if (TagsListBox.SelectedItem is TagItem selectedTag)
            {
                var createTagWindow = new EditTagWindow(selectedTag);
                createTagWindow.ShowDialog();
                UpdateUI();
            }
        }

        private void NewTagButton_Click(object sender, RoutedEventArgs e)
        {
            var createTagWindow = new EditTagWindow();
            createTagWindow.ShowDialog();
            UpdateUI();
        }
    }

}