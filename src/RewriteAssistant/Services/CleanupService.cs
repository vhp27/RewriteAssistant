using System;
using System.Diagnostics;
using System.IO;

namespace RewriteAssistant.Services;

/// <summary>
/// Interface for application cleanup operations
/// </summary>
public interface ICleanupService
{
    /// <summary>
    /// Performs startup cleanup operations
    /// </summary>
    void PerformStartupCleanup();

    /// <summary>
    /// Clears the internal clipboard cache
    /// </summary>
    void ClearClipboardCache();

    /// <summary>
    /// Removes old log files
    /// </summary>
    void CleanupLogFiles();

    /// <summary>
    /// Cleans up temporary files
    /// </summary>
    void CleanupTemporaryFiles();
}

/// <summary>
/// Handles application cleanup operations including temporary files,
/// clipboard cache, and log files.
/// Requirements: 7.1
/// </summary>
public class CleanupService : ICleanupService
{
    private const string AppTempFolderName = "RewriteAssistant";
    private const string LogFolderName = "logs";
    private const int LogRetentionDays = 7;
    private const int TempFileRetentionHours = 24;

    private readonly string _tempPath;
    private readonly string _logPath;
    private readonly string _appDataPath;

    /// <summary>
    /// Creates a new CleanupService with default paths
    /// </summary>
    public CleanupService()
    {
        _appDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            AppTempFolderName);
        _tempPath = Path.Combine(Path.GetTempPath(), AppTempFolderName);
        _logPath = Path.Combine(_appDataPath, LogFolderName);
    }

    /// <summary>
    /// Creates a new CleanupService with custom paths (for testing)
    /// </summary>
    public CleanupService(string appDataPath, string tempPath, string logPath)
    {
        _appDataPath = appDataPath;
        _tempPath = tempPath;
        _logPath = logPath;
    }

    /// <inheritdoc />
    public void PerformStartupCleanup()
    {
        Logger.Debug("Performing startup cleanup...");

        try
        {
            CleanupTemporaryFiles();
            ClearClipboardCache();
            CleanupLogFiles();

            Logger.Debug("Startup cleanup completed successfully");
        }
        catch (Exception ex)
        {
            // Log but don't fail startup due to cleanup errors
            Logger.Warn($"Startup cleanup encountered errors: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public void ClearClipboardCache()
    {
        try
        {
            var clipboardCachePath = Path.Combine(_tempPath, "clipboard_cache");
            
            if (Directory.Exists(clipboardCachePath))
            {
                // Delete all files in the clipboard cache directory
                foreach (var file in Directory.GetFiles(clipboardCachePath))
                {
                    SafeDeleteFile(file);
                }

                Debug.WriteLine("Clipboard cache cleared");
            }

            // Also clear any clipboard temp files in the main temp directory
            if (Directory.Exists(_tempPath))
            {
                foreach (var file in Directory.GetFiles(_tempPath, "clipboard_*.tmp"))
                {
                    SafeDeleteFile(file);
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error clearing clipboard cache: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public void CleanupLogFiles()
    {
        try
        {
            if (!Directory.Exists(_logPath))
            {
                Logger.Debug($"Log directory does not exist: {_logPath}");
                return;
            }

            var cutoffDate = DateTime.Now.AddDays(-LogRetentionDays);
            var logPatterns = new[] { "*.log", "*.txt" };
            var deletedCount = 0;
            var totalFiles = 0;

            foreach (var pattern in logPatterns)
            {
                foreach (var file in Directory.GetFiles(_logPath, pattern))
                {
                    totalFiles++;
                    try
                    {
                        var fileInfo = new FileInfo(file);
                        if (fileInfo.LastWriteTime < cutoffDate)
                        {
                            SafeDeleteFile(file);
                            deletedCount++;
                            Logger.Debug($"Deleted old log file: {Path.GetFileName(file)}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Warn($"Error processing log file {file}: {ex.Message}");
                    }
                }
            }

            Logger.Debug($"Log cleanup: {deletedCount} deleted, {totalFiles - deletedCount} retained (retention: {LogRetentionDays} days)");
        }
        catch (Exception ex)
        {
            Logger.Warn($"Error cleaning up log files: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public void CleanupTemporaryFiles()
    {
        try
        {
            if (!Directory.Exists(_tempPath))
            {
                return;
            }

            var cutoffTime = DateTime.Now.AddHours(-TempFileRetentionHours);
            var deletedCount = 0;

            // Clean up files in the temp directory
            foreach (var file in Directory.GetFiles(_tempPath))
            {
                try
                {
                    var fileInfo = new FileInfo(file);
                    if (fileInfo.LastWriteTime < cutoffTime)
                    {
                        SafeDeleteFile(file);
                        deletedCount++;
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error deleting temp file {file}: {ex.Message}");
                }
            }

            // Clean up subdirectories (except clipboard_cache which is handled separately)
            foreach (var dir in Directory.GetDirectories(_tempPath))
            {
                var dirName = Path.GetFileName(dir);
                if (dirName == "clipboard_cache")
                {
                    continue; // Handled by ClearClipboardCache
                }

                try
                {
                    var dirInfo = new DirectoryInfo(dir);
                    if (dirInfo.LastWriteTime < cutoffTime && IsDirectoryEmpty(dir))
                    {
                        Directory.Delete(dir, false);
                        Debug.WriteLine($"Deleted empty temp directory: {dirName}");
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error deleting temp directory {dir}: {ex.Message}");
                }
            }

            if (deletedCount > 0)
            {
                Debug.WriteLine($"Deleted {deletedCount} temporary files");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error cleaning up temporary files: {ex.Message}");
        }
    }

    /// <summary>
    /// Safely deletes a file, handling common exceptions
    /// </summary>
    private static void SafeDeleteFile(string filePath)
    {
        try
        {
            if (File.Exists(filePath))
            {
                // Remove read-only attribute if set
                var attributes = File.GetAttributes(filePath);
                if ((attributes & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
                {
                    File.SetAttributes(filePath, attributes & ~FileAttributes.ReadOnly);
                }

                File.Delete(filePath);
            }
        }
        catch (IOException)
        {
            // File is in use, skip
        }
        catch (UnauthorizedAccessException)
        {
            // No permission, skip
        }
    }

    /// <summary>
    /// Checks if a directory is empty
    /// </summary>
    private static bool IsDirectoryEmpty(string path)
    {
        try
        {
            return !Directory.EnumerateFileSystemEntries(path).Any();
        }
        catch
        {
            return false;
        }
    }
}
