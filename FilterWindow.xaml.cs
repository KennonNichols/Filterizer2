using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Filterizer2
{
    public partial class FilterWindow : Window
    {
        private List<TagItem> _allTags = new List<TagItem>();

        public MediaSearchFilter Filter;
        private MainWindow parent;
        
        public FilterWindow(MainWindow mainWindow, MediaSearchFilter? filter = null)
        {
            InitializeComponent();

            if (filter is SearchFilterOpen or null)
            {
                filter = new MediaSearchFilter(new List<TagFilter>());
            }

            parent = mainWindow;
            Filter = filter;
            
            UpdateFilters();
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            
            parent.ReloadAllMediaItems();
        }

        // Handles the filtering of tags as the user types
        private void TagFilterTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            string filterText = TagFilterTextBox.Text.ToLower();
            
            
            /*var filteredTags = _allTags
                .Where(tag => tag.Name.Contains(filterText, StringComparison.CurrentCultureIgnoreCase) || 
                              tag.Aliases.Any(alias => alias.Contains(filterText, StringComparison.CurrentCultureIgnoreCase)))
                .ToList();*/

            TagsListBox.ItemsSource = TagRepository.SearchTags(filterText);
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
                TagDescriptionBox.Text = selectedTag.Description;
            }
            else
            {
                TagDescriptionBox.Text = "";
            }
        }

        private void TagSelection(object sender, RoutedEventArgs e)
        {
            if (FilterListBox.SelectedItem is not TagFilter filter) return;
            
            //If this is a fresh filter, add it to the main filter
            if (filter.Tags.Count == 0)
            {
                Filter.Filters.Add(filter);
            }
                
            TagItem tag = (TagItem)((Button)sender).Tag;
            if (!filter.Tags.Contains(tag))
            {
                filter.Tags.Add(tag);  
            }      
                
            UpdateFilters();
        }

        private void DeleteFilter(object sender, RoutedEventArgs e)
        {
            TagFilter filter = (TagFilter)((Button)sender).Tag;
            Filter.Filters.Remove(filter);
            
            UpdateFilters();
        }

        private void UpdateFilters()
        {
            FilterListBox.Items.Clear();
            foreach (TagFilter filter in Filter.Filters)
            {
                FilterListBox.Items.Add(filter);
            }
            FilterListBox.Items.Add(new TagFilter(new List<TagItem>()));
        }
    }

}