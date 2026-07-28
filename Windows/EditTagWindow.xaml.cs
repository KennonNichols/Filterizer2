using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using MessageBox = System.Windows.MessageBox;
using TextBox = System.Windows.Controls.TextBox;

namespace Filterizer2.Windows
{
    public partial class EditTagWindow: ISelectsTags
    {
	    private List<string> Aliases { get; } = new List<string>();
        private List<int> ParentIds { get; } = new List<int>();

        [GeneratedRegex("[@$\"\\s>]")]
        private static partial Regex ForbiddenChars();
        [GeneratedRegex("[@$\"\\t\\r\\n>]")]
        private static partial Regex ForbiddenCharsDescription();
        

        private readonly TagItem? _editingTag;
        
        public EditTagWindow(TagItem? tagItem = null)
        {
            InitializeComponent();

            _editingTag = tagItem;
            
            TagTypeComboBox.ItemsSource = Tags.GetAllValues();
            TagTypeComboBox.SelectedIndex = 0; // Select the first item by default

            // Set the initial colors and description based on the default selection
            UpdateUiForSelectedTagType((TagCategory)TagTypeComboBox.SelectedItem);

            if (tagItem != null) SetData(tagItem);
        }

        private void SetData(TagItem tagItem)
        {
            //Category
            TagTypeComboBox.SelectedItem = tagItem.Category;
            // UpdateUiForSelectedTagType(tagItem.Category);
            
            //Subcategory
            TagSubtypeComboBox.SelectedItem = tagItem.SubCategory;

            //Name and description
            TagNameTextBox.Text = tagItem.Name;
            TagDescriptionTextBox.Text = tagItem.Description;

            //Aliases
            foreach (string tagItemAlias in tagItem.Aliases)
            {
                AliasesListBox.Items.Add(tagItemAlias);
                Aliases.Add(tagItemAlias);
            }
            
            //Parents
            foreach (var tagItemParent in tagItem.ImmediateParentTags)
            {
	            ParentsListBox.Items.Add(tagItemParent);
	            ParentIds.Add(tagItemParent.Id);
            }
        }


        private void TagTypeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Update the UI when a new TagType is selected
            if (TagTypeComboBox.SelectedItem is TagCategory selectedTagType)
            {
                UpdateUiForSelectedTagType(selectedTagType);
            }
        }
        
        private void TagSubtypeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
	        // Update the UI when a new TagType is selected
	        if (TagSubtypeComboBox.SelectedItem is TagSubCategory selectedTagSubtype)
	        {
		        TagSubDescriptionTextBlock.Text = selectedTagSubtype.Description;
	        }
        }

        private void UpdateUiForSelectedTagType(TagCategory tagType)
        {
            // Get the color, title, and description for the selected TagType
            var color = tagType.Color;
            var description = tagType.Description;

            // Update the border colors
            MainBorder.BorderBrush = new SolidColorBrush(color);
            TagNameTextBox.BorderBrush = new SolidColorBrush(color);
            TagTypeComboBox.BorderBrush = new SolidColorBrush(color);
            TagDescriptionTextBox.BorderBrush = new SolidColorBrush(color);

            // Update the description text block
            TagDescriptionTextBlock.Text = description;
            TagDescriptionTextBlock.Foreground = new SolidColorBrush(color);

            TagSubDescriptionTextBlock.Foreground = new SolidColorBrush(color);
            
            TagSubtypeComboBox.ItemsSource = tagType.SubcategoriesInOrder;
            TagSubtypeComboBox.SelectedIndex = tagType.SubcategoriesInOrder.Count - 1; // Select misc by default
        }

        private void CreateTagButton_Click(object sender, RoutedEventArgs e)
        {
            string tagName = TagNameTextBox.Text.Trim();
            TagCategory selectedTagType = (TagCategory)TagTypeComboBox.SelectedItem;
            TagSubCategory selectedTagSubtype = (TagSubCategory)TagSubtypeComboBox.SelectedItem;
            string tagDescription = TagDescriptionTextBox.Text.Trim();

            if (string.IsNullOrEmpty(tagName))
            {
                MessageBox.Show("Tag Name cannot be empty.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (_editingTag != null)
            {
                _editingTag.Name = tagName;
                _editingTag.Description = tagDescription;
                _editingTag.Category = selectedTagType;
                _editingTag.SubCategory = selectedTagSubtype;
                _editingTag.Aliases = Aliases;
                _editingTag.ImmediateParentIDs = ParentIds;
                
                TagRepository.UpdateTag(_editingTag);
            }
            else
            {
                var newTag = new TagItem
                {
                    Name = tagName,
                    Category = selectedTagType,
                    SubCategory = selectedTagSubtype,
                    Description = tagDescription,
                    Aliases = Aliases,
                    ImmediateParentIDs = ParentIds
                };

                TagRepository.AddTag(newTag);
            }

            Close();
        }
        
        
        private void AddAlias_Click(object sender, RoutedEventArgs e)
        {
            var alias = Microsoft.VisualBasic.Interaction.InputBox("Enter a new alias:", "Add Alias");
            if (string.IsNullOrWhiteSpace(alias) || Aliases.Contains(alias)) return;
            Regex regex = GetRegexForTextSource(sender);
            
            if (regex.IsMatch(" "))
            {
	            alias = alias.Replace(" ", "_");
            }
            
            if (regex.IsMatch(alias))
            {
	            char matched = regex.Match(alias).Value[0];
	            MessageBox.Show($"Alias contains forbidden character: {matched}", "Invalid Alias",
		            MessageBoxButton.OK, MessageBoxImage.Error);
	            alias = regex.Replace(alias, string.Empty);
            }
            
            Aliases.Add(alias);
            AliasesListBox.Items.Add(alias);
        }

        private void RemoveAlias_Click(object sender, RoutedEventArgs e)
        {
            if (AliasesListBox.SelectedItem is string alias)
            {
                Aliases.Remove(alias);
                AliasesListBox.Items.Remove(alias);
            }
        }

        public void OnTagSelectComplete(List<TagItem> parents)
        {
	        ParentIds.Clear();
	        ParentsListBox.Items.Clear();
	        foreach (TagItem parent in parents)
	        {
		        ParentIds.Add(parent.Id);
	        }
	        foreach (var tagItem in parents)
	        {
		        ParentsListBox.Items.Add(tagItem);
	        }
        }

        public List<TagItem> GetParents => _editingTag?.ImmediateParentTags.ToList() ??
                                           ParentsListBox.Items.SourceCollection.Cast<TagItem>().ToList();

        private void EditParent_Click(object sender, RoutedEventArgs e)
        {
	        SelectTagsWindow selectTagsWindow = new SelectTagsWindow(this, _editingTag);
	        selectTagsWindow.Show();
        }

        private void RestrictedTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
	        e.Handled = GetRegexForTextSource(sender).IsMatch(e.Text);
        }

        private void RestrictedTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
	        var textBox = sender as TextBox;
	        if (textBox == null) return;

	        if (e.Key == Key.Space)
	        {
		        var regex = GetRegexForTextSource(sender);
		        if (regex.IsMatch(" "))
		        {
			        e.Handled = true;
			        int caretIndex = textBox.CaretIndex;
			        textBox.Text = textBox.Text.Remove(textBox.SelectionStart, textBox.SelectionLength)
				        .Insert(caretIndex, "_");
			        textBox.CaretIndex = caretIndex + 1;
		        }
	        }
        }

        private void RestrictedTextBox_Pasting(object sender, DataObjectPastingEventArgs e)
        {
	        if (sender is not TextBox textBox) return;

	        if (e.DataObject.GetDataPresent(typeof(string)))
	        {
		        Regex regex = GetRegexForTextSource(sender);
		        
		        string pastedText = (string)e.DataObject.GetData(typeof(string));

		        //Space is special cased to be replaced with underscores.
		        if (regex.IsMatch(" "))
		        {
			        pastedText = pastedText.Replace(" ", "_");
		        }
		        

		        string cleanedText = regex.Replace(pastedText, string.Empty);

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

        private Regex GetRegexForTextSource(object source)
        {
	        return source is TextBox { Name: "TagDescriptionTextBox" } ? ForbiddenCharsDescription() : ForbiddenChars();
        }
    }

    public interface ISelectsTags
    {
	    public void OnTagSelectComplete(List<TagItem> parents);

	    public List<TagItem> GetParents { get; }
    }
}