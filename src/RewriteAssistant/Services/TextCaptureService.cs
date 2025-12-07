using System.Windows;
using System.Windows.Automation;

namespace RewriteAssistant.Services;

/// <summary>
/// Context information about the captured text
/// </summary>
public class TextContext
{
    public IntPtr WindowHandle { get; set; }
    public string ApplicationName { get; set; } = string.Empty;
    public int SelectionStart { get; set; }
    public int SelectionLength { get; set; }
    public AutomationElement? Element { get; set; }
    public bool UsedClipboardCapture { get; set; }
    public string? OriginalClipboardText { get; set; }
}

/// <summary>
/// Result of a text capture operation
/// </summary>
public class TextCaptureResult
{
    public bool Success { get; set; }
    public bool IsEditableField { get; set; }
    public string Text { get; set; } = string.Empty;
    public bool HasSelection { get; set; }
    public TextContext Context { get; set; } = new();
}

/// <summary>
/// Interface for text capture service
/// </summary>
public interface ITextCaptureService
{
    Task<TextCaptureResult> CaptureTextAsync();
}

/// <summary>
/// Interface for async clipboard capture operations
/// </summary>
public interface IAsyncClipboardCapture
{
    Task<bool> TryGetTextFromClipboardAsync(TextCaptureResult result);
}

/// <summary>
/// Service for capturing text from focused editable fields using Windows UI Automation.
/// Uses a universal behavior-based approach that works with any application.
/// Requirements: 1.1, 1.2, 5.1, 7.1, 7.2
/// </summary>
public class TextCaptureService : ITextCaptureService
{
    /// <summary>
    /// Captures text from the currently focused editable element
    /// </summary>
    public async Task<TextCaptureResult> CaptureTextAsync()
    {
        return await CaptureTextInternalAsync();
    }

    private async Task<TextCaptureResult> CaptureTextInternalAsync()
    {
        var result = new TextCaptureResult();

        try
        {
            Logger.Debug("Attempting to capture text from focused element...");
            
            // Get the focused element
            var focusedElement = AutomationElement.FocusedElement;
            if (focusedElement == null)
            {
                Logger.Warn("No focused element found");
                return result;
            }

            var controlType = focusedElement.Current.ControlType;
            var className = focusedElement.Current.ClassName ?? string.Empty;
            Logger.Debug($"Focused element: Type={controlType.ProgrammaticName}, Class='{className}'");

            // Build context information
            result.Context = BuildTextContext(focusedElement);
            Logger.Debug($"Context: App={result.Context.ApplicationName}, Handle=0x{result.Context.WindowHandle:X}");

            // Check if element has keyboard focus
            var hasKeyboardFocus = false;
            try
            {
                hasKeyboardFocus = (bool)focusedElement.GetCurrentPropertyValue(AutomationElement.HasKeyboardFocusProperty);
            }
            catch { }

            // Check if the element is editable using standard UI Automation patterns
            var isStandardEditable = IsEditableElement(focusedElement);
            
            // Detect Chromium-based apps by window class (they report HasKeyboardFocus=false incorrectly)
            // This is framework detection, not app-specific - works for ALL Chromium/Electron apps
            var isChromiumBased = IsChromiumWindow(className);
            
            Logger.Debug($"IsStandardEditable={isStandardEditable}, HasKeyboardFocus={hasKeyboardFocus}, IsChromiumBased={isChromiumBased}");

            // Strategy 1: Try standard UI Automation patterns first (works for native apps)
            if (isStandardEditable)
            {
                if (TryGetTextFromTextPattern(focusedElement, result))
                {
                    result.Success = true;
                    result.IsEditableField = true;
                    Logger.Info($"Captured text via TextPattern: {result.Text.Length} chars, HasSelection={result.HasSelection}");
                    return result;
                }

                if (TryGetTextFromValuePattern(focusedElement, result))
                {
                    result.Success = true;
                    result.IsEditableField = true;
                    Logger.Info($"Captured text via ValuePattern: {result.Text.Length} chars");
                    return result;
                }
            }

            // Strategy 2: Try clipboard-based capture if:
            // - Element has keyboard focus, OR
            // - It's a Chromium-based app (they incorrectly report HasKeyboardFocus=false)
            if (hasKeyboardFocus || isChromiumBased)
            {
                Logger.Debug("Trying clipboard-based capture...");
                if (await TryGetTextFromClipboardAsync(result))
                {
                    result.Success = true;
                    result.IsEditableField = true;
                    result.Context.UsedClipboardCapture = true;
                    Logger.Info($"Captured text via Clipboard: {result.Text.Length} chars, HasSelection={result.HasSelection}");
                    return result;
                }
            }

            // No text captured - element is not editable or doesn't contain text
            if (!isStandardEditable && !hasKeyboardFocus && !isChromiumBased)
            {
                Logger.Info($"Element is not editable: {controlType.ProgrammaticName}");
                result.IsEditableField = false;
            }
            else
            {
                Logger.Warn("Element appears editable but couldn't extract text");
                result.IsEditableField = true;
            }
            
            result.Success = false;
        }
        catch (Exception ex)
        {
            Logger.Error("Exception during text capture", ex);
            result.Success = false;
        }

        return result;
    }

    /// <summary>
    /// Detects if the window is Chromium-based by its window class.
    /// Chromium apps (Chrome, Edge, Electron, VS Code, Discord, etc.) all use these class names.
    /// This is framework detection, not app-specific hardcoding.
    /// </summary>
    private static bool IsChromiumWindow(string className)
    {
        if (string.IsNullOrEmpty(className))
            return false;
            
        // Chromium-based windows use these class name patterns
        // This covers ALL Chromium/Electron apps universally
        return className.StartsWith("Chrome_WidgetWin", StringComparison.OrdinalIgnoreCase) ||
               className.Contains("Chrome_RenderWidgetHostHWND", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Checks if an element is an editable text field using UI Automation patterns.
    /// This is a universal check that works for any application.
    /// </summary>
    private static bool IsEditableElement(AutomationElement element)
    {
        try
        {
            // Check if element is enabled
            if (!(bool)element.GetCurrentPropertyValue(AutomationElement.IsEnabledProperty))
            {
                return false;
            }

            // Check for TextPattern with editable capability
            if (element.TryGetCurrentPattern(TextPattern.Pattern, out var textPatternObj))
            {
                if (element.TryGetCurrentPattern(ValuePattern.Pattern, out var valuePatternObj))
                {
                    var valuePattern = (ValuePattern)valuePatternObj;
                    return !valuePattern.Current.IsReadOnly;
                }
                var controlType = element.Current.ControlType;
                return controlType == ControlType.Edit || controlType == ControlType.Document;
            }

            // Check for ValuePattern (simpler text inputs)
            if (element.TryGetCurrentPattern(ValuePattern.Pattern, out var valPatternObj))
            {
                var valuePattern = (ValuePattern)valPatternObj;
                return !valuePattern.Current.IsReadOnly;
            }

            // Check control type as fallback
            var ctrlType = element.Current.ControlType;
            return ctrlType == ControlType.Edit || ctrlType == ControlType.Document;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Builds context information for the focused element
    /// </summary>
    private static TextContext BuildTextContext(AutomationElement element)
    {
        var context = new TextContext
        {
            Element = element
        };

        try
        {
            var hwnd = element.Current.NativeWindowHandle;
            context.WindowHandle = new IntPtr(hwnd);

            var processId = element.Current.ProcessId;
            if (processId > 0)
            {
                try
                {
                    var process = System.Diagnostics.Process.GetProcessById(processId);
                    context.ApplicationName = process.ProcessName;
                }
                catch
                {
                    context.ApplicationName = "Unknown";
                }
            }
        }
        catch
        {
            // Ignore context building errors
        }

        return context;
    }

    /// <summary>
    /// Tries to get text using TextPattern (supports selection)
    /// </summary>
    private static bool TryGetTextFromTextPattern(AutomationElement element, TextCaptureResult result)
    {
        try
        {
            if (!element.TryGetCurrentPattern(TextPattern.Pattern, out var patternObj))
            {
                return false;
            }

            var textPattern = (TextPattern)patternObj;
            var selection = textPattern.GetSelection();

            if (selection != null && selection.Length > 0)
            {
                var selectedText = selection[0].GetText(-1);
                if (!string.IsNullOrEmpty(selectedText))
                {
                    result.Text = selectedText;
                    result.HasSelection = true;

                    try
                    {
                        var docRange = textPattern.DocumentRange;
                        var fullText = docRange.GetText(-1);
                        var selectionStart = fullText.IndexOf(selectedText, StringComparison.Ordinal);
                        if (selectionStart >= 0)
                        {
                            result.Context.SelectionStart = selectionStart;
                            result.Context.SelectionLength = selectedText.Length;
                        }
                    }
                    catch { }

                    return true;
                }
            }

            // No selection - get full document text
            var documentRange = textPattern.DocumentRange;
            var text = documentRange.GetText(-1);
            if (text != null)
            {
                result.Text = text;
                result.HasSelection = false;
                result.Context.SelectionStart = 0;
                result.Context.SelectionLength = 0;
                return true;
            }
        }
        catch { }

        return false;
    }

    /// <summary>
    /// Tries to get text using ValuePattern (simpler text inputs)
    /// </summary>
    private static bool TryGetTextFromValuePattern(AutomationElement element, TextCaptureResult result)
    {
        try
        {
            if (!element.TryGetCurrentPattern(ValuePattern.Pattern, out var patternObj))
            {
                return false;
            }

            var valuePattern = (ValuePattern)patternObj;
            var text = valuePattern.Current.Value;

            if (text != null)
            {
                result.Text = text;
                result.HasSelection = false;
                result.Context.SelectionStart = 0;
                result.Context.SelectionLength = 0;
                return true;
            }
        }
        catch { }

        return false;
    }

    /// <summary>
    /// Tries to get text using clipboard-based capture (Ctrl+C simulation).
    /// This is a universal method that works for any application supporting standard shortcuts.
    /// </summary>
    private static async Task<bool> TryGetTextFromClipboardAsync(TextCaptureResult result)
    {
        const int maxRetries = 2;
        
        for (int attempt = 0; attempt < maxRetries; attempt++)
        {
            try
            {
                if (attempt > 0)
                {
                    Logger.Debug($"Clipboard capture retry attempt {attempt + 1}...");
                    await Task.Delay(100);
                }

                var targetWindow = NativeMethods.GetForegroundWindow();
                Logger.Debug($"Target window handle: 0x{targetWindow:X}");

                // Release all modifier keys to avoid conflicts
                InputSimulator.ReleaseAllModifierKeys();
                await Task.Delay(150 + (attempt * 50));

                // Ensure target window is still in foreground
                var currentForeground = NativeMethods.GetForegroundWindow();
                if (currentForeground != targetWindow && targetWindow != IntPtr.Zero)
                {
                    Logger.Debug("Restoring foreground window focus");
                    NativeMethods.SetForegroundWindow(targetWindow);
                    await Task.Delay(100);
                }

                // Save current clipboard content
                result.Context.OriginalClipboardText = InputSimulator.GetClipboardText();

                // Clear clipboard
                InputSimulator.ClearClipboard();
                await Task.Delay(50);

                // Try to copy existing selection
                Logger.Debug("Attempting Ctrl+C for existing selection...");
                InputSimulator.SendCtrlC();
                await Task.Delay(250 + (attempt * 50));

                var copiedText = InputSimulator.GetClipboardText();

                if (!string.IsNullOrEmpty(copiedText))
                {
                    result.Text = copiedText;
                    result.HasSelection = true;
                    Logger.Debug($"Got selected text from clipboard: {copiedText.Length} chars");
                    return true;
                }

                // No selection - try Ctrl+A then Ctrl+C
                Logger.Debug("No selection found, trying Ctrl+A + Ctrl+C...");
                
                InputSimulator.ClearClipboard();
                await Task.Delay(50);

                InputSimulator.SendCtrlA();
                await Task.Delay(250 + (attempt * 50));
                
                InputSimulator.SendCtrlC();
                await Task.Delay(250 + (attempt * 50));

                copiedText = InputSimulator.GetClipboardText();

                if (!string.IsNullOrEmpty(copiedText))
                {
                    result.Text = copiedText;
                    result.HasSelection = false;
                    Logger.Debug($"Got all text from clipboard: {copiedText.Length} chars");
                    return true;
                }

                Logger.Debug($"Clipboard capture attempt {attempt + 1} failed - no text obtained");
            }
            catch (Exception ex)
            {
                Logger.Error($"Exception during clipboard capture attempt {attempt + 1}", ex);
            }
        }

        Logger.Debug("Clipboard capture failed after all retries");
        return false;
    }

    /// <summary>
    /// Sends Ctrl+V using SendInput with scan codes.
    /// Delegates to InputSimulator for centralized implementation.
    /// </summary>
    [Obsolete("Use InputSimulator.SendCtrlV() directly")]
    public static void SendCtrlVWithScanCode()
    {
        InputSimulator.SendCtrlV();
    }
}
