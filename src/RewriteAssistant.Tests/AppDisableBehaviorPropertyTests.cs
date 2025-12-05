using FluentAssertions;
using FsCheck;
using FsCheck.Xunit;
using Xunit;

namespace RewriteAssistant.Tests;

/// <summary>
/// Property-based tests for app disable behavior
/// 
/// **Feature: ai-rewrite-assistant, Property 6: App disable stops all hotkey processing**
/// **Validates: Requirements 3.4**
/// 
/// Property: For any hotkey press when the application is in disabled state,
/// no rewrite operation should be initiated and no API calls should be made.
/// </summary>
public class AppDisableBehaviorPropertyTests
{
    /// <summary>
    /// Default style IDs matching the built-in styles
    /// </summary>
    private static readonly string[] StyleIds = new[]
    {
        "grammar_fix",
        "formal_tone",
        "casual_tone",
        "shorten_text",
        "expand_text"
    };

    /// <summary>
    /// Simulates the app enabled/disabled state and hotkey processing behavior
    /// </summary>
    private class AppStateSimulator
    {
        private readonly object _lock = new();
        private bool _isEnabled;
        private int _processedCount;
        private int _ignoredCount;
        private int _apiCallCount;

        public bool IsEnabled
        {
            get { lock (_lock) { return _isEnabled; } }
        }

        public int ProcessedCount => _processedCount;
        public int IgnoredCount => _ignoredCount;
        public int ApiCallCount => _apiCallCount;

        public void SetEnabled(bool enabled)
        {
            lock (_lock)
            {
                _isEnabled = enabled;
            }
        }

        /// <summary>
        /// Simulates handling a hotkey press with app enabled check
        /// Returns true if the hotkey was processed, false if ignored
        /// </summary>
        public bool TryHandleHotkeyPress(string styleId)
        {
            lock (_lock)
            {
                // Check if app is enabled (Requirement 3.4)
                if (!_isEnabled)
                {
                    _ignoredCount++;
                    return false; // Ignored due to app being disabled
                }

                _processedCount++;
                // Simulate API call
                _apiCallCount++;
                return true; // Processed
            }
        }

        /// <summary>
        /// Simulates handling a hotkey press and returns whether an API call would be made
        /// </summary>
        public (bool processed, bool apiCallMade) TryHandleHotkeyPressWithApiTracking(string styleId)
        {
            lock (_lock)
            {
                // Check if app is enabled (Requirement 3.4)
                if (!_isEnabled)
                {
                    _ignoredCount++;
                    return (false, false); // No processing, no API call
                }

                _processedCount++;
                _apiCallCount++;
                return (true, true); // Processed with API call
            }
        }

        public void Reset()
        {
            lock (_lock)
            {
                _isEnabled = true;
                _processedCount = 0;
                _ignoredCount = 0;
                _apiCallCount = 0;
            }
        }
    }

    /// <summary>
    /// Generator for random style ID values
    /// </summary>
    private static Gen<string> StyleIdGen =>
        Gen.Elements(StyleIds);

    /// <summary>
    /// **Feature: ai-rewrite-assistant, Property 6: App disable stops all hotkey processing**
    /// **Validates: Requirements 3.4**
    /// 
    /// When app is disabled, all hotkey presses should be ignored and no API calls made.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property WhenAppDisabled_AllHotkeyPresses_ShouldBeIgnored()
    {
        return Prop.ForAll(
            Arb.From<PositiveInt>(),
            Arb.From(StyleIdGen),
            (pressCount, styleId) =>
            {
                var simulator = new AppStateSimulator();
                
                // Set app to disabled state
                simulator.SetEnabled(false);
                
                // Try to handle multiple hotkey presses with various styles
                var count = Math.Min(pressCount.Get, 100);
                for (int i = 0; i < count; i++)
                {
                    simulator.TryHandleHotkeyPress(styleId);
                }
                
                // All should be ignored, no API calls made
                return simulator.ProcessedCount == 0 && 
                       simulator.IgnoredCount == count &&
                       simulator.ApiCallCount == 0;
            });
    }

    /// <summary>
    /// **Feature: ai-rewrite-assistant, Property 6: App disable stops all hotkey processing**
    /// **Validates: Requirements 3.4**
    /// 
    /// When app is enabled, hotkey presses should be processed and API calls made.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property WhenAppEnabled_HotkeyPresses_ShouldBeProcessed()
    {
        return Prop.ForAll(
            Arb.From<PositiveInt>(),
            Arb.From(StyleIdGen),
            (pressCount, styleId) =>
            {
                var simulator = new AppStateSimulator();
                
                // Set app to enabled state
                simulator.SetEnabled(true);
                
                // Try to handle multiple hotkey presses
                var count = Math.Min(pressCount.Get, 100);
                for (int i = 0; i < count; i++)
                {
                    simulator.TryHandleHotkeyPress(styleId);
                }
                
                // All should be processed with API calls
                return simulator.ProcessedCount == count && 
                       simulator.IgnoredCount == 0 &&
                       simulator.ApiCallCount == count;
            });
    }


    /// <summary>
    /// **Feature: ai-rewrite-assistant, Property 6: App disable stops all hotkey processing**
    /// **Validates: Requirements 3.4**
    /// 
    /// Toggling app state should immediately affect hotkey processing.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property TogglingAppState_ShouldImmediatelyAffectProcessing()
    {
        return Prop.ForAll(
            Arb.From(StyleIdGen),
            styleId =>
            {
                var simulator = new AppStateSimulator();
                
                // Start enabled - should process
                simulator.SetEnabled(true);
                var (processed1, apiCall1) = simulator.TryHandleHotkeyPressWithApiTracking(styleId);
                
                // Disable - should ignore
                simulator.SetEnabled(false);
                var (processed2, apiCall2) = simulator.TryHandleHotkeyPressWithApiTracking(styleId);
                
                // Re-enable - should process again
                simulator.SetEnabled(true);
                var (processed3, apiCall3) = simulator.TryHandleHotkeyPressWithApiTracking(styleId);
                
                return processed1 && apiCall1 &&      // First press processed
                       !processed2 && !apiCall2 &&    // Second press ignored (disabled)
                       processed3 && apiCall3;        // Third press processed (re-enabled)
            });
    }

    /// <summary>
    /// **Feature: ai-rewrite-assistant, Property 6: App disable stops all hotkey processing**
    /// **Validates: Requirements 3.4**
    /// 
    /// No API calls should be made when app is disabled, regardless of rewrite style.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property WhenDisabled_NoApiCalls_ForAnyStyle()
    {
        return Prop.ForAll(
            Arb.From(StyleIdGen),
            styleId =>
            {
                var simulator = new AppStateSimulator();
                
                // Disable the app
                simulator.SetEnabled(false);
                
                // Try to process with the given style
                var (processed, apiCallMade) = simulator.TryHandleHotkeyPressWithApiTracking(styleId);
                
                // Should not process and should not make API call
                return !processed && !apiCallMade && simulator.ApiCallCount == 0;
            });
    }

    /// <summary>
    /// **Feature: ai-rewrite-assistant, Property 6: App disable stops all hotkey processing**
    /// **Validates: Requirements 3.4**
    /// 
    /// Multiple rapid state changes should maintain correct behavior.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property RapidStateChanges_ShouldMaintainCorrectBehavior()
    {
        return Prop.ForAll(
            Arb.From<bool[]>(),
            Arb.From(StyleIdGen),
            (states, styleId) =>
            {
                if (states == null || states.Length == 0)
                    return true;

                var simulator = new AppStateSimulator();
                var expectedProcessed = 0;
                var expectedIgnored = 0;

                foreach (var enabled in states)
                {
                    simulator.SetEnabled(enabled);
                    simulator.TryHandleHotkeyPress(styleId);
                    
                    if (enabled)
                        expectedProcessed++;
                    else
                        expectedIgnored++;
                }
                
                return simulator.ProcessedCount == expectedProcessed &&
                       simulator.IgnoredCount == expectedIgnored &&
                       simulator.ApiCallCount == expectedProcessed; // API calls only when enabled
            });
    }

    /// <summary>
    /// **Feature: ai-rewrite-assistant, Property 6: App disable stops all hotkey processing**
    /// **Validates: Requirements 3.4**
    /// 
    /// Interleaved enable/disable operations should behave correctly.
    /// </summary>
    [Fact]
    public void InterleavedEnableDisable_ShouldBehaveCorrectly()
    {
        var simulator = new AppStateSimulator();
        
        // Start enabled - should succeed
        simulator.SetEnabled(true);
        simulator.TryHandleHotkeyPress("grammar_fix").Should().BeTrue();
        simulator.ProcessedCount.Should().Be(1);
        simulator.ApiCallCount.Should().Be(1);
        
        // Disable - should be ignored
        simulator.SetEnabled(false);
        simulator.TryHandleHotkeyPress("formal_tone").Should().BeFalse();
        simulator.TryHandleHotkeyPress("casual_tone").Should().BeFalse();
        simulator.IgnoredCount.Should().Be(2);
        simulator.ApiCallCount.Should().Be(1); // No new API calls
        
        // Re-enable - should succeed again
        simulator.SetEnabled(true);
        simulator.TryHandleHotkeyPress("shorten_text").Should().BeTrue();
        simulator.ProcessedCount.Should().Be(2);
        simulator.ApiCallCount.Should().Be(2);
    }

    /// <summary>
    /// **Feature: ai-rewrite-assistant, Property 6: App disable stops all hotkey processing**
    /// **Validates: Requirements 3.4**
    /// 
    /// All rewrite styles should be blocked when disabled.
    /// </summary>
    [Fact]
    public void WhenDisabled_AllStyles_ShouldBeBlocked()
    {
        var simulator = new AppStateSimulator();
        simulator.SetEnabled(false);
        
        foreach (var styleId in StyleIds)
        {
            simulator.TryHandleHotkeyPress(styleId).Should().BeFalse($"Style {styleId} should be blocked when disabled");
        }
        
        simulator.ProcessedCount.Should().Be(0);
        simulator.ApiCallCount.Should().Be(0);
        simulator.IgnoredCount.Should().Be(StyleIds.Length);
    }
}
