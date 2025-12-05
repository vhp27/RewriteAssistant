using System;

namespace RewriteAssistant.Models;

/// <summary>
/// Event arguments for prompt change events
/// </summary>
public class PromptChangedEventArgs : EventArgs
{
    public ChangeType ChangeType { get; }
    public CustomPrompt Prompt { get; }
    public string? OldPromptId { get; }

    public PromptChangedEventArgs(ChangeType changeType, CustomPrompt prompt, string? oldPromptId = null)
    {
        ChangeType = changeType;
        Prompt = prompt;
        OldPromptId = oldPromptId;
    }
}

/// <summary>
/// Event arguments for style change events
/// </summary>
public class StyleChangedEventArgs : EventArgs
{
    public ChangeType ChangeType { get; }
    public CustomStyle Style { get; }
    public HotkeyConfig? OldHotkey { get; }

    public StyleChangedEventArgs(ChangeType changeType, CustomStyle style, HotkeyConfig? oldHotkey = null)
    {
        ChangeType = changeType;
        Style = style;
        OldHotkey = oldHotkey;
    }
}

/// <summary>
/// Event arguments for hotkey change events
/// </summary>
public class HotkeyChangedEventArgs : EventArgs
{
    public ChangeType ChangeType { get; }
    public HotkeyConfig? OldHotkey { get; }
    public HotkeyConfig? NewHotkey { get; }
    public string StyleId { get; }

    public HotkeyChangedEventArgs(ChangeType changeType, string styleId, HotkeyConfig? oldHotkey, HotkeyConfig? newHotkey)
    {
        ChangeType = changeType;
        StyleId = styleId;
        OldHotkey = oldHotkey;
        NewHotkey = newHotkey;
    }
}

/// <summary>
/// Result of hotkey validation
/// </summary>
public class HotkeyValidationResult
{
    public bool IsValid { get; }
    public string? ConflictingStyleId { get; }
    public string? ConflictingStyleName { get; }
    public string? ErrorMessage { get; }

    private HotkeyValidationResult(bool isValid, string? conflictingStyleId = null, string? conflictingStyleName = null, string? errorMessage = null)
    {
        IsValid = isValid;
        ConflictingStyleId = conflictingStyleId;
        ConflictingStyleName = conflictingStyleName;
        ErrorMessage = errorMessage;
    }

    public static HotkeyValidationResult Valid() => new(true);

    public static HotkeyValidationResult Conflict(string styleId, string styleName) =>
        new(false, styleId, styleName, $"Hotkey conflicts with existing style '{styleName}'");

    public static HotkeyValidationResult Invalid(string errorMessage) =>
        new(false, errorMessage: errorMessage);
}

/// <summary>
/// Type of configuration change
/// </summary>
public enum ChangeType
{
    Added,
    Updated,
    Deleted
}
