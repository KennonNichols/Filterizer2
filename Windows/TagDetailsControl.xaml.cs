using System.Windows.Controls;
using System.Windows.Media;
using Brush = System.Windows.Media.Brush;
using UserControl = System.Windows.Controls.UserControl;

namespace Filterizer2.Windows
{
	public partial class TagDetailsControl
	{

		private Brush _defaultBorderBrush;
		public Brush DefaultBorderBrush
		{
			get => _defaultBorderBrush;
			set
			{
				_defaultBorderBrush = value;
				TagDetailsBorder.BorderBrush = value;
			}
		}

		public TagDetailsControl()
		{
			InitializeComponent();
		}

		public void DisplayTag(TagItem? selectedTag, bool updateColor)
		{
			if (selectedTag == null)
			{
				TagTitleTextBlock.Text = "";
				TagDescriptionTextBlock.Text = "";
				TagAliasesTextBlock.Text = "";
				TagParentsTextBlock.Text = "";
				TagDetailsBorder.BorderBrush = DefaultBorderBrush;
				return;
			}

			TagTitleTextBlock.Text = selectedTag.Name;
			TagDescriptionTextBlock.Text = selectedTag.Description;
			TagAliasesTextBlock.Text = selectedTag.Aliases.Any() 
				? "Aliases: " + string.Join(", ", selectedTag.Aliases) 
				: "No Aliases";
			TagParentsTextBlock.Text = selectedTag.ImmediateParentTags.Any() 
				? "Implies: " + string.Join(", ", selectedTag.ImmediateParentTags) 
				: "Does not imply any other tags.";

			if (updateColor)
			{
				// Set the border color based on the TagType
				TagDetailsBorder.BorderBrush = new SolidColorBrush(selectedTag.Category.Color);
			}
		}
	}
}