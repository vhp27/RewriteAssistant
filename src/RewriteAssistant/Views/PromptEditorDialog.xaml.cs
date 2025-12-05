using System.Windows;
using System.Windows.Controls;
using RewriteAssistant.Models;

namespace RewriteAssistant.Views;

/// <summary>
/// Dialog for creating and editing custom prompts.
/// Requirements: 2.2, 2.3, 6.3
/// </summary>
public partial class PromptEditorDialog : Window
{
    private CustomPrompt? _existingPrompt;
    
    /// <summary>
    /// Gets the resulting prompt after the dialog is closed with Save.
    /// </summary>
    public CustomPrompt? ResultPrompt { get; private set; }
    
    /// <summary>
    /// Creates a new PromptEditorDialog for creating a new prompt.
    /// </summary>
    public PromptEditorDialog()
    {
        InitializeComponent();
        DialogTitle.Text = "New Prompt";
        Title = "New Prompt";
    }
    
    /// <summary>
    /// Creates a new PromptEditorDialog for editing an existing prompt.
    /// </summary>
    /// <param name="prompt">The prompt to edit</param>
    public PromptEditorDialog(CustomPrompt prompt) : this()
    {
        _existingPrompt = prompt;
        DialogTitle.Text = "Edit Prompt";
        Title = "Edit Prompt";
        
        // Populate fields with existing values
        NameTextBox.Text = prompt.Name;
        PromptTextBox.Text = prompt.PromptText;
        
        // Disable name editing for built-in prompts
        if (prompt.IsBuiltIn)
        {
            NameTextBox.IsEnabled = false;
            NameTextBox.ToolTip = "Built-in prompt names cannot be changed";
        }
        
        ValidateForm();
    }

    private void NameTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        ValidateForm();
    }
    
    private void PromptTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        ValidateForm();
    }
    
    private void ValidateForm()
    {
        var nameValid = !string.IsNullOrWhiteSpace(NameTextBox.Text);
        var promptValid = !string.IsNullOrWhiteSpace(PromptTextBox.Text);
        
        // Show/hide error messages
        NameErrorText.Visibility = nameValid ? Visibility.Collapsed : Visibility.Visible;
        PromptErrorText.Visibility = promptValid ? Visibility.Collapsed : Visibility.Visible;
        
        // Enable save button only when both fields are valid
        SaveButton.IsEnabled = nameValid && promptValid;
    }
    
    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        // Final validation
        if (string.IsNullOrWhiteSpace(NameTextBox.Text) || string.IsNullOrWhiteSpace(PromptTextBox.Text))
        {
            ValidateForm();
            return;
        }
        
        var now = DateTime.UtcNow;
        
        ResultPrompt = new CustomPrompt
        {
            Id = _existingPrompt?.Id ?? Guid.NewGuid().ToString(),
            Name = NameTextBox.Text.Trim(),
            PromptText = PromptTextBox.Text.Trim(),
            IsBuiltIn = _existingPrompt?.IsBuiltIn ?? false,
            CreatedAt = _existingPrompt?.CreatedAt ?? now,
            ModifiedAt = now
        };
        
        DialogResult = true;
        Close();
    }
    
    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
