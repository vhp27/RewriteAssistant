using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;

namespace RewriteAssistant.Services;

/// <summary>
/// Simple logging service for debugging hotkey and application issues.
/// Logs to both Debug output and a file for easier troubleshooting.
/// </summary>
public static class Logger
{
    private static readonly object _lock = new();
    private static readonly string _logFilePath;
    private static bool _isInitialized;

    static Logger()
    {
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var logDir = Path.Combine(appDataPath, "RewriteAssistant", "logs");
        
        try
        {
            if (!Directory.Exists(logDir))
            {
                Directory.CreateDirectory(logDir);
            }
            
            _logFilePath = Path.Combine(logDir, $"rewrite_assistant_{DateTime.Now:yyyyMMdd_HHmmss}.log");
            _isInitialized = true;
        }
        catch
        {
            _logFilePath = string.Empty;
            _isInitialized = false;
        }
    }

    public static void Info(string message, [CallerMemberName] string caller = "", [CallerFilePath] string file = "")
    {
        Log("INFO", message, caller, file);
    }

    public static void Debug(string message, [CallerMemberName] string caller = "", [CallerFilePath] string file = "")
    {
        Log("DEBUG", message, caller, file);
    }

    public static void Error(string message, Exception? ex = null, [CallerMemberName] string caller = "", [CallerFilePath] string file = "")
    {
        var fullMessage = ex != null ? $"{message}: {ex.Message}\n{ex.StackTrace}" : message;
        Log("ERROR", fullMessage, caller, file);
    }

    public static void Warn(string message, [CallerMemberName] string caller = "", [CallerFilePath] string file = "")
    {
        Log("WARN", message, caller, file);
    }


    private static void Log(string level, string message, string caller, string file)
    {
        var fileName = Path.GetFileNameWithoutExtension(file);
        var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
        var logMessage = $"[{timestamp}] [{level}] [{fileName}.{caller}] {message}";

        // Always write to Debug output
        System.Diagnostics.Debug.WriteLine(logMessage);

        // Also write to console if available
        Console.WriteLine(logMessage);

        // Write to file
        if (_isInitialized && !string.IsNullOrEmpty(_logFilePath))
        {
            lock (_lock)
            {
                try
                {
                    File.AppendAllText(_logFilePath, logMessage + Environment.NewLine);
                }
                catch
                {
                    // Ignore file write errors
                }
            }
        }
    }

    public static string GetLogFilePath() => _logFilePath;
}
