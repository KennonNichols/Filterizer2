using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Filterizer2.Windows
{
    public partial class EditTagWindow
    {
        private List<string> Aliases { get; } = new List<string>();

        private readonly TagItem? _editingTag;
        
        public EditTagWindow(TagItem? tagItem = null)
        {
            InitializeComponent();

            _editingTag = tagItem;
            
            // Populate the ComboBox with TagType enum values
            TagTypeComboBox.ItemsSource = Enum.GetValues(typeof(TagCategory)).Cast<TagCategory>();
            TagTypeComboBox.SelectedIndex = 0; // Select the first item by default

            // Set the initial colors and description based on the default selection
            UpdateUiForSelectedTagType((TagCategory)TagTypeComboBox.SelectedItem);

            if (tagItem != null) SetData(tagItem);
        }

        private void SetData(TagItem tagItem)
        {
            //Category
            TagTypeComboBox.SelectedItem = tagItem.Category;
            UpdateUiForSelectedTagType(tagItem.Category);

            //Name and description
            TagNameTextBox.Text = tagItem.Name;
            TagDescriptionTextBox.Text = tagItem.Description;

            //Aliases
            foreach (string tagItemAlias in tagItem.Aliases)
            {
                AliasesListBox.Items.Add(tagItemAlias);
                Aliases.Add(tagItemAlias);
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

        private void UpdateUiForSelectedTagType(TagCategory tagType)
        {
            // Get the color, title, and description for the selected TagType
            var color = tagType.GetColor();
            var description = tagType.GetDescription();

            // Update the border colors
            MainBorder.BorderBrush = new SolidColorBrush(color);
            TagNameTextBox.BorderBrush = new SolidColorBrush(color);
            TagTypeComboBox.BorderBrush = new SolidColorBrush(color);
            TagDescriptionTextBox.BorderBrush = new SolidColorBrush(color);

            // Update the description text block
            TagDescriptionTextBlock.Text = description;
            TagDescriptionTextBlock.Foreground = new SolidColorBrush(color);
        }

        private void CreateTagButton_Click(object sender, RoutedEventArgs e)
        {
            string tagName = TagNameTextBox.Text.Trim();
            TagCategory selectedTagType = (TagCategory)TagTypeComboBox.SelectedItem;
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
                _editingTag.Aliases = Aliases;
                
                
                TagRepository.UpdateTag(_editingTag);
            }
            else
            {
                var newTag = new TagItem
                {
                    Name = tagName,
                    Category = selectedTagType,
                    Description = tagDescription
                };

                TagRepository.AddTag(newTag);
            }

            Close();
        }
        
        
        private void AddAlias_Click(object sender, RoutedEventArgs e)
        {
            var alias = Microsoft.VisualBasic.Interaction.InputBox("Enter a new alias:", "Add Alias");
            if (string.IsNullOrWhiteSpace(alias) || Aliases.Contains(alias)) return;
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
    }
}