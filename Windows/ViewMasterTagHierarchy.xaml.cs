using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;

namespace Filterizer2.Windows
{
	public partial class ViewMasterTagHierarchy
	{
		public ViewMasterTagHierarchy(TagItem? tagToView = null)
		{
			InitializeComponent();
			LoadTags();
			if (tagToView != null)
			{
				Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background, new Action(() =>
				{
					TreeViewItem firstContainer = (TreeViewItem)TagsHierarchy.ItemContainerGenerator.ContainerFromItem(TagsHierarchy.Items[0]);

					if (firstContainer != null)
					{
						// Expand the item
						firstContainer.IsExpanded = true;
						SelectTag(tagToView);
					}
				}));
			}
		}

		private void SelectTag(TagItem tagItem)
		{
			TreeViewItem? tvi = GetTreeViewItem(TagsHierarchy, tagItem.Id);
			if (tvi == null) return;
			tvi.IsSelected = true;
			tvi.BringIntoView();
		}
		
		private TreeViewItem? GetTreeViewItem(ItemsControl container, int idToMatch)
		{
			if (container.DataContext is HierarchyViewTag hvt && hvt.Tag.Id == idToMatch && hvt.IsPrimary) return container as TreeViewItem;

			
			container.UpdateLayout();
    
			
			foreach (var item in container.Items)
			{
				if (container.ItemContainerGenerator.ContainerFromItem(item) is not TreeViewItem tvi) continue;
				bool wasExpanded = tvi.IsExpanded;
				if (!wasExpanded)
				{
					tvi.IsExpanded = true;
					tvi.UpdateLayout(); //Force sub-items to generate
				}
					
				TreeViewItem? result = GetTreeViewItem(tvi, idToMatch);
				if (result != null) return result;

				if (!wasExpanded)
				{
					tvi.IsExpanded = false;
				}
			}
			return null;
		}

		private void GoToMenuItem_OnClick(object sender, RoutedEventArgs e)
		{
			if (sender is not MenuItem { CommandParameter: HierarchyViewTag tagView}) return;
			SelectTag(tagView.Tag);
		}

		private void LoadTags()
		{
			Dictionary<int, TagItem> allTags = TagRepository.GetTags().ToDictionary(t => t.Id);
			
			List<HierarchyViewPiece> currentUsedHierarchyLevel = new List<HierarchyViewPiece>();
			HierarchyViewPiece masterPiece = new HierarchyViewMasterItem();
			
			foreach (HierarchyViewPiece categoryViewPiece in (from object allValue in Tags.GetAllTagCategories() select new HierarchyViewCategory((TagCategory)allValue)).Cast<HierarchyViewPiece>())
			{
				masterPiece.Children.Add(categoryViewPiece);
				currentUsedHierarchyLevel.Add(categoryViewPiece);
			}
			
			

			List<HierarchyViewPiece> lastUsedHierarchyLevel = currentUsedHierarchyLevel.ToList();
			currentUsedHierarchyLevel.Clear();

			//Add all tags with no parent, or children of a category that does not subscribe to parent/child hierarchy
			foreach ((int id, TagItem tagItem) in allTags)
			{
				if (tagItem.MayBeTrueChild(allTags))
				{
					continue;
				}
				HierarchyViewPiece parentPiece = lastUsedHierarchyLevel.Find(item =>
					item is HierarchyViewCategory catItem && catItem.Category == tagItem.Category) ?? throw new InvalidOperationException("Tag has nonexistent category.");

				HierarchyViewTag tag = new HierarchyViewTag(tagItem, true);
				
				currentUsedHierarchyLevel.Add(tag);
				parentPiece.Children.Add(tag);
			}
			
			lastUsedHierarchyLevel = currentUsedHierarchyLevel.ToList();
			currentUsedHierarchyLevel.Clear();

			
			
			while (lastUsedHierarchyLevel.Count > 0)
			{
				foreach (HierarchyViewPiece possibleParentPiece in lastUsedHierarchyLevel)
				{
					if (possibleParentPiece is not HierarchyViewTag hierarchyTag) continue;
					int parentTagId = hierarchyTag.Tag.Id;
					//For each child tag
					foreach (var (_, tag) in allTags)
					{
						if (tag.ImmediateParentIDs.Contains(parentTagId) && tag.IsChildable)
						{
							var hierarchyChildTag = new HierarchyViewTag(tag, tag.IsTrueChildOf(hierarchyTag.Tag));
							currentUsedHierarchyLevel.Add(hierarchyChildTag);
							possibleParentPiece.Children.Add(hierarchyChildTag);
						}
					}
				}
				lastUsedHierarchyLevel = currentUsedHierarchyLevel.ToList();
				currentUsedHierarchyLevel.Clear();
			}

			List<HierarchyViewPiece> databasePiece = new List<HierarchyViewPiece>();
			databasePiece.Add(masterPiece);
			TagsHierarchy.ItemsSource = databasePiece;
		}

		private void TagsListBox_OnSelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
		{
			HierarchyViewPiece viewPiece = (HierarchyViewPiece)TagsHierarchy.SelectedItem;
			TagDetailsBorder.BorderBrush = viewPiece.GetColor;
			TagTitleTextBlock.Text = viewPiece.GetTitle;
			TagDescriptionTextBlock.Text = viewPiece.GetDescription;
			if (viewPiece is HierarchyViewTag tag)
			{
				TagParentsTextBlock.Text = tag.Tag.ImmediateParentIDs.Count > 0
					? "Implies: " + string.Join(", ", tag.Tag.ImmediateParentTags) 
					: "Does not imply any other tags.";
			}
		}

		private abstract record HierarchyViewPiece()
		{
			public readonly List<HierarchyViewPiece> Children = new List<HierarchyViewPiece>();

			public IEnumerable<HierarchyViewPiece> GetChildren => Children;
			public abstract string GetDescription { get; }
			public abstract string GetTitle { get; }
			public abstract Brush GetColor { get; }
			public abstract bool IsPrimary { get;  }
			public bool IsNotPrimary => !IsPrimary;
		}

		private record HierarchyViewMasterItem : HierarchyViewPiece
		{
			public override string GetDescription =>
				"This master view shows all tags as a hierarchy. Some tags may be repeated for redundancy. Smaller tags are duplicates and are not under the primary parent. You may right-click on these to navigate to the primary one.";
			public override string GetTitle => "Database";
			public override Brush GetColor => Brushes.Black;
			public override bool IsPrimary => true;
		}

		private record HierarchyViewTag(TagItem Tag, bool IsChildOfPrimaryParent): HierarchyViewPiece
		{
			public override string GetDescription => Tag.Description;
			public override string GetTitle => Tag.Name;
			public override Brush GetColor => Tag.Category.Brush;
			public override bool IsPrimary => IsChildOfPrimaryParent;
		}

		private record HierarchyViewCategory(TagCategory Category): HierarchyViewPiece
		{
			public override string GetDescription => Category.Description;
			public override string GetTitle => Category.Title;
			public override Brush GetColor => Category.Brush;
			public override bool IsPrimary => true;
		} 
	}
}