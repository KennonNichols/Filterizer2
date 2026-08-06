using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using ListBox = System.Windows.Controls.ListBox;
using MessageBox = System.Windows.MessageBox;
using TextBox = System.Windows.Controls.TextBox;

namespace Filterizer2.Windows
{
	public partial class MediaTaggingHelperWindow
	{
		private List<TagItem> _preexistingTags = new List<TagItem>();
		private int _categoryIndex = 0;
		private int _subcategoryIndex = 0;

		public bool IsSingle
		{
			get => _isSingle;
			set
			{
				_isSingle = value;
				RefreshSearchResults();
				RepopulateChildListBox();
			}
		}

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

		private Action<List<TagItem>>? _onTagSelectComplete;
		
		public MediaTaggingHelperWindow(string? mediaFilePath, Action<List<TagItem>>? onTagSelectComplete = null)
		{
			InitializeComponent();

			DataContext = this;
			
			_currentCategory = Tags.TagCategoriesInTaggingOrder[0];
			_currentSubCategory = _currentCategory.SubcategoriesInOrder[0];
			UpdateContentForCurrentCategories();


			SearchListBox.ItemContainerGenerator.StatusChanged += OnItemContainerGeneratorOnStatusChanged;
			
			_masterTags.CollectionChanged += MasterTags_CollectionChanged;
			_queueTags.CollectionChanged += Queue_CollectionChanged;
			_onTagSelectComplete = onTagSelectComplete;

			if (mediaFilePath != null)
			{
				MediaPlayer.ShowMedia(mediaFilePath);
			}

			return;

			void OnItemContainerGeneratorOnStatusChanged(object? sender, EventArgs e)
			{
				if (SearchListBox.ItemContainerGenerator.Status == System.Windows.Controls.Primitives.GeneratorStatus.ContainersGenerated)
				{
					FocusListBoxIndex(SearchListBox, 0);
					SearchListBox.ItemContainerGenerator.StatusChanged -= OnItemContainerGeneratorOnStatusChanged;
				}
			}
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
				_suppressCollectionChanged = true;
				foreach (TagDisplayChildingItem queueTag in _queueTags)
				{
					if (!queueTag.IsImplied)
					{
						_masterTags.Add(queueTag.Tag);
					}
				}
				_queueTags.Clear();
				_suppressCollectionChanged = false;
				ForceCollectionChangedHandler();
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
						if (originalWindowTags != null)
						{
							originalWindowTags.Clear();
							foreach (TagItem masterTag in _masterTags)
							{
								originalWindowTags.Add(masterTag);
							}
						}
						else if (_onTagSelectComplete != null)
						{
							_onTagSelectComplete.Invoke(_masterTags.ToList());
						}
						else
						{
							throw new Exception("Opened media tagging helper without list of tags to write to or action to execute.");
						}

						DialogResult = true;
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
			
			//Clear all memories
			_selectionMemories.Clear();
			MoveFocusToListBoxWithDefault(SearchListBox);
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
				if (preExistingTag.Name == "Tagging_In_Progress") continue;
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
				_suppressCollectionChanged = true;
				foreach (TagItem transferredTag in transferredTags)
				{
					_preexistingTags.RemoveAll(t => Equals(t, transferredTag));
					_queueTags.Add(new TagDisplayChildingItem(transferredTag, IsTagImplied(transferredTag, currentTagIDs)));
				}
				_suppressCollectionChanged = false;
				//Refresh once
				ForceCollectionChangedHandler();
			}
			else
			{
				RefreshSearchResults();
			}
			FocusListBoxIndex(SearchListBox, 0);
		}

		/// <summary>
		/// All tags both in the queue and the master collection.
		/// </summary>
		private HashSet<int>? _currentTagCollectionIdsCache;
		/// <summary>
		/// Get all tags both in the queue and the master collection.
		/// </summary>
		/// <returns></returns>
		private HashSet<int> GetCurrentTagCollectionIds()
		{
			return _currentTagCollectionIdsCache ??=
			[
				.._masterTags.Select(t => t.Id),
				.._queueTags.Select(t => t.Tag.Id)
			];
		}
		/// <summary>
		/// All tags in the master collection AND implied by those tags
		/// </summary>
		private HashSet<int>? _currentMasterTagFullCollectionIdsCache;
		private HashSet<int> GetCurrentMasterTagFullCollectionIds()
		{
			if (_currentMasterTagFullCollectionIdsCache != null) return _currentMasterTagFullCollectionIdsCache;
			_currentMasterTagFullCollectionIdsCache = new HashSet<int>();
			
			foreach (TagItem masterTag in _masterTags)
			{
				_currentMasterTagFullCollectionIdsCache.Add(masterTag.Id);
				foreach (int id in masterTag.GetAllParentIdsRecursive())
				{
					_currentMasterTagFullCollectionIdsCache.Add(id);
				}
			}

			return _currentMasterTagFullCollectionIdsCache;
		}
		/// <summary>
		/// All tags in the queue AND implied by those tags
		/// </summary>
		private HashSet<int>? _currentQueueTagFullCollectionIdsCache;
		private HashSet<int> GetCurrentQueueTagFullCollectionIds()
		{
			if (_currentQueueTagFullCollectionIdsCache != null) return _currentQueueTagFullCollectionIdsCache;
			_currentQueueTagFullCollectionIdsCache = new HashSet<int>();
			
			foreach (TagDisplayChildingItem queueTagDisplay in _queueTags)
			{
				TagItem queueTag = queueTagDisplay.Tag;
				_currentQueueTagFullCollectionIdsCache.Add(queueTag.Id);
				foreach (int id in queueTag.GetAllParentIdsRecursive())
				{
					_currentQueueTagFullCollectionIdsCache.Add(id);
				}
			}

			return _currentQueueTagFullCollectionIdsCache;
		}
		/// <summary>
		/// All tags both in the queue and the master collection AND implied by those tags
		/// </summary>
		private HashSet<int>? _currentAllTagFullCollectionIdsCache;
		private HashSet<int> GetCurrentAllTagFullCollectionIdsCache()
		{
			return _currentAllTagFullCollectionIdsCache ??=
			[
				..GetCurrentMasterTagFullCollectionIds(),
				..GetCurrentQueueTagFullCollectionIds()
			];
		}
		
		
		
		protected override void OnClosed(EventArgs e)
		{
			base.OnClosed(e);
			MediaPlayer.Dispose();
		}

		private string _previousSearch;
		private void RefreshSearchResults()
		{
			string search = SearchTextBox.Text.Trim();

			bool shouldRememberPast = true;
			if (_previousSearch != search || _searchResults.Count == 0)
			{
				//If the search changed, forget memories
				_selectionMemories.Remove(SearchListBox);
				shouldRememberPast = false;
			}

			_previousSearch = search;

			HashSet<int> blacklist = GetCurrentTagCollectionIds();

			IEnumerable<TagItem> tags = TagRepository.SearchTags(search, _currentSubCategory, blacklist);
			
			//Filter all tags that are excluded by our current tags if in single mode
			if (IsSingle)
			{
				tags = tags.Where(item => !item.ExcludedByIDs.Any(id => GetCurrentAllTagFullCollectionIdsCache().Contains(id)));
			}
			
			TagItem? rememberedItem;
			List<(TagItem Tag, bool? IsImpliedInNewLife, int NewLifeIndex)> previousTagItemsAndHasBeenImplied =
				new List<(TagItem Tag, bool? IsImpliedInNewLife, int NewLifeIndex)>();
			HashSet<int> foundTagIDs = new HashSet<int>();
			bool isRememberedItemInImplications = false;
			if (shouldRememberPast)
			{
				if (_selectionMemories.TryGetValue(SearchListBox, out SelectionMemory memory))
				{
					TagDisplayChildingItem rememberedDisplayItem;
					if (memory.Index != null)
					{
						int rememberedIndex = (int)memory.Index;
						if (rememberedIndex >= _searchResults.Count)
						{
							rememberedIndex = _searchResults.Count - 1;
						}
						//Special mode if remembering index when this changes
						rememberedDisplayItem = _searchResults[rememberedIndex];
					}
					else if (memory.Item != null)
					{
						rememberedDisplayItem = memory.Item;
					}
					else
					{
						goto SKIPIFUNFOUND;
					}

					rememberedItem = rememberedDisplayItem.Tag;
					isRememberedItemInImplications = rememberedDisplayItem.IsImplied;
					
					//Iterate until we reach the item we want, and then stop. We will later work backwards from that to go to the previous child that isn't implied
					//TODO make sure this works; it can skip further up the list if any items are implied or gone
					int ind = 0;
					foreach (TagDisplayChildingItem tagDisplayChildingItem in _searchResults)
					{
						previousTagItemsAndHasBeenImplied.Add((tagDisplayChildingItem.Tag, null, -1));
						foundTagIDs.Add(tagDisplayChildingItem.Tag.Id);
						
						//Be done if we have reached the selected tag
						if (ind == memory.Index)
						{
							break;
						}
						if (rememberedItem == tagDisplayChildingItem.Tag)
						{
							break;
						}
						
						ind++;
					}

					SKIPIFUNFOUND:;
				}
			}
			
			_searchResults.Clear();

			int index = 0;
			foreach (TagDisplayChildingItem result in tags
				         .Select(t => new TagDisplayChildingItem(t, IsTagImplied(t, blacklist)))
				         .OrderBy(r => r.IsImplied)
				         .ThenBy(r => r.Tag.Id))
			{
				if (shouldRememberPast)
				{
					if (foundTagIDs.Contains(result.Tag.Id))
					{
						previousTagItemsAndHasBeenImplied[
							previousTagItemsAndHasBeenImplied.FindIndex(t => Equals(result.Tag, t.Tag))] = (result.Tag, result.IsImplied, index);
					}
					index++;
				}
				_searchResults.Add(result);
			}
			
			
			if (shouldRememberPast)
			{
				int i;
				//Iterate backwards over all previous tags
				for (i = previousTagItemsAndHasBeenImplied.Count - 1; i >= 0; i--)
				{
					(TagItem tag, bool? isImpliedInNewLife, int newLifeIndex) = previousTagItemsAndHasBeenImplied[i];
					
					//If we never found an answer to whether it was implied, it is no longer in the search results, and must be skipped
					if (isImpliedInNewLife == null)
					{
						continue;
					}
					//If this tag is now in implications, and we were not in implications before, we skip it
					if (isImpliedInNewLife == true && !isRememberedItemInImplications)
					{
						continue;
					}
					//Once we found one that exists and is not implied, break and use that index
					_selectionMemories[SearchListBox] = new SelectionMemory(Index: newLifeIndex);
					break;
				}
				
			}
		}

		private bool _suppressCollectionChanged;

		private void ForceCollectionChangedHandler()
		{
			Queue_CollectionChanged(null, null);
			MasterTags_CollectionChanged(null, null);
		}
		private void Queue_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
		{
			if (!_suppressCollectionChanged)
			{
				_currentQueueTagFullCollectionIdsCache = null;
				Tags_CollectionChanged(sender, e);
			}
		}
		
		private void MasterTags_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
		{
			if (!_suppressCollectionChanged)
			{
				_currentMasterTagFullCollectionIdsCache = null;
				Tags_CollectionChanged(sender, e);
			}
		}

		private void Tags_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
		{
			if (!_suppressCollectionChanged)
			{
				_currentAllTagFullCollectionIdsCache = null;
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
			if (_tagChildrenCache.TryGetValue(tag.Name, out var children))
				return children.Any(currentSelectedTags.Contains);
			children = TagRepository.GetAllTagIdsChildOf(tag.Id, null, true).ToHashSet();
			_tagChildrenCache[tag.Name] = children;
			return children.Any(currentSelectedTags.Contains);
		}

		private void RefreshQueueChilding()
		{
			if (_queueTags.Count <= 0) return;
			HashSet<int> currentTags = GetCurrentTagCollectionIds();
			// bool anyChange = false;
			foreach (TagDisplayChildingItem tagDisplayChildingItem in _queueTags)
			{
				// anyChange = true;
				tagDisplayChildingItem.IsImplied = IsTagImplied(tagDisplayChildingItem.Tag, currentTags);
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
			if (Equals(sender, SearchListBox))
			{
				MoveSelectionBetweenListBoxes(SearchListBox, QueueListBox);
			}
		}

		/// <summary>
		/// Moves the selected item from listBoxToTakeFrom to listBoxToSendTo, and optionally focuses the keyboard on that tag, saving the current one from either item or index move.
		/// </summary>
		/// <param name="listBoxToTakeFrom"></param>
		/// <param name="listBoxToSendTo"></param>
		/// <param name="focusMovedTagMode"></param>
		private void MoveSelectionBetweenListBoxes(ListBox listBoxToTakeFrom, ListBox listBoxToSendTo, FocusSaveMode focusMovedTagMode = FocusSaveMode.None)
		{
			if (listBoxToTakeFrom.SelectedItem is TagDisplayChildingItem selectedItem)
			{
				//Save if focus will change
				if (focusMovedTagMode != FocusSaveMode.None)
				{
					SaveListBoxState(listBoxToTakeFrom, focusMovedTagMode);
				}
				//Remove from original
				((ObservableCollection<TagDisplayChildingItem>)listBoxToTakeFrom.ItemsSource).Remove(selectedItem);
				//Send to new
				((ObservableCollection<TagDisplayChildingItem>)listBoxToSendTo.ItemsSource).Add(selectedItem);
				//Focus
				if (focusMovedTagMode != FocusSaveMode.None)
				{
					FocusListBoxItem(listBoxToSendTo, selectedItem);
				}
			}
		}

		private Dictionary<ListBox, SelectionMemory?> _selectionMemories = new Dictionary<ListBox, SelectionMemory?>();
		private bool _isSingle;

		private void FocusListBoxIndex(ListBox listBox, int index)
		{
			if (listBox.Items.Count == 0)
			{
				listBox.Focus();
				Keyboard.Focus(listBox);
				return;
			}
			
			//TODO make sure it works if the index is 0, -1, and too high
			//If we are past the last one, select that one
			if (index >= listBox.Items.Count)
			{
				index = listBox.Items.Count - 1;
			}
			else if (index < 0)
			{
				index = 0;
			}
			
			FocusListBoxItem(listBox, listBox.Items[index]);
		}
		
		private void FocusListBoxItem(ListBox listBox, object? targetItem)
		{
			if (targetItem == null) return;

			listBox.SelectedItem = targetItem;
			listBox.ScrollIntoView(targetItem);

			//Try to get the ListBoxItem container immediately
			if (listBox.ItemContainerGenerator.ContainerFromItem(targetItem) is ListBoxItem container)
			{
				//Container exists, focus it directly
				container.Focus();
				Keyboard.Focus(container);
			}
			else
			{
				//Container doesn't exist yet (UI is still rendering).
				//Wait until the container generation is finished, then focus it.
				void OnItemContainerGeneratorOnStatusChanged(object? sender, EventArgs e)
				{
					if (listBox.ItemContainerGenerator.Status == System.Windows.Controls.Primitives.GeneratorStatus.ContainersGenerated)
					{
						if (listBox.ItemContainerGenerator.ContainerFromItem(targetItem) is ListBoxItem retryContainer)
						{
							retryContainer.Focus();
							Keyboard.Focus(retryContainer);
							listBox.ItemContainerGenerator.StatusChanged -= OnItemContainerGeneratorOnStatusChanged;
						}
					}
				}

				listBox.ItemContainerGenerator.StatusChanged += OnItemContainerGeneratorOnStatusChanged;
			}
		}

		/// <summary>
		/// Makes the user focus on the chosen ListBox. If it remembers the user having been there, it goes to that remembered spot and clears the memory.
		/// If listBoxToRemember is passed and focusSaveMode is not none, it will save the index/item. This should be the one that the user is moving OFF of.
		/// </summary>
		/// <param name="focusedListBox"></param>
		/// <param name="listBoxToRemember"></param>
		/// <param name="focusSaveMode"></param>
		private void MoveFocusToListBoxWithDefault(ListBox focusedListBox, ListBox? listBoxToRemember = null, FocusSaveMode focusSaveMode = FocusSaveMode.None)
		{
			if (listBoxToRemember != null)
			{
				SaveListBoxState(listBoxToRemember, focusSaveMode);
			}
			
			//Automatically focus and select any saved selections
			if (_selectionMemories.TryGetValue(focusedListBox, out SelectionMemory? memory))
			{
				if (memory!.Index != null)
				{
					FocusListBoxIndex(focusedListBox, (int)memory.Index);
					return;
				}

				if (memory.Item != null)
				{
					FocusListBoxItem(focusedListBox, memory.Item);
					return;
				}
			}
			FocusListBoxIndex(focusedListBox, 0);
		}

		private void SaveListBoxState(ListBox listBox, FocusSaveMode focusSaveMode)
		{
			switch (focusSaveMode)
			{
				case FocusSaveMode.Index:
					_selectionMemories[listBox] =
						new SelectionMemory(Index: listBox.SelectedIndex);
					break;
				case FocusSaveMode.Item:
					_selectionMemories[listBox] =
						new SelectionMemory(Item: listBox.SelectedItem as TagDisplayChildingItem);
					break;
				case FocusSaveMode.None:
					break;
				default:
					throw new ArgumentOutOfRangeException(nameof(focusSaveMode), focusSaveMode, null);
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
					//Filter all tags that are excluded by our current tags if in single mode
					if (IsSingle && childTag.ExcludedByIDs.Any(id => GetCurrentAllTagFullCollectionIdsCache().Contains(id)))
					{
						continue;
					}
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
			//Clear the memory of the child list box
			_selectionMemories.Remove(ChildListBox);
		}
		
		private void QueueListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
		{
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
			if (sender is ListBox { SelectedItem: TagDisplayChildingItem selectedItem })
			{
				TagDetails.DisplayTag(selectedItem.Tag, false);
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

		private void CreateTagButton_OnClick(object sender, RoutedEventArgs e)
		{
			EditTagWindow editTagWindow = new EditTagWindow(subCategory: _currentSubCategory);
			MediaPlayer.PausePlayer();
			editTagWindow.ShowDialog();
			RefreshSearchResults();
		}

		private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
		{
			//On search result box
			if (SearchListBox.IsKeyboardFocusWithin)
			{
				switch (e.Key)
				{
					//Backspace for text
					case Key.Back:
						if (SearchTextBox.CaretIndex > 0)
						{
							int originalCaretIndex = SearchTextBox.CaretIndex;
							SearchTextBox.Text = SearchTextBox.Text.Remove(SearchTextBox.CaretIndex - 1, 1);
							SearchTextBox.CaretIndex = originalCaretIndex - 1;
						}
						e.Handled = true;
						break;
					//Delete for text
					case Key.Delete:
						if (SearchTextBox.CaretIndex < SearchTextBox.Text.Length)
						{
							int originalCaretIndex = SearchTextBox.CaretIndex;
							SearchTextBox.Text = SearchTextBox.Text.Remove(SearchTextBox.CaretIndex, 1);
							SearchTextBox.CaretIndex = originalCaretIndex;
						}
						e.Handled = true;
						break;
					//CTRL+Right moves to queue without moving tag
					case Key.Right when Keyboard.Modifiers.HasFlag(ModifierKeys.Control):
						//Move focus, saving our current index
						MoveFocusToListBoxWithDefault(QueueListBox, listBoxToRemember: SearchListBox, FocusSaveMode.Item);
						e.Handled = true;
						break;
					//Right key moves the tag from search box to queue, and focuses. Saves our current index
					case Key.Right:
						MoveSelectionBetweenListBoxes(SearchListBox, QueueListBox, FocusSaveMode.Index);
						e.Handled = true;
						break;
				}
			}
			//On queue list box
			else if (QueueListBox.IsKeyboardFocusWithin)
			{
				switch (e.Key)
				{
					//CTRL+Left returns to search results without moving tag
					case Key.Left when Keyboard.Modifiers.HasFlag(ModifierKeys.Control):
						//Move focus, saving our current item
						MoveFocusToListBoxWithDefault(SearchListBox, listBoxToRemember: QueueListBox, FocusSaveMode.Item);
						e.Handled = true;
						break;
					//Left removes the tag and returns to search results
					case Key.Left:
						//Save the state
						SaveListBoxState(QueueListBox, FocusSaveMode.Index);
						//Remove the tag
						QueueTags.Remove((TagDisplayChildingItem)QueueListBox.SelectedItem);
						//Move focus
						MoveFocusToListBoxWithDefault(SearchListBox);
						e.Handled = true;
						break;
					//CTRL+Right or CTRL+Down moves to child box
					case Key.Right or Key.Down when Keyboard.Modifiers.HasFlag(ModifierKeys.Control):
						MoveFocusToListBoxWithDefault(ChildListBox, listBoxToRemember: QueueListBox, FocusSaveMode.Item);
						e.Handled = true;
						break;
				}
			}
			//On child list box
			else if (ChildListBox.IsKeyboardFocusWithin)
			{
				switch (e.Key)
				{
					//CTRL+Up or CTRL+Right moves focus to queue without moving tag
					case Key.Up or Key.Right when Keyboard.Modifiers.HasFlag(ModifierKeys.Control):
						//Move focus to queue, saving our current index
						MoveFocusToListBoxWithDefault(QueueListBox, listBoxToRemember: ChildListBox, FocusSaveMode.Index);
						e.Handled = true;
						break;
					//Right moves the tag to queue without moving the focus
					case Key.Right:
						_queueTags.Add((TagDisplayChildingItem)ChildListBox.SelectedItem);
						RepopulateChildListBox();
						e.Handled = true;
						break;
					//CTRL+Left returns to search results without any side effect
					case Key.Left when Keyboard.Modifiers.HasFlag(ModifierKeys.Control):
						//Move focus, saving our current item
						MoveFocusToListBoxWithDefault(SearchListBox, listBoxToRemember: ChildListBox, FocusSaveMode.Item);
						e.Handled = true;
						break;
				}
			}
		}

		private void SearchListBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
		{
			//Search results text will go into search field
			int savedCaretIndex = SearchTextBox.CaretIndex;
			SearchTextBox.Text = SearchTextBox.Text.Insert(SearchTextBox.CaretIndex, e.Text);
			SearchTextBox.CaretIndex = savedCaretIndex + e.Text.Length;
			e.Handled = true; 
		}

		private AdvancedTaggerHelpWindow? _helpWindow;
		private void HelpButton_OnClick(object sender, RoutedEventArgs e)
		{
			if (_helpWindow == null)
			{
				_helpWindow = new AdvancedTaggerHelpWindow()
				{
					Owner = this
				};

				_helpWindow.Closed += (_, _) => _helpWindow = null;

				_helpWindow.Show();
			}
			else
			{
				if (_helpWindow.IsVisible)
					_helpWindow.Hide();
				else
					_helpWindow.Show();
			}
		}
		
		[GeneratedRegex("[@$\"\\s>!]")]
		private static partial Regex ForbiddenSearchChars();
		
		private void SearchBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
		{
			e.Handled = ForbiddenSearchChars().IsMatch(e.Text);
		}

		private void SearchBox_PreviewKeyDown(object sender, KeyEventArgs e)
		{
			var textBox = sender as TextBox;
			if (textBox == null) return;

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

		private void Window_Closing(object? sender, CancelEventArgs e)
		{
			if (DialogResult != true)
			{
				MessageBoxResult result = MessageBox.Show(
					"Are you sure you want to close the tagging window without saving?", 
					"Confirm Exit", 
					MessageBoxButton.YesNo, 
					MessageBoxImage.Question);

				if (result == MessageBoxResult.No)
				{
					e.Cancel = true;
				}
			}
		}

		private enum FocusSaveMode
		{
			Index,
			Item,
			None
		}

		public record SelectionMemory(int? Index = null, TagDisplayChildingItem? Item = null);
		
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