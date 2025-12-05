using FluentAssertions;
using FsCheck;
using FsCheck.Xunit;
using RewriteAssistant.Models;
using RewriteAssistant.Services;
using System.IO;
using System.Text.Json;
using Xunit;

namespace RewriteAssistant.Tests;

/// <summary>
/// Property-based tests for ConfigurationManager
/// 
/// **Feature: ai-rewrite-assistant, Property 9: Crash recovery restores functional state**
/// **Validates: Requirements 7.2**
/// 
/// Property: For any corrupted or incomplete configuration state (simulating crash recovery),
/// the system should either restore to a valid default state or gracefully handle the
/// corruption without crashing.
/// </summary>
public class ConfigurationManagerPropertyTests
{
    private readonly string _testConfigDir;

    public ConfigurationManagerPropertyTests()
    {
        _testConfigDir = Path.Combine(Path.GetTempPath(), "RewriteAssistantTests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(_testConfigDir);
    }

    private string GetTestConfigPath() => Path.Combine(_testConfigDir, $"config_{Guid.NewGuid()}.json");

    /// <summary>
    /// **Feature: ai-rewrite-assistant, Property 9: Crash recovery restores functional state**
    /// **Validates: Requirements 7.2**
    /// 
    /// Loading from a non-existent file should return valid defaults.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property NonExistentFile_ShouldReturnValidDefaults()
    {
        return Prop.ForAll(
            Arb.From<Guid>(),
            guid =>
            {
                var configPath = Path.Combine(_testConfigDir, $"nonexistent_{guid}.json");
                var manager = new ConfigurationManager(configPath);
                
                var config = manager.Load();
                
                return config != null &&
                       config.Hotkeys != null &&
                       config.ApiKeys != null;
            });
    }

    /// <summary>
    /// **Feature: ai-rewrite-assistant, Property 9: Crash recovery restores functional state**
    /// **Validates: Requirements 7.2**
    /// 
    /// Loading from an empty file should return valid defaults.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property EmptyFile_ShouldReturnValidDefaults()
    {
        return Prop.ForAll(
            Arb.From<Guid>(),
            guid =>
            {
                var configPath = Path.Combine(_testConfigDir, $"empty_{guid}.json");
                File.WriteAllText(configPath, string.Empty);
                
                var manager = new ConfigurationManager(configPath);
                var config = manager.Load();
                
                // Cleanup
                try { File.Delete(configPath); } catch { }
                
                return config != null &&
                       config.Hotkeys != null &&
                       config.ApiKeys != null;
            });
    }

    /// <summary>
    /// **Feature: ai-rewrite-assistant, Property 9: Crash recovery restores functional state**
    /// **Validates: Requirements 7.2**
    /// 
    /// Loading from a file with invalid JSON should return valid defaults.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property InvalidJson_ShouldReturnValidDefaults()
    {
        return Prop.ForAll(
            Arb.From<NonEmptyString>(),
            invalidContent =>
            {
                var content = invalidContent.Get;
                // Ensure it's not valid JSON
                if (IsValidJson(content))
                    return true; // Skip valid JSON
                
                var configPath = GetTestConfigPath();
                File.WriteAllText(configPath, content);
                
                var manager = new ConfigurationManager(configPath);
                var config = manager.Load();
                
                // Cleanup
                try { File.Delete(configPath); } catch { }
                
                return config != null &&
                       config.Hotkeys != null &&
                       config.ApiKeys != null;
            });
    }

    /// <summary>
    /// **Feature: ai-rewrite-assistant, Property 9: Crash recovery restores functional state**
    /// **Validates: Requirements 7.2**
    /// 
    /// Loading from a file with partial/truncated JSON should return valid defaults.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property TruncatedJson_ShouldReturnValidDefaults()
    {
        var truncatedJsonSamples = new[]
        {
            "{",
            "{ \"isEnabled\":",
            "{ \"isEnabled\": true,",
            "{ \"isEnabled\": true, \"hotkeys\": [",
            "{ \"isEnabled\": true, \"hotkeys\": [{",
            "{ \"isEnabled\": true, \"hotkeys\": [{ \"id\":",
        };

        return Prop.ForAll(
            Gen.Elements(truncatedJsonSamples).ToArbitrary(),
            truncatedJson =>
            {
                var configPath = GetTestConfigPath();
                File.WriteAllText(configPath, truncatedJson);
                
                var manager = new ConfigurationManager(configPath);
                var config = manager.Load();
                
                // Cleanup
                try { File.Delete(configPath); } catch { }
                
                return config != null &&
                       config.Hotkeys != null &&
                       config.ApiKeys != null;
            });
    }

    /// <summary>
    /// **Feature: ai-rewrite-assistant, Property 9: Crash recovery restores functional state**
    /// **Validates: Requirements 7.2**
    /// 
    /// Loading from a file with null values should handle gracefully.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property NullValues_ShouldBeHandledGracefully()
    {
        var nullValueJsonSamples = new[]
        {
            "null",
            "{ \"isEnabled\": null }",
            "{ \"hotkeys\": null }",
            "{ \"apiKeys\": null }",
            "{ \"isEnabled\": true, \"hotkeys\": null, \"apiKeys\": null }",
        };

        return Prop.ForAll(
            Gen.Elements(nullValueJsonSamples).ToArbitrary(),
            jsonWithNulls =>
            {
                var configPath = GetTestConfigPath();
                File.WriteAllText(configPath, jsonWithNulls);
                
                var manager = new ConfigurationManager(configPath);
                var config = manager.Load();
                
                // Cleanup
                try { File.Delete(configPath); } catch { }
                
                return config != null &&
                       config.Hotkeys != null &&
                       config.ApiKeys != null;
            });
    }

    /// <summary>
    /// **Feature: ai-rewrite-assistant, Property 9: Crash recovery restores functional state**
    /// **Validates: Requirements 7.2**
    /// 
    /// Valid configuration should round-trip correctly.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ValidConfig_ShouldRoundTrip()
    {
        return Prop.ForAll(
            Arb.From<bool>(),
            Arb.From<bool>(),
            (isEnabled, startWithWindows) =>
            {
                var configPath = GetTestConfigPath();
                var manager = new ConfigurationManager(configPath);
                
                var originalConfig = new AppConfiguration
                {
                    IsEnabled = isEnabled,
                    StartWithWindows = startWithWindows,
                    DefaultStyleId = "grammar_fix",
                    Prompts = new List<CustomPrompt>(),
                    Styles = new List<CustomStyle>(),
                    Hotkeys = new List<HotkeyConfig>(),
                    ApiKeys = new ApiKeyStorage()
                };
                
                manager.Save(originalConfig);
                var loadedConfig = manager.Load();
                
                // Cleanup
                try { File.Delete(configPath); } catch { }
                
                return loadedConfig.IsEnabled == originalConfig.IsEnabled &&
                       loadedConfig.StartWithWindows == originalConfig.StartWithWindows;
            });
    }

    /// <summary>
    /// **Feature: ai-rewrite-assistant, Property 9: Crash recovery restores functional state**
    /// **Validates: Requirements 7.2**
    /// 
    /// Default configuration should have valid initial values.
    /// </summary>
    [Fact]
    public void DefaultConfiguration_ShouldHaveValidValues()
    {
        var config = AppConfiguration.CreateDefault();
        
        config.Should().NotBeNull();
        config.Hotkeys.Should().NotBeNull();
        config.ApiKeys.Should().NotBeNull();
        var validStyles = new[] 
        {
            "grammar_fix",
            "formal_tone",
            "casual_tone",
            "shorten_text",
            "expand_text"
        };
        validStyles.Should().Contain(config.DefaultStyleId);
    }

    /// <summary>
    /// **Feature: ai-rewrite-assistant, Property 9: Crash recovery restores functional state**
    /// **Validates: Requirements 7.2**
    /// 
    /// Loading should never throw exceptions, always return a valid config.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property Load_ShouldNeverThrow()
    {
        var problematicContents = new[]
        {
            "",
            " ",
            "\n",
            "\t",
            "null",
            "undefined",
            "[]",
            "\"string\"",
            "123",
            "true",
            "false",
            "{",
            "}",
            "{ broken",
            "random garbage content",
            new string('x', 10000), // Large content
        };

        return Prop.ForAll(
            Gen.Elements(problematicContents).ToArbitrary(),
            content =>
            {
                var configPath = GetTestConfigPath();
                File.WriteAllText(configPath, content);
                
                var manager = new ConfigurationManager(configPath);
                
                AppConfiguration? config = null;
                Exception? caughtException = null;
                
                try
                {
                    config = manager.Load();
                }
                catch (Exception ex)
                {
                    caughtException = ex;
                }
                
                // Cleanup
                try { File.Delete(configPath); } catch { }
                
                // Should not throw and should return valid config
                return caughtException == null && 
                       config != null &&
                       config.Hotkeys != null &&
                       config.ApiKeys != null;
            });
    }

    /// <summary>
    /// Helper to check if a string is valid JSON
    /// </summary>
    private static bool IsValidJson(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return false;
            
        try
        {
            JsonDocument.Parse(content);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
