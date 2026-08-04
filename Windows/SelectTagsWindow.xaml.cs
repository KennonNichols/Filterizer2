using System.Windows;
using System.Windows.Controls;
using Button = System.Windows.Controls.Button;

namespace Filterizer2.Windows
{
    public partial class SelectTagsWindow
    {
	    private readonly List<TagItem> _tagList;
	    private readonly Action<List<TagItem>> _onComplete;
	    private readonly TagItem? _childTag;
        
        public SelectTagsWindow( Action<List<TagItem>> onTagSelectComplete, List<TagItem>? preexistingTags = null, TagItem? childTag = null)
        {
            InitializeComponent();

            //If we are working on a fresh tag that hasn't even been built yet, there will be no parents.
            if (preexistingTags != null)
            {
	            _tagList = preexistingTags;
            }
            else
            {
	            _tagList = new List<TagItem>();
            }
            _onComplete = onTagSelectComplete;
            _childTag = childTag;

            UpdateTags();
        }

        protected override void OnClosed(EventArgs e)
        {
	        base.OnClosed(e);
	        
	        _onComplete.Invoke(_tagList);
        }

        //Handles the filtering of tags as the user types
        private void TagFilterTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            string filterText = TagFilterTextBox.Text.ToLower();
            
            Func<TagItem, bool> tagValidator;
            if (_childTag == null)
            {
	            //If the child doesn't yet exist, then any tag could be a parent.
	            tagValidator = _ => true;
            }
            else
            {
	            //Otherwise, we can't parent a child to their own parent.
	            tagValidator = tag => !tag.GetAllParentIdsRecursive().Contains(_childTag.Id);
            }
            
            TagsListBox.ItemsSource = TagRepository.SearchTags(filterText).Where(tagValidator);
        }

        //Handles the display of tag details when a tag is selected
        private void TagsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateUi();
        }

        private void UpdateUi()
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
	        TagItem tag = (TagItem)((Button)sender).Tag;

            
            if (_tagList.Any(item => item.Id == tag.Id)) return;

            //If the tag we want to add is a child of this tag (or is this tag) we ignore it. (Old logic, we validate the tags we show now)
            // if (tag.GetTagHierarchyIds().Contains(_childTag.Id))
            // {
	           //  MessageBox.Show("Adding this tag would create circular tag parenthood.", "Parenting error",
		          //   MessageBoxButton.OK, MessageBoxImage.Error);
	           //  return;
            // }
            
            _tagList.Add(tag);
            UpdateTags();
        }

        private void DeleteFilter(object sender, RoutedEventArgs e)
        {
            TagItem filter = (TagItem)((Button)sender).Tag;
            _tagList.Remove(filter);
            
            UpdateTags();
        }

        private void UpdateTags()
        {
            FilterListBox.Items.Clear();
            foreach (TagItem filter in _tagList)
            {
                FilterListBox.Items.Add(filter);
            }
        }
    }

}