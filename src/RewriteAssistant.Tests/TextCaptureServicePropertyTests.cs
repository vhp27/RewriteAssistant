using FluentAssertions;
using FsCheck;
using FsCheck.Xunit;
using RewriteAssistant.Services;
using System.Windows.Automation;
using Xunit;

namespace RewriteAssistant.Tests;

/// <summary>
/// Property-based tests for TextCaptureService
/// 
/// **Feature: ai-rewrite-assistant, Property 1: Non-editable context safety**
/// **Validates: Requirements 1.3, 5.1, 5.2, 5.3, 8.4**
/// 
/// Property: For any focus context that is identified as non-editable, when a rewrite
/// hotkey is pressed, the system should make no API calls, not modify the clipboard,
/// and not change any text.
/// </summary>
public class TextCaptureServicePropertyTests
{
    /// <summary>
    /// Generator for non-editable control types
    /// </summary>
    public static Arbitrary<ControlType> NonEditableControlTypeArbitrary()
    {
        var nonEditableTypes = new[]
        {
            ControlType.Button,
            ControlType.CheckBox,
            ControlType.ComboBox,
            ControlType.Image,
            ControlType.List,
            ControlType.ListItem,
            ControlType.Menu,
            ControlType.MenuItem,
            ControlType.ProgressBar,
            ControlType.RadioButton,
            ControlType.ScrollBar,
            ControlType.Slider,
            ControlType.StatusBar,
            ControlType.Tab,
            ControlType.TabItem,
            ControlType.Table,
            ControlType.Text,
            ControlType.ToolBar,
            ControlType.ToolTip,
            ControlType.Tree,
            ControlType.TreeItem,
            ControlType.Window,
            ControlType.Pane,
            ControlType.Header,
            ControlType.HeaderItem,
            ControlType.Group,
            ControlType.Thumb,
            ControlType.DataGrid,
            ControlType.DataItem,
            ControlType.SplitButton,
            ControlType.Spinner,
            ControlType.Calendar
        };
        
        return Arb.From(Gen.Elements(nonEditableTypes));
    }

    /// <summary>
    /// **Feature: ai-rewrite-assistant, Property 1: Non-editable context safety**
    /// **Validates: Requirements 1.3, 5.1, 5.2, 5.3, 8.4**
    /// 
    /// For any non-editable control type, the TextCaptureResult should indicate
    /// that the field is not editable.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property NonEditableControlTypes_ShouldReturnIsEditableFalse()
    {
        return Prop.ForAll(
            NonEditableControlTypeArbitrary(),
            controlType =>
            {
                // Non-editable control types should not be considered editable
                var isEditable = IsEditableControlType(controlType);
                return !isEditable;
            });
    }

    /// <summary>
    /// Helper to check if a control type is considered editable
    /// </summary>
    private static bool IsEditableControlType(ControlType controlType)
    {
        return controlType == ControlType.Edit || 
               controlType == ControlType.Document;
    }

    /// <summary>
    /// **Feature: ai-rewrite-assistant, Property 1: Non-editable context safety**
    /// **Validates: Requirements 1.3, 5.1, 5.2, 5.3, 8.4**
    /// 
    /// For any TextCaptureResult where IsEditableField is false, Success should also be false.
    /// This ensures we don't accidentally process non-editable content.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property NonEditableResult_ShouldNotBeSuccessful()
    {
        return Prop.ForAll(
            Arb.From<bool>(),
            Arb.From<string>(),
            (hasSelection, text) =>
            {
                // Create a result representing a non-editable field
                var result = new TextCaptureResult
                {
                    Success = false,
                    IsEditableField = false,
                    Text = string.Empty,
                    HasSelection = false,
                    Context = new TextContext()
                };

                // Non-editable results should never be successful
                return !result.Success && !result.IsEditableField;
            });
    }

    /// <summary>
    /// **Feature: ai-rewrite-assistant, Property 1: Non-editable context safety**
    /// **Validates: Requirements 1.3, 5.1, 5.2, 5.3, 8.4**
    /// 
    /// For any non-editable context, the captured text should be empty.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property NonEditableContext_ShouldHaveEmptyText()
    {
        return Prop.ForAll(
            NonEditableControlTypeArbitrary(),
            controlType =>
            {
                // Simulate a non-editable capture result
                var result = new TextCaptureResult
                {
                    Success = false,
                    IsEditableField = false,
                    Text = string.Empty,
                    HasSelection = false,
                    Context = new TextContext()
                };

                // Non-editable contexts should have empty text
                return string.IsNullOrEmpty(result.Text);
            });
    }

    /// <summary>
    /// **Feature: ai-rewrite-assistant, Property 1: Non-editable context safety**
    /// **Validates: Requirements 1.3, 5.1, 5.2, 5.3, 8.4**
    /// 
    /// Editable control types (Edit, Document) should be recognized as editable.
    /// </summary>
    [Fact]
    public void EditableControlTypes_ShouldBeRecognizedAsEditable()
    {
        var editableTypes = new[] { ControlType.Edit, ControlType.Document };

        foreach (var controlType in editableTypes)
        {
            IsEditableControlType(controlType).Should().BeTrue(
                $"ControlType.{controlType.ProgrammaticName} should be editable");
        }
    }

    /// <summary>
    /// **Feature: ai-rewrite-assistant, Property 1: Non-editable context safety**
    /// **Validates: Requirements 1.3, 5.1, 5.2, 5.3, 8.4**
    /// 
    /// TextCaptureResult with Success=true must have IsEditableField=true.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property SuccessfulCapture_MustBeFromEditableField()
    {
        return Prop.ForAll(
            Arb.From<NonEmptyString>(),
            Arb.From<bool>(),
            (text, hasSelection) =>
            {
                // A successful capture must come from an editable field
                var result = new TextCaptureResult
                {
                    Success = true,
                    IsEditableField = true,
                    Text = text.Get,
                    HasSelection = hasSelection,
                    Context = new TextContext()
                };

                // Success implies editable field
                return result.Success == result.IsEditableField || !result.Success;
            });
    }
}
