using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

namespace Filterizer2.Windows
{
	public partial class MediaTaggingHelperWindow : Window
	{
		private List<TagItem> _preexistingTags = new List<TagItem>();
		private int _categoryIndex = 0;
		private int _subcategoryIndex = 0;

		private TagCategory? _currentCategory;
		private TagSubCategory? _currentSubCategory;
		
		private readonly ObservableCollection<TagItem> _masterTags = new();
		private readonly ObservableCollection<TagDisplayChildingItem> _queueTags = new();
		private readonly ObservableCollection<TagDisplayChildingItem> _searchResults = new();
		private readonly ObservableCollection<TagDisplayChildingItem> _childOfQueue = new();

		public ObservableCollection<TagItem> MasterTags => _masterTags;
		public ObservableCollection<TagDisplayChildingItem> QueueTags => _queueTags;
		public ObservableCollection<TagDisplayChildingItem> SearchResults => _searchResults;
		public ObservableCollection<TagDisplayChildingItem> ChildOfQueue => _childOfQueue;
		
		private Dictionary<string, HashSet<int>> _tagChildrenCache = new Dictionary<string, HashSet<int>>();
		
		public MediaTaggingHelperWindow()
		{
			InitializeComponent();
			DataContext = this;
			_currentCategory = Tags.TagCategoriesInTaggingOrder[0];
			_currentSubCategory = _currentCategory.SubcategoriesInOrder[0];
			UpdateContentForCurrentCategories();
			_masterTags.CollectionChanged += Tags_CollectionChanged;
			_queueTags.CollectionChanged += Tags_CollectionChanged;
		}

		private ObservableCollection<TagItem>? originalWindowTags = null;

		public void SetStartingTagList(ref ObservableCollection<TagItem> tagItems)
		{
			foreach (TagItem tagItem in tagItems)
			{
				_preexistingTags.Add(tagItem);
			}

			originalWindowTags = tagItems;
		}
		
		private void MoveToNextSubcategory()
		{
			if (_queueTags.Count > 0)
			{
				suppressCollectionChanged = true;
				foreach (TagDisplayChildingItem queueTag in _queueTags)
				{
					if (!queueTag.IsImplied)
					{
						_masterTags.Add(queueTag.Tag);
					}
				}
				_queueTags.Clear();
				suppressCollectionChanged = false;
				RefreshForDirtyCollection();
			}

			_subcategoryIndex++;
			if (_subcategoryIndex >= _currentCategory.SubcategoriesInOrder.Count)
			{
				_categoryIndex++;
				if (_categoryIndex >= Tags.TagCategoriesInTaggingOrder.Count)
				{
					if (MessageBox.Show(
						    "You have reached the end of tagging flow. Are you finished?",
						    "Confirm tags?",
						    MessageBoxButton.YesNo,
						    MessageBoxImage.Question
					    ) == MessageBoxResult.Yes)
					{
						if (originalWindowTags == null)
						{
							throw new Exception("Opened media tagging helper without list of tags to write to.");
						}
						originalWindowTags.Clear();
						foreach (TagItem masterTag in _masterTags)
						{
							originalWindowTags.Add(masterTag);
						}
						Close();
						return;
					}
					else
					{
						Reset();
					}
				}
				else
				{
					_currentCategory = Tags.TagCategoriesInTaggingOrder[_categoryIndex];
					_subcategoryIndex = 0;
					_currentSubCategory = _currentCategory.SubcategoriesInOrder[0];
				}
			}
			else
			{
				_currentSubCategory = _currentCategory.SubcategoriesInOrder[_subcategoryIndex];
			}

			if (!TagRepository.CheckAnyTagsOfSubcategoryExist(_currentSubCategory))
			{
				//If there are no tags in this subcat, we skip it
				MoveToNextSubcategory();
				return;
			}
			
			//Remove parent cache, since those tags will most likely never be looked at again
			_tagChildrenCache.Clear();
			SearchTextBox.Text = "";
			UpdateContentForCurrentCategories();
		}

		private void Reset()
		{
			foreach (TagItem masterTag in _masterTags)
			{
				_preexistingTags.Add(masterTag);
			}
			_masterTags.Clear();
			_categoryIndex = 0;
			_subcategoryIndex = 0;
			_currentCategory = Tags.TagCategoriesInTaggingOrder[0];
			_currentSubCategory = _currentCategory.SubcategoriesInOrder[0];
			UpdateContentForCurrentCategories();
		}

		private void UpdateContentForCurrentCategories()
		{
			HeaderTextBlock.Text = _currentCategory.Title + " > " + _currentSubCategory.Title;
			SubcategoryDescriptionTextBlock.Text = _currentSubCategory.Description;
			QueueHeader.Text = $"Queue ({_currentSubCategory.Title})";

			//Add preexisting tags to the queue where they belong
			List<TagItem> transferredTags = new List<TagItem>();
			HashSet<int> currentTagIDs = GetCurrentTagCollectionIds();
			bool anyFound = false;
			foreach (TagItem preExistingTag in _preexistingTags )
			{
				if (Equals(preExistingTag.SubCategory, _currentSubCategory))
				{
					anyFound = true;
					transferredTags.Add(preExistingTag);
					//Add that tag to the current list
					currentTagIDs.Add(preExistingTag.Id);
				}
			}

			if (anyFound)
			{
				//Temporarily suppress collection updating, as we might add a large number of tags and do not need to refresh every time
				suppressCollectionChanged = true;
				foreach (TagItem transferredTag in transferredTags)
				{
					_preexistingTags.RemoveAll(t => Equals(t, transferredTag));
					_queueTags.Add(new TagDisplayChildingItem(transferredTag, IsTagImplied(transferredTag, currentTagIDs)));
				}
				suppressCollectionChanged = false;
				//Refresh once
				RefreshForDirtyCollection();
			}
			else
			{
				RefreshSearchResults();
			}
		}

		private HashSet<int>? _currentTagCollectionIdsCache;
		private HashSet<int> GetCurrentTagCollectionIds()
		{
			return _currentTagCollectionIdsCache ??=
			[
				.._masterTags.Select(t => t.Id),
				.._queueTags.Select(t => t.Tag.Id)
			];
		}

		private void RefreshSearchResults()
		{
			string search = SearchTextBox.Text.Trim();

			HashSet<int> blacklist = GetCurrentTagCollectionIds();

			IEnumerable<TagItem> tags = TagRepository.SearchTags(search, _currentSubCategory, blacklist);

			_searchResults.Clear();

			foreach (TagDisplayChildingItem result in tags
				         .Select(t => new TagDisplayChildingItem(t, IsTagImplied(t, blacklist)))
				         .OrderBy(r => r.IsImplied)
				         .ThenBy(r => r.Tag.Name))
			{
				_searchResults.Add(result);
			}
		}

		private bool suppressCollectionChanged = false;
		private void Tags_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
		{
			if (!suppressCollectionChanged)
			{
				RefreshForDirtyCollection();
			}
		}

		private void RefreshForDirtyCollection()
		{
			_currentTagCollectionIdsCache = null;
			RefreshQueueChilding();
			RefreshSearchResults();
		}

		private bool _stifleAllSelectionChangedEvents = false;
		
		private void DeselectAllThatAreNotSelected(object sender)
		{
			_stifleAllSelectionChangedEvents = true;
			DeselectIfSelected(SearchListBox, sender);
			DeselectIfSelected(QueueListBox, sender);
			DeselectIfSelected(ChildListBox, sender);
			DeselectIfSelected(MasterListBox, sender);
			_stifleAllSelectionChangedEvents = false;
		}

		private void DeselectIfSelected(ListBox box, object sender)
		{
			if (Equals(sender, box))
			{
				return;
			}
			if (box.SelectedItem != null)
			{
				box.SelectedItem = null;
			}
		}

		private bool IsTagImplied(TagItem tag, HashSet<int> currentSelectedTags)
		{
			HashSet<int> children;
			if (!_tagChildrenCache.TryGetValue(tag.Name, out children))
			{
				children = TagRepository.GetAllTagIdsChildOf(tag.Id, null, true).ToHashSet();
				// parents = tag.GetAllParentIdsRecursive().ToHashSet();
				_tagChildrenCache[tag.Name] = children;
			}
			return children.Any(currentSelectedTags.Contains);
		}

		private void RefreshQueueChilding()
		{
			if (_queueTags.Count > 0)
			{
				HashSet<int> currentTags = GetCurrentTagCollectionIds();
				// bool anyChange = false;
				foreach (TagDisplayChildingItem tagDisplayChildingItem in _queueTags)
				{
					// anyChange = true;
					tagDisplayChildingItem.IsImplied = IsTagImplied(tagDisplayChildingItem.Tag, currentTags);
				}
			
				// if (anyChange)
				// {
				// 	QueueListBox.Items.Refresh();
				// }
			}
		}
		
		private void ConfirmSubcategoryButton_Click(object sender, RoutedEventArgs e)
		{
			MoveToNextSubcategory();
		}

		private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
		{
			RefreshSearchResults();
		}

		private void SearchListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			if (_stifleAllSelectionChangedEvents)
			{
				return;
			}
			DeselectAllThatAreNotSelected(sender);
			if (sender is ListBox { SelectedItem: TagDisplayChildingItem selectedItem })
			{
				TagDetails.DisplayTag(selectedItem.Tag, false);
			}
		}

		private void SearchListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
		{
			//Move the item to the queueList, and update search
			if (sender is ListBox { SelectedItem: TagDisplayChildingItem selectedItem })
			{
				_queueTags.Add(selectedItem);
				//Automatically select it
				QueueListBox.SelectedItem = selectedItem.Tag;
				QueueListBox.ScrollIntoView(QueueListBox.SelectedItem);
			}
		}

		private void QueueListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			if (_stifleAllSelectionChangedEvents)
			{
				return;
			}
			DeselectAllThatAreNotSelected(sender);
			if (sender is ListBox box)
			{
				TagDetails.DisplayTag((box.SelectedItem as TagDisplayChildingItem)?.Tag, false);
				RepopulateChildListBox();
			}
		}

		private void RepopulateChildListBox()
		{
			if (QueueListBox.SelectedItem is TagDisplayChildingItem parentTagItem)
			{
				//Show children
				HashSet<int> blacklist =
				[
					.._masterTags.Select(t => t.Id),
					.._queueTags.Select(t => t.Tag.Id)
				];

				bool anyFound = false;
				_childOfQueue.Clear();
				foreach (TagItem childTag in TagRepository.GetAllTagsChildOf(parentTagItem.Tag.Id, blacklist))
				{
					anyFound = true;
					_childOfQueue.Add(new TagDisplayChildingItem(childTag, IsTagImplied(childTag, blacklist)));
				}

				if (anyFound)
				{
					ChildListHeader.Text = "Children of " + parentTagItem.Tag.Name;
					ChildListHeader.Foreground = Brushes.Black;
				}
				else
				{
					ChildListHeader.Text = "Children of " + parentTagItem.Tag.Name + " (None)";
					ChildListHeader.Foreground = Brushes.Gray;
				}
			}
			else
			{
				_childOfQueue.Clear();
				ChildListHeader.Text = "Children of Selected Queue Tag";
				ChildListHeader.Foreground = Brushes.Gray;
			}
		}
		
		private void QueueListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
		{
			//Send it back to the parent
			if (sender is ListBox { SelectedItem: TagDisplayChildingItem selectedItem })
			{
				_queueTags.Remove(selectedItem);
				RefreshSearchResults();
			}
		}

		private void ChildListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			if (_stifleAllSelectionChangedEvents)
			{
				return;
			}
			DeselectAllThatAreNotSelected(sender);
			if (sender is ListBox { SelectedItem: TagItem tagItem })
			{
				TagDetails.DisplayTag(tagItem, false);
			}
		}

		private void ChildListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
		{
			//Send it to the queue
			if (sender is ListBox { SelectedItem: TagDisplayChildingItem selectedItem })
			{
				_queueTags.Add(selectedItem);
				RepopulateChildListBox();
			}
		}

		private void MasterListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			if (_stifleAllSelectionChangedEvents)
			{
				return;
			}
			DeselectAllThatAreNotSelected(sender);
			if (sender is ListBox { SelectedItem: TagItem tagItem })
			{
				TagDetails.DisplayTag(tagItem, false);
			}
		}
		
		public class TagDisplayChildingItem : INotifyPropertyChanged
		{
			public TagItem Tag { get; }

			private bool _isImplied;
			public bool IsImplied
			{
				get => _isImplied;
				set
				{
					if (_isImplied == value)
						return;

					_isImplied = value;
					
					OnPropertyChanged(nameof(IsImplied));
					OnPropertyChanged(nameof(Foreground));
				}
			}

			public Brush Foreground => IsImplied ? Brushes.Gray : Brushes.Black;

			public TagDisplayChildingItem(TagItem tag, bool isImplied)
			{
				Tag = tag;

				_isImplied = isImplied;
			}

			public override string ToString() => Tag.Name;
			
			public event PropertyChangedEventHandler? PropertyChanged;
			
			private void OnPropertyChanged(string propertyName)
			{
				PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
			}
		}
	}
	
}