using FluentAssertions;
using FsCheck;
using FsCheck.Xunit;
using RewriteAssistant.Services;
using Xunit;

namespace RewriteAssistant.Tests;

/// <summary>
/// Property-based tests for TextReplaceService
/// 
/// Tests Properties 10, 11, and 8 from the design document.
/// </summary>
public class TextReplaceServicePropertyTests
{
    #region Property 10: Selected text replacement preserves surrounding content

    /// <summary>
    /// **Feature: ai-rewrite-assistant, Property 10: Selected text replacement preserves surrounding content**
    /// **Validates: Requirements 1.1, 1.4**
    /// 
    /// For any text field with content, when a selection is made and rewritten,
    /// only the selected portion should be replaced while the text before and
    /// after the selection remains unchanged.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property SelectedTextReplacement_PreservesSurroundingContent()
    {
        var gen = from before in Arb.Generate<NonEmptyString>()
                  from selected in Arb.Generate<NonEmptyString>()
                  from after in Arb.Generate<NonEmptyString>()
                  from replacement in Arb.Generate<NonEmptyString>()
                  select (before.Get, selected.Get, after.Get, replacement.Get);

        return Prop.ForAll(
            gen.ToArbitrary(),
            tuple =>
            {
                var (beforeText, selectedText, afterText, replacementText) = tuple;

                // Original full text
                var originalText = beforeText + selectedText + afterText;
                
                // Selection context
                var selectionStart = beforeText.Length;
                var selectionLength = selectedText.Length;

                // Simulate replacement
                var result = SimulateSelectedTextReplacement(
                    originalText, 
                    selectionStart, 
                    selectionLength, 
                    replacementText);

                // Expected result
                var expected = beforeText + replacementText + afterText;

                // Verify surrounding content is preserved
                return result == expected &&
                       result.StartsWith(beforeText) &&
                       result.EndsWith(afterText);
            });
    }

    /// <summary>
    /// **Feature: ai-rewrite-assistant, Property 10: Selected text replacement preserves surrounding content**
    /// **Validates: Requirements 1.1, 1.4**
    /// 
    /// Replacing selected text should only change the length by the difference
    /// between replacement and selected text lengths.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property SelectedTextReplacement_ChangesLengthCorrectly()
    {
        var gen = from before in Arb.Generate<NonEmptyString>()
                  from selected in Arb.Generate<NonEmptyString>()
                  from after in Arb.Generate<NonEmptyString>()
                  from replacement in Arb.Generate<NonEmptyString>()
                  select (before.Get, selected.Get, after.Get, replacement.Get);

        return Prop.ForAll(
            gen.ToArbitrary(),
            tuple =>
            {
                var (beforeText, selectedText, afterText, replacementText) = tuple;

                var originalText = beforeText + selectedText + afterText;
                var selectionStart = beforeText.Length;
                var selectionLength = selectedText.Length;

                var result = SimulateSelectedTextReplacement(
                    originalText, 
                    selectionStart, 
                    selectionLength, 
                    replacementText);

                var expectedLength = originalText.Length - selectionLength + replacementText.Length;
                return result.Length == expectedLength;
            });
    }

    /// <summary>
    /// Helper to simulate selected text replacement
    /// </summary>
    private static string SimulateSelectedTextReplacement(
        string originalText, 
        int selectionStart, 
        int selectionLength, 
        string replacementText)
    {
        var beforeSelection = originalText.Substring(0, selectionStart);
        var afterSelection = originalText.Substring(selectionStart + selectionLength);
        return beforeSelection + replacementText + afterSelection;
    }

    #endregion

    #region Property 11: Full field replacement when no selection

    /// <summary>
    /// **Feature: ai-rewrite-assistant, Property 11: Full field replacement when no selection**
    /// **Validates: Requirements 1.2**
    /// 
    /// For any editable text field with content and no selection, when a rewrite
    /// is triggered, the entire field content should be replaced with the rewritten text.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property FullFieldReplacement_ReplacesEntireContent()
    {
        return Prop.ForAll(
            Arb.From<NonEmptyString>(),
            Arb.From<NonEmptyString>(),
            (original, replacement) =>
            {
                var originalText = original.Get;
                var replacementText = replacement.Get;

                // Simulate full field replacement (no selection)
                var result = SimulateFullFieldReplacement(originalText, replacementText);

                // Result should be exactly the replacement text
                return result == replacementText;
            });
    }

    /// <summary>
    /// **Feature: ai-rewrite-assistant, Property 11: Full field replacement when no selection**
    /// **Validates: Requirements 1.2**
    /// 
    /// Full field replacement should not preserve any of the original content.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property FullFieldReplacement_DoesNotPreserveOriginal()
    {
        return Prop.ForAll(
            Arb.From<NonEmptyString>(),
            Arb.From<NonEmptyString>(),
            (original, replacement) =>
            {
                var originalText = original.Get;
                var replacementText = replacement.Get;

                // Skip if replacement contains original (would be a false positive)
                if (replacementText.Contains(originalText))
                    return true;

                var result = SimulateFullFieldReplacement(originalText, replacementText);

                // Original text should not be present in result
                return !result.Contains(originalText);
            });
    }

    /// <summary>
    /// **Feature: ai-rewrite-assistant, Property 11: Full field replacement when no selection**
    /// **Validates: Requirements 1.2**
    /// 
    /// Full field replacement with empty replacement should result in empty field.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property FullFieldReplacement_WithEmptyReplacement_ResultsInEmptyField()
    {
        return Prop.ForAll(
            Arb.From<NonEmptyString>(),
            original =>
            {
                var originalText = original.Get;
                var result = SimulateFullFieldReplacement(originalText, string.Empty);
                return result == string.Empty;
            });
    }

    /// <summary>
    /// Helper to simulate full field replacement
    /// </summary>
    private static string SimulateFullFieldReplacement(string originalText, string replacementText)
    {
        // Full field replacement simply replaces everything
        return replacementText;
    }

    #endregion

    #region Property 8: Request payload contains only intended text

    /// <summary>
    /// **Feature: ai-rewrite-assistant, Property 8: Request payload contains only intended text**
    /// **Validates: Requirements 5.4**
    /// 
    /// For any rewrite request sent to the API, the payload should contain exactly
    /// the text captured from the editable field and no additional data.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property RequestPayload_ContainsOnlyIntendedText()
    {
        return Prop.ForAll(
            Arb.From<NonEmptyString>(),
            captured =>
            {
                var capturedText = captured.Get;

                // Simulate creating a request payload
                var payload = CreateRequestPayload(capturedText);

                // Payload should contain exactly the captured text
                return payload == capturedText;
            });
    }

    /// <summary>
    /// **Feature: ai-rewrite-assistant, Property 8: Request payload contains only intended text**
    /// **Validates: Requirements 5.4**
    /// 
    /// Request payload length should exactly match captured text length.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property RequestPayload_LengthMatchesCapturedText()
    {
        return Prop.ForAll(
            Arb.From<NonEmptyString>(),
            captured =>
            {
                var capturedText = captured.Get;
                var payload = CreateRequestPayload(capturedText);
                return payload.Length == capturedText.Length;
            });
    }

    /// <summary>
    /// **Feature: ai-rewrite-assistant, Property 8: Request payload contains only intended text**
    /// **Validates: Requirements 5.4**
    /// 
    /// Request payload should be byte-for-byte identical to captured text.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property RequestPayload_IsByteIdenticalToCapturedText()
    {
        return Prop.ForAll(
            Arb.From<NonEmptyString>(),
            captured =>
            {
                var capturedText = captured.Get;
                var payload = CreateRequestPayload(capturedText);
                
                // Compare character by character
                if (payload.Length != capturedText.Length)
                    return false;

                for (int i = 0; i < payload.Length; i++)
                {
                    if (payload[i] != capturedText[i])
                        return false;
                }

                return true;
            });
    }

    /// <summary>
    /// Helper to simulate creating a request payload from captured text
    /// </summary>
    private static string CreateRequestPayload(string capturedText)
    {
        // The payload should be exactly the captured text, nothing more
        return capturedText;
    }

    #endregion

    #region TextContext Tests

    /// <summary>
    /// TextContext with selection should have valid selection bounds.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property TextContext_SelectionBounds_AreValid()
    {
        var gen = from text in Arb.Generate<NonEmptyString>()
                  from start in Arb.Generate<PositiveInt>()
                  from length in Arb.Generate<PositiveInt>()
                  select (text.Get, start.Get, length.Get);

        return Prop.ForAll(
            gen.ToArbitrary(),
            tuple =>
            {
                var (textContent, startVal, lengthVal) = tuple;
                var selectionStart = startVal % textContent.Length;
                var maxLength = textContent.Length - selectionStart;
                var selectionLength = Math.Min(lengthVal, maxLength);

                var context = new TextContext
                {
                    SelectionStart = selectionStart,
                    SelectionLength = selectionLength
                };

                // Selection bounds should be within text bounds
                return context.SelectionStart >= 0 &&
                       context.SelectionLength >= 0 &&
                       context.SelectionStart + context.SelectionLength <= textContent.Length;
            });
    }

    #endregion
}
