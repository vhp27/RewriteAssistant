using System.Windows.Automation;
using System.Windows.Automation.Text;

namespace RewriteAssistant.Services;

/// <summary>
/// Interface for text replace service
/// </summary>
public interface ITextReplaceService
{
    Task<bool> ReplaceTextAsync(string newText, TextContext context);
}

/// <summary>
/// Interface for async text replacement operations
/// </summary>
public interface IAsyncTextReplace
{
    Task<bool> TryReplaceWithClipboardAsync(string newText, TextContext context);
}

/// <summary>
/// Service for replacing text in focused editable fields using Windows UI Automation
/// Requirements: 1.1, 1.2, 1.4, 7.1, 7.3
/// </summary>
public class TextReplaceService : ITextReplaceService
{
    /// <summary>
    /// Replaces text in the focused editable element
    /// </summary>
    /// <param name="newText">The new text to insert</param>
    /// <param name="context">The context from the original text capture</param>
    /// <returns>True if replacement was successful</returns>
    public async Task<bool> ReplaceTextAsync(string newText, TextContext context)
    {
        return await ReplaceTextInternalAsync(newText, context);
    }

    private async Task<bool> ReplaceTextInternalAsync(string newText, TextContext context)
    {
        try
        {
            Logger.Debug($"ReplaceText called: newText.Length={newText.Length}, UsedClipboardCapture={context.UsedClipboardCapture}");
            
            // If we used clipboard capture, use clipboard-based replacement
            if (context.UsedClipboardCapture)
            {
                Logger.Debug("Using clipboard-based replacement for web editor");
                return await TryReplaceWithClipboardAsync(newText, context);
            }

            // Get the element from context or get currently focused element
            var element = context.Element ?? AutomationElement.FocusedElement;
            if (element == null)
            {
                Logger.Warn("No element found for replacement");
                return false;
            }

            // Try to replace using ValuePattern first (most common for simple inputs)
            if (TryReplaceWithValuePattern(element, newText, context))
            {
                Logger.Info("Replaced text via ValuePattern");
                return true;
            }

            // Try to replace using TextPattern (for rich text controls)
            if (TryReplaceWithTextPattern(element, newText, context))
            {
                Logger.Info("Replaced text via TextPattern");
                return true;
            }

            // Fall back to keyboard simulation
            if (await TryReplaceWithKeyboardSimulationAsync(element, newText, context))
            {
                Logger.Info("Replaced text via keyboard simulation");
                return true;
            }

            // Last resort: try clipboard-based replacement even if we didn't use clipboard capture
            // This helps with browser inputs that don't support UI Automation patterns
            Logger.Debug("Trying clipboard-based replacement as last resort");
            if (await TryReplaceWithClipboardAsync(newText, context))
            {
                Logger.Info("Replaced text via clipboard (fallback)");
                return true;
            }

            Logger.Warn("All replacement methods failed");
            return false;
        }
        catch (Exception ex)
        {
            Logger.Error("Exception during text replacement", ex);
            return false;
        }
    }

    /// <summary>
    /// Replaces text using clipboard-based paste (for web editors)
    /// </summary>
    private static async Task<bool> TryReplaceWithClipboardAsync(string newText, TextContext context)
    {
        try
        {
            // Store the foreground window handle
            var targetWindow = NativeMethods.GetForegroundWindow();
            Logger.Debug($"Replace target window: 0x{targetWindow:X}");

            // CRITICAL: Release all modifier keys first
            InputSimulator.ReleaseAllModifierKeys();
            await Task.Delay(200);

            // Ensure the target window is still in foreground
            var currentForeground = NativeMethods.GetForegroundWindow();
            if (currentForeground != targetWindow && targetWindow != IntPtr.Zero)
            {
                Logger.Debug("Restoring foreground window focus for paste");
                NativeMethods.SetForegroundWindow(targetWindow);
                await Task.Delay(100);
            }

            // Put the new text on the clipboard using STA thread
            InputSimulator.SetClipboardText(newText);
            await Task.Delay(100);

            // If we didn't use clipboard capture (fallback path), we need to select all first
            if (!context.UsedClipboardCapture)
            {
                Logger.Debug("Selecting all text before clipboard paste");
                InputSimulator.SendCtrlA();
                await Task.Delay(300);
            }
            // The text should already be selected (from capture), so just paste
            
            InputSimulator.SendCtrlV();
            await Task.Delay(300);

            // Restore original clipboard content if we saved it
            if (context.OriginalClipboardText != null)
            {
                await Task.Delay(300); // Wait for paste to complete
                try
                {
                    InputSimulator.SetClipboardText(context.OriginalClipboardText);
                }
                catch
                {
                    // Ignore clipboard restore errors
                }
            }

            Logger.Debug("Clipboard-based replacement completed");
            return true;
        }
        catch (Exception ex)
        {
            Logger.Error("Clipboard replacement failed", ex);
            return false;
        }
    }

    /// <summary>
    /// Tries to replace text using ValuePattern
    /// </summary>
    private static bool TryReplaceWithValuePattern(AutomationElement element, string newText, TextContext context)
    {
        try
        {
            if (!element.TryGetCurrentPattern(ValuePattern.Pattern, out var patternObj))
            {
                return false;
            }

            var valuePattern = (ValuePattern)patternObj;

            // If there was a selection, we need to handle partial replacement
            if (context.SelectionLength > 0)
            {
                // Get current value
                var currentValue = valuePattern.Current.Value ?? string.Empty;
                
                // Build new value with replacement
                var beforeSelection = currentValue.Substring(0, Math.Min(context.SelectionStart, currentValue.Length));
                var afterSelection = context.SelectionStart + context.SelectionLength <= currentValue.Length
                    ? currentValue.Substring(context.SelectionStart + context.SelectionLength)
                    : string.Empty;
                
                var finalText = beforeSelection + newText + afterSelection;
                valuePattern.SetValue(finalText);
            }
            else
            {
                // No selection - replace entire content
                valuePattern.SetValue(newText);
            }

            // Try to restore cursor position
            TryRestoreCursorPosition(element, context, newText.Length);

            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Tries to replace text using TextPattern (for rich text controls)
    /// </summary>
    private static bool TryReplaceWithTextPattern(AutomationElement element, string newText, TextContext context)
    {
        try
        {
            if (!element.TryGetCurrentPattern(TextPattern.Pattern, out var textPatternObj))
            {
                return false;
            }

            // TextPattern doesn't directly support setting text
            // We need to use ValuePattern in combination or fall back to keyboard simulation
            // Try ValuePattern first if available
            if (element.TryGetCurrentPattern(ValuePattern.Pattern, out var valuePatternObj))
            {
                return TryReplaceWithValuePattern(element, newText, context);
            }

            // TextPattern alone doesn't support text modification
            // Fall through to keyboard simulation
            return false;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Tries to replace text using keyboard simulation (last resort)
    /// </summary>
    private static async Task<bool> TryReplaceWithKeyboardSimulationAsync(AutomationElement element, string newText, TextContext context)
    {
        try
        {
            // Ensure element has focus
            element.SetFocus();
            
            // Small delay to ensure focus is set
            await Task.Delay(50);

            if (context.SelectionLength > 0)
            {
                // Selection exists - just type to replace
                // The selection should still be active
                SendKeys(newText);
            }
            else
            {
                // No selection - select all and replace
                // Send Ctrl+A to select all
                InputSimulator.SendCtrlA();
                await Task.Delay(50);
                
                // Type the new text (replaces selection)
                SendKeys(newText);
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Tries to restore cursor position after replacement
    /// </summary>
    private static void TryRestoreCursorPosition(AutomationElement element, TextContext context, int newTextLength)
    {
        try
        {
            // Try to set cursor at end of inserted text
            if (element.TryGetCurrentPattern(TextPattern.Pattern, out var textPatternObj))
            {
                var textPattern = (TextPattern)textPatternObj;
                var docRange = textPattern.DocumentRange;
                
                // Move to end of inserted text
                var targetPosition = context.SelectionStart + newTextLength;
                var range = docRange.Clone();
                range.MoveEndpointByUnit(TextPatternRangeEndpoint.Start, TextUnit.Character, targetPosition);
                range.MoveEndpointByUnit(TextPatternRangeEndpoint.End, TextUnit.Character, 0);
                range.Select();
            }
        }
        catch
        {
            // Ignore cursor positioning errors
        }
    }

    /// <summary>
    /// Sends text as keyboard input
    /// </summary>
    private static void SendKeys(string text)
    {
        foreach (var c in text)
        {
            SendCharacter(c);
        }
    }

    /// <summary>
    /// Sends a single character as keyboard input
    /// </summary>
    private static void SendCharacter(char c)
    {
        var inputs = new NativeMethods.INPUT[2];

        // Key down with unicode
        inputs[0] = new NativeMethods.INPUT
        {
            type = NativeMethods.INPUT_KEYBOARD,
            u = new NativeMethods.InputUnion
            {
                ki = new NativeMethods.KEYBDINPUT
                {
                    wVk = 0,
                    wScan = c,
                    dwFlags = NativeMethods.KEYEVENTF_UNICODE,
                    time = 0,
                    dwExtraInfo = IntPtr.Zero
                }
            }
        };

        // Key up with unicode
        inputs[1] = new NativeMethods.INPUT
        {
            type = NativeMethods.INPUT_KEYBOARD,
            u = new NativeMethods.InputUnion
            {
                ki = new NativeMethods.KEYBDINPUT
                {
                    wVk = 0,
                    wScan = c,
                    dwFlags = NativeMethods.KEYEVENTF_UNICODE | NativeMethods.KEYEVENTF_KEYUP,
                    time = 0,
                    dwExtraInfo = IntPtr.Zero
                }
            }
        };

        NativeMethods.SendInput((uint)inputs.Length, inputs, NativeMethods.INPUT.Size);
    }

}
