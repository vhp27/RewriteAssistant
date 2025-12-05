using System.Text.Json.Serialization;

namespace RewriteAssistant.Models;

/// <summary>
/// Configuration for a single hotkey binding
/// </summary>
public class HotkeyConfig
{
    /// <summary>
    /// Unique identifier for this hotkey
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Modifier keys (Ctrl, Alt, Shift, Win)
    /// </summary>
    [JsonPropertyName("modifiers")]
    public List<string> Modifiers { get; set; } = new();

    /// <summary>
    /// The main key (e.g., "G", "F", "1")
    /// </summary>
    [JsonPropertyName("key")]
    public string Key { get; set; } = string.Empty;


    /// <summary>
    /// Gets the Windows modifier flags for RegisterHotKey
    /// </summary>
    [JsonIgnore]
    public uint ModifierFlags
    {
        get
        {
            uint flags = 0;
            foreach (var mod in Modifiers)
            {
                flags |= mod.ToLowerInvariant() switch
                {
                    "ctrl" or "control" => 0x0002u, // MOD_CONTROL
                    "alt" => 0x0001u, // MOD_ALT
                    "shift" => 0x0004u, // MOD_SHIFT
                    "win" or "windows" => 0x0008u, // MOD_WIN
                    _ => 0u
                };
            }
            return flags;
        }
    }

    /// <summary>
    /// Gets the virtual key code for the main key
    /// </summary>
    [JsonIgnore]
    public uint VirtualKeyCode
    {
        get
        {
            if (string.IsNullOrEmpty(Key))
                return 0;

            var keyUpper = Key.ToUpperInvariant();

            // Single letter keys (A-Z)
            if (keyUpper.Length == 1 && keyUpper[0] >= 'A' && keyUpper[0] <= 'Z')
            {
                return (uint)(byte)keyUpper[0];
            }

            // Number keys (0-9)
            if (keyUpper.Length == 1 && keyUpper[0] >= '0' && keyUpper[0] <= '9')
            {
                return (uint)(byte)keyUpper[0];
            }

            // Function keys (F1-F12)
            if (keyUpper.StartsWith("F") && int.TryParse(keyUpper.Substring(1), out var fNum) && fNum >= 1 && fNum <= 12)
            {
                return (uint)(0x70 + fNum - 1); // VK_F1 = 0x70
            }

            // Special keys
            return keyUpper switch
            {
                "SPACE" => 0x20,
                "ENTER" or "RETURN" => 0x0D,
                "TAB" => 0x09,
                "ESCAPE" or "ESC" => 0x1B,
                "BACKSPACE" => 0x08,
                "DELETE" or "DEL" => 0x2E,
                "INSERT" or "INS" => 0x2D,
                "HOME" => 0x24,
                "END" => 0x23,
                "PAGEUP" or "PGUP" => 0x21,
                "PAGEDOWN" or "PGDN" => 0x22,
                "UP" => 0x26,
                "DOWN" => 0x28,
                "LEFT" => 0x25,
                "RIGHT" => 0x27,
                // OEM keys (punctuation/symbols)
                "`" => 0xC0,  // VK_OEM_3 (backtick/tilde)
                "-" => 0xBD,  // VK_OEM_MINUS
                "=" => 0xBB,  // VK_OEM_PLUS (equals/plus)
                "[" => 0xDB,  // VK_OEM_4 (open bracket)
                "]" => 0xDD,  // VK_OEM_6 (close bracket)
                "\\" => 0xDC, // VK_OEM_5 (backslash)
                ";" => 0xBA,  // VK_OEM_1 (semicolon)
                "'" => 0xDE,  // VK_OEM_7 (quote)
                "," => 0xBC,  // VK_OEM_COMMA
                "." => 0xBE,  // VK_OEM_PERIOD
                "/" => 0xBF,  // VK_OEM_2 (slash)
                _ => 0
            };
        }
    }
}
