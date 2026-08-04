using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Filterizer2.Windows
{
    public partial class TagDictionaryWindow
    {
        public TagDictionaryWindow()
        {
            InitializeComponent();
            LoadAllTags();
        }

        private void LoadAllTags()
        {
	        // Assume this method gets all tags from the database
            TagsListBox.ItemsSource = TagRepository.GetTags();
        }

        // Handles the filtering of tags as the user types
        private void TagFilterTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateTagList();
        }

        private void UpdateTagList()
        {
            string filterText = TagFilterTextBox.Text.ToLower();
            TagsListBox.ItemsSource = null;
            TagsListBox.ItemsSource = TagRepository.SearchTags(filterText);
        }

        // Handles the display of tag details when a tag is selected
        private void TagsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateUi();
        }

        private void UpdateUi()
        {
	        TagDetails.DisplayTag(TagsListBox.SelectedItem as TagItem, true);
            if (TagsListBox.SelectedItem is TagItem selectedTag)
            {
	            bool hasParents = selectedTag.ImmediateParentTags.Any();

                DeleteTagButton.IsEnabled = true;
                EditTagButton.IsEnabled = true;

                if (hasParents)
                {
	                TagNameHierarchyPanel.Visibility = Visibility.Visible;
	                TagNameHierarchy.ItemsSource = new[] { selectedTag };
                }
                else
                {
	                TagNameHierarchyPanel.Visibility = Visibility.Hidden;
                }
            }
            else
            {
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

        private void IOTagDictionary_OnClick(object sender, RoutedEventArgs e)
        {
	        var tagDictionaryIOWindow = new TagDictionaryIO();
	        tagDictionaryIOWindow.ShowDialog();
	        UpdateUi();
	        UpdateTagList();
        }

        private void ButtonBase_OnClick(object sender, RoutedEventArgs e)
        {
	        var tagHierarchyWindow = new ViewMasterTagHierarchy();
	        tagHierarchyWindow.ShowDialog();
        }
    }
}