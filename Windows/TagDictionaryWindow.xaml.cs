using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Filterizer2.Windows
{
    public partial class TagDictionaryWindow
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
            UpdateTagList();
        }

        private void UpdateTagList()
        {
            string filterText = TagFilterTextBox.Text.ToLower();
            TagsListBox.ItemsSource = TagRepository.SearchTags(filterText);
        }

        // Handles the display of tag details when a tag is selected
        private void TagsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateUi();
        }

        private void UpdateUi()
        {
            if (TagsListBox.SelectedItem is TagItem selectedTag)
            {
                TagTitleTextBlock.Text = selectedTag.Name;
                TagDescriptionTextBlock.Text = selectedTag.Description;
                TagAliasesTextBlock.Text = selectedTag.Aliases.Any() 
                    ? "Aliases: " + string.Join(", ", selectedTag.Aliases) 
                    : "No Aliases";

                // Set the border color based on the TagType
                TagDetailsBorder.BorderBrush = new SolidColorBrush(selectedTag.Category.Color);

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
            UpdateTagList();
        }

        private void EditTagButton_Click(object sender, RoutedEventArgs e)
        {
            if (TagsListBox.SelectedItem is not TagItem selectedTag) return;
            var createTagWindow = new EditTagWindow(selectedTag);
            createTagWindow.ShowDialog();
            UpdateUi();
            UpdateTagList();
        }

        private void NewTagButton_Click(object sender, RoutedEventArgs e)
        {
            var createTagWindow = new EditTagWindow();
            createTagWindow.ShowDialog();
            UpdateUi();
            UpdateTagList();
        }
    }

}