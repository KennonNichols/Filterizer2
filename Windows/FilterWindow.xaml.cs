using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Button = System.Windows.Controls.Button;
using KeyEventArgs = System.Windows.Forms.KeyEventArgs;
using TextBox = System.Windows.Controls.TextBox;

namespace Filterizer2.Windows
{
    public partial class FilterWindow
    {
        public readonly MediaSearchFilter Filter;
        private readonly IHasFilter _parent;
        
        public FilterWindow(IHasFilter mainWindow, MediaSearchFilter? filter = null)
        {
            InitializeComponent();

            if (filter is SearchFilterOpen or null)
            {
                filter = new MediaSearchFilter(new List<TagFilter>());
            }

            _parent = mainWindow;
            Filter = filter;
            
            UpdateFilters();
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            
            _parent.SetFilter(Filter);
            _parent.ReloadAllMediaItems();
        }

        // private HashSet<int> _tempAllIDs = new HashSet<int>();
        // Handles the filtering of tags as the user types
        private void TagFilterTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            string filterText = TagFilterTextBox.Text.ToLower();

            if (filterText.Length <= 1)
            {
	            TagsListBox.ItemsSource = null;
	            return;
            }
            
            /*var filteredTags = _allTags
                .Where(tag => tag.Name.Contains(filterText, StringComparison.CurrentCultureIgnoreCase) || 
                              tag.Aliases.Any(alias => alias.Contains(filterText, StringComparison.CurrentCultureIgnoreCase)))
                .ToList();*/
            
            TagsListBox.ItemsSource = TagRepository.SearchTags(filterText);
        }

        // Handles the display of tag details when a tag is selected
        private void TagsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
	        TagDetailsControl.DisplayTag(TagsListBox.SelectedItem as TagItem, true);
        }

        private void TagsListBox_OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
	        if (FilterListBox.SelectedItem is not TagFilter filter) return;
	        if (TagsListBox.SelectedItem is not TagItem tag) return;

	        bool isFresh = filter.IsEmpty;
            
	        //If this is a fresh filter, add it to the main filter
	        if (isFresh)
	        {
		        Filter.Filters.Add(filter);
	        }
                
	        filter.AddTag(tag);  
                
	        //if this wasn't a fresh filter, we mark that it is an edit filter operation
	        UpdateFilters(!isFresh);
        }

        // private void TagSelection(object sender, RoutedEventArgs e)
        // {
        //     if (FilterListBox.SelectedItem is not TagFilter filter) return;
        //
        //     bool isFresh = filter.IsEmpty;
        //     
        //     //If this is a fresh filter, add it to the main filter
        //     if (isFresh)
        //     {
        //         Filter.Filters.Add(filter);
        //     }
        //         
        //     TagItem tag = (TagItem)((Button)sender).Tag;
        //     filter.AddTag(tag);  
        //         
        //     //if this wasn't a fresh filter, we mark that it is an edit filter operation
        //     UpdateFilters(!isFresh);
        // }

        private void DeleteFilter(object sender, RoutedEventArgs e)
        {
            TagFilter filter = (TagFilter)((Button)sender).Tag;
            Filter.Filters.Remove(filter);
            
            UpdateFilters();
        }
        
        private void InvertFilter(object sender, RoutedEventArgs e)
        {
	        TagFilter filter = (TagFilter)((Button)sender).Tag;
	        filter.Inverted = !filter.Inverted;
	        
	        UpdateFilters(true);
        }

        private void UpdateFilters(bool editingExistingFilter = false)
        {
	        int index = FilterListBox.SelectedIndex;
            FilterListBox.Items.Clear();
            foreach (TagFilter filter in Filter.Filters)
            {
                FilterListBox.Items.Add(filter);
            }
            FilterListBox.Items.Add(new TagFilter(new HashSet<TagItem>()));

            //If we were tweaking a filter, reselect the one the user was working on
            if (editingExistingFilter)
            {
	            FilterListBox.SelectedIndex = index;
            }
            //Otherwise, select the newly made filter
            else
            {
	            FilterListBox.SelectedIndex = FilterListBox.Items.Count - 1;
            }
        }
        
        
        [GeneratedRegex("[@$\"\\s>!]")]
        private static partial Regex ForbiddenSearchChars();
		
        private void SearchBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
	        e.Handled = ForbiddenSearchChars().IsMatch(e.Text);
        }

        private void SearchBox_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
	        if (sender is not TextBox textBox) return;

	        if (e.Key == Key.Space)
	        {
		        int caretIndex = textBox.CaretIndex;
		        textBox.Text = textBox.Text.Remove(textBox.SelectionStart, textBox.SelectionLength)
			        .Insert(caretIndex, "_");
		        textBox.CaretIndex = caretIndex + 1;
		        e.Handled = true;
	        }
        }

        private void SearchBox_Pasting(object sender, DataObjectPastingEventArgs e)
        {
	        if (sender is not TextBox textBox) return;

	        if (e.DataObject.GetDataPresent(typeof(string)))
	        {
		        string pastedText = (string)e.DataObject.GetData(typeof(string));

		        pastedText = pastedText.Replace(" ", "_");
		        

		        string cleanedText = ForbiddenSearchChars().Replace(pastedText, string.Empty);

		        int selectionStart = textBox.SelectionStart;
		        int selectionLength = textBox.SelectionLength;

		        string currentText = textBox.Text;
		        string newText = currentText.Remove(selectionStart, selectionLength)
			        .Insert(selectionStart, cleanedText);

		        textBox.Text = newText;

		        textBox.SelectionStart = selectionStart + cleanedText.Length;
	        }
	        
	        e.CancelCommand();
        }
    }

}