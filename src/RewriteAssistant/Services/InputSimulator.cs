using System.Windows;

namespace RewriteAssistant.Services;

/// <summary>
/// Centralized service for keyboard input simulation and clipboard operations.
/// Consolidates duplicate logic from TextCaptureService and TextReplaceService.
/// Requirements: 2.1, 2.2, 2.3, 2.5
/// </summary>
public static class InputSimulator
{
    /// <summary>
    /// Releases all modifier keys (Ctrl, Shift, Alt, Win) to prevent conflicts
    /// </summary>
    public static void ReleaseAllModifierKeys()
    {
        // Use keybd_event with scan codes
        NativeMethods.keybd_event((byte)NativeMethods.VK_CONTROL, 0x1D, NativeMethods.KEYEVENTF_KEYUP, UIntPtr.Zero);
        NativeMethods.keybd_event((byte)NativeMethods.VK_LCONTROL, 0x1D, NativeMethods.KEYEVENTF_KEYUP, UIntPtr.Zero);
        NativeMethods.keybd_event((byte)NativeMethods.VK_RCONTROL, 0x1D, NativeMethods.KEYEVENTF_KEYUP | NativeMethods.KEYEVENTF_EXTENDEDKEY, UIntPtr.Zero);
        NativeMethods.keybd_event((byte)NativeMethods.VK_SHIFT, 0x2A, NativeMethods.KEYEVENTF_KEYUP, UIntPtr.Zero);
        NativeMethods.keybd_event((byte)NativeMethods.VK_LSHIFT, 0x2A, NativeMethods.KEYEVENTF_KEYUP, UIntPtr.Zero);
        NativeMethods.keybd_event((byte)NativeMethods.VK_RSHIFT, 0x36, NativeMethods.KEYEVENTF_KEYUP, UIntPtr.Zero);
        NativeMethods.keybd_event((byte)NativeMethods.VK_MENU, 0x38, NativeMethods.KEYEVENTF_KEYUP, UIntPtr.Zero);
        NativeMethods.keybd_event((byte)NativeMethods.VK_LWIN, 0x5B, NativeMethods.KEYEVENTF_KEYUP, UIntPtr.Zero);
        
        // Also use SendInput as backup
        var inputs = new NativeMethods.INPUT[8];
        inputs[0] = CreateKeyInput(NativeMethods.VK_CONTROL, 0x1D, false);
        inputs[1] = CreateKeyInput(NativeMethods.VK_LCONTROL, 0x1D, false);
        inputs[2] = CreateKeyInput(NativeMethods.VK_SHIFT, 0x2A, false);
        inputs[3] = CreateKeyInput(NativeMethods.VK_LSHIFT, 0x2A, false);
        inputs[4] = CreateKeyInput(NativeMethods.VK_RSHIFT, 0x36, false);
        inputs[5] = CreateKeyInput(NativeMethods.VK_MENU, 0x38, false);
        inputs[6] = CreateKeyInput(NativeMethods.VK_LWIN, 0x5B, false);
        inputs[7] = CreateKeyInput(NativeMethods.VK_RCONTROL, 0x1D, false);
        NativeMethods.SendInput((uint)inputs.Length, inputs, NativeMethods.INPUT.Size);
        
        Logger.Debug("Released all modifier keys");
    }

    /// <summary>
    /// Sends Ctrl+C using SendInput with scan codes
    /// </summary>
    public static void SendCtrlC()
    {
        var inputs = new NativeMethods.INPUT[4];
        inputs[0] = CreateKeyInput(NativeMethods.VK_CONTROL, 0x1D, true);
        inputs[1] = CreateKeyInput(NativeMethods.VK_C, 0x2E, true);
        inputs[2] = CreateKeyInput(NativeMethods.VK_C, 0x2E, false);
        inputs[3] = CreateKeyInput(NativeMethods.VK_CONTROL, 0x1D, false);

        var sent = NativeMethods.SendInput((uint)inputs.Length, inputs, NativeMethods.INPUT.Size);
        Logger.Debug($"SendCtrlC: sent {sent} inputs");
    }


    /// <summary>
    /// Sends Ctrl+A using SendInput with scan codes
    /// </summary>
    public static void SendCtrlA()
    {
        var inputs = new NativeMethods.INPUT[4];
        inputs[0] = CreateKeyInput(NativeMethods.VK_CONTROL, 0x1D, true);
        inputs[1] = CreateKeyInput(NativeMethods.VK_A, 0x1E, true);
        inputs[2] = CreateKeyInput(NativeMethods.VK_A, 0x1E, false);
        inputs[3] = CreateKeyInput(NativeMethods.VK_CONTROL, 0x1D, false);

        var sent = NativeMethods.SendInput((uint)inputs.Length, inputs, NativeMethods.INPUT.Size);
        Logger.Debug($"SendCtrlA: sent {sent} inputs");
    }

    /// <summary>
    /// Sends Ctrl+V using SendInput with scan codes
    /// </summary>
    public static void SendCtrlV()
    {
        var inputs = new NativeMethods.INPUT[4];
        inputs[0] = CreateKeyInput(NativeMethods.VK_CONTROL, 0x1D, true);
        inputs[1] = CreateKeyInput(NativeMethods.VK_V, 0x2F, true);
        inputs[2] = CreateKeyInput(NativeMethods.VK_V, 0x2F, false);
        inputs[3] = CreateKeyInput(NativeMethods.VK_CONTROL, 0x1D, false);

        var sent = NativeMethods.SendInput((uint)inputs.Length, inputs, NativeMethods.INPUT.Size);
        Logger.Debug($"SendCtrlV: sent {sent} inputs");
    }

    /// <summary>
    /// Gets clipboard text on an STA thread (required for WPF clipboard operations)
    /// </summary>
    /// <returns>The clipboard text, or null if clipboard doesn't contain text</returns>
    public static string? GetClipboardText()
    {
        string? result = null;
        var thread = new Thread(() =>
        {
            try
            {
                if (Clipboard.ContainsText())
                {
                    result = Clipboard.GetText();
                }
            }
            catch (Exception ex)
            {
                Logger.Debug($"Clipboard read error: {ex.Message}");
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join(1000);
        return result;
    }

    /// <summary>
    /// Sets clipboard text on an STA thread
    /// </summary>
    /// <param name="text">The text to set on the clipboard</param>
    public static void SetClipboardText(string text)
    {
        var thread = new Thread(() =>
        {
            try
            {
                Clipboard.SetText(text);
            }
            catch (Exception ex)
            {
                Logger.Debug($"Clipboard set error: {ex.Message}");
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join(1000);
    }

    /// <summary>
    /// Clears clipboard on an STA thread
    /// </summary>
    public static void ClearClipboard()
    {
        var thread = new Thread(() =>
        {
            try
            {
                Clipboard.Clear();
            }
            catch (Exception ex)
            {
                Logger.Debug($"Clipboard clear error: {ex.Message}");
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join(1000);
    }

    /// <summary>
    /// Creates a keyboard input structure with both virtual key and scan code
    /// </summary>
    /// <param name="vk">Virtual key code</param>
    /// <param name="scan">Scan code</param>
    /// <param name="keyDown">True for key down, false for key up</param>
    /// <returns>INPUT structure for SendInput</returns>
    internal static NativeMethods.INPUT CreateKeyInput(ushort vk, ushort scan, bool keyDown)
    {
        return new NativeMethods.INPUT
        {
            type = NativeMethods.INPUT_KEYBOARD,
            u = new NativeMethods.InputUnion
            {
                ki = new NativeMethods.KEYBDINPUT
                {
                    wVk = vk,
                    wScan = scan,
                    dwFlags = keyDown ? NativeMethods.KEYEVENTF_SCANCODE : (NativeMethods.KEYEVENTF_SCANCODE | NativeMethods.KEYEVENTF_KEYUP),
                    time = 0,
                    dwExtraInfo = IntPtr.Zero
                }
            }
        };
    }
}
