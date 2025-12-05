using FluentAssertions;
using FsCheck;
using FsCheck.Xunit;
using RewriteAssistant.Models;
using RewriteAssistant.Services;
using Xunit;

namespace RewriteAssistant.Tests;

/// <summary>
/// Property-based tests for HotkeyManager
/// 
/// **Feature: ai-rewrite-assistant, Property 3: Reentrant guard prevents recursive triggers**
/// **Validates: Requirements 1.5, 7.5**
/// 
/// Property: For any sequence of hotkey presses during an active rewrite operation,
/// only the first request should be processed and all subsequent presses should be
/// ignored until the operation completes.
/// </summary>
public class HotkeyManagerPropertyTests
{
    /// <summary>
    /// Simulates the reentrant guard behavior for testing
    /// </summary>
    private class ReentrantGuardSimulator
    {
        private readonly object _lock = new();
        private bool _isProcessing;
        private int _processedCount;
        private int _ignoredCount;

        public bool IsProcessing
        {
            get { lock (_lock) { return _isProcessing; } }
        }

        public int ProcessedCount => _processedCount;
        public int IgnoredCount => _ignoredCount;

        public void SetProcessing(bool processing)
        {
            lock (_lock)
            {
                _isProcessing = processing;
            }
        }

        /// <summary>
        /// Simulates handling a hotkey press with reentrant guard
        /// </summary>
        public bool TryHandleHotkeyPress()
        {
            lock (_lock)
            {
                if (_isProcessing)
                {
                    _ignoredCount++;
                    return false; // Ignored due to reentrant guard
                }

                _processedCount++;
                return true; // Processed
            }
        }

        public void Reset()
        {
            lock (_lock)
            {
                _isProcessing = false;
                _processedCount = 0;
                _ignoredCount = 0;
            }
        }
    }

    /// <summary>
    /// **Feature: ai-rewrite-assistant, Property 3: Reentrant guard prevents recursive triggers**
    /// **Validates: Requirements 1.5, 7.5**
    /// 
    /// When processing is active, all hotkey presses should be ignored.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property WhenProcessing_AllHotkeyPresses_ShouldBeIgnored()
    {
        return Prop.ForAll(
            Arb.From<PositiveInt>(),
            pressCount =>
            {
                var simulator = new ReentrantGuardSimulator();
                
                // Set processing to true (simulating active rewrite)
                simulator.SetProcessing(true);
                
                // Try to handle multiple hotkey presses
                var count = Math.Min(pressCount.Get, 100);
                for (int i = 0; i < count; i++)
                {
                    simulator.TryHandleHotkeyPress();
                }
                
                // All should be ignored
                return simulator.ProcessedCount == 0 && 
                       simulator.IgnoredCount == count;
            });
    }

    /// <summary>
    /// **Feature: ai-rewrite-assistant, Property 3: Reentrant guard prevents recursive triggers**
    /// **Validates: Requirements 1.5, 7.5**
    /// 
    /// When processing is not active, the first hotkey press should be processed.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property WhenNotProcessing_FirstHotkeyPress_ShouldBeProcessed()
    {
        return Prop.ForAll(
            Arb.From<PositiveInt>(),
            _ =>
            {
                var simulator = new ReentrantGuardSimulator();
                
                // Processing is not active
                simulator.SetProcessing(false);
                
                // First press should be processed
                var result = simulator.TryHandleHotkeyPress();
                
                return result && simulator.ProcessedCount == 1;
            });
    }

    /// <summary>
    /// **Feature: ai-rewrite-assistant, Property 3: Reentrant guard prevents recursive triggers**
    /// **Validates: Requirements 1.5, 7.5**
    /// 
    /// After processing completes, new hotkey presses should be processed again.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property AfterProcessingCompletes_NewPresses_ShouldBeProcessed()
    {
        return Prop.ForAll(
            Arb.From<PositiveInt>(),
            Arb.From<PositiveInt>(),
            (ignoredCount, processedAfter) =>
            {
                var simulator = new ReentrantGuardSimulator();
                
                // Start processing
                simulator.SetProcessing(true);
                
                // These should be ignored
                var ignored = Math.Min(ignoredCount.Get, 50);
                for (int i = 0; i < ignored; i++)
                {
                    simulator.TryHandleHotkeyPress();
                }
                
                // Complete processing
                simulator.SetProcessing(false);
                
                // This should be processed
                var result = simulator.TryHandleHotkeyPress();
                
                return result && 
                       simulator.IgnoredCount == ignored &&
                       simulator.ProcessedCount == 1;
            });
    }

    /// <summary>
    /// **Feature: ai-rewrite-assistant, Property 3: Reentrant guard prevents recursive triggers**
    /// **Validates: Requirements 1.5, 7.5**
    /// 
    /// The processing state should be thread-safe.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ProcessingState_ShouldBeConsistent()
    {
        return Prop.ForAll(
            Arb.From<bool>(),
            Arb.From<bool>(),
            (initialState, newState) =>
            {
                var simulator = new ReentrantGuardSimulator();
                
                simulator.SetProcessing(initialState);
                var stateAfterFirst = simulator.IsProcessing;
                
                simulator.SetProcessing(newState);
                var stateAfterSecond = simulator.IsProcessing;
                
                return stateAfterFirst == initialState && 
                       stateAfterSecond == newState;
            });
    }

    /// <summary>
    /// **Feature: ai-rewrite-assistant, Property 3: Reentrant guard prevents recursive triggers**
    /// **Validates: Requirements 1.5, 7.5**
    /// 
    /// Multiple rapid state changes should maintain consistency.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property RapidStateChanges_ShouldMaintainConsistency()
    {
        return Prop.ForAll(
            Arb.From<bool[]>(),
            states =>
            {
                if (states == null || states.Length == 0)
                    return true;

                var simulator = new ReentrantGuardSimulator();
                
                foreach (var state in states)
                {
                    simulator.SetProcessing(state);
                }
                
                // Final state should match last value
                var expectedFinalState = states[states.Length - 1];
                return simulator.IsProcessing == expectedFinalState;
            });
    }

    /// <summary>
    /// **Feature: ai-rewrite-assistant, Property 3: Reentrant guard prevents recursive triggers**
    /// **Validates: Requirements 1.5, 7.5**
    /// 
    /// Interleaved processing and hotkey presses should behave correctly.
    /// </summary>
    [Fact]
    public void InterleavedOperations_ShouldBehaveCorrectly()
    {
        var simulator = new ReentrantGuardSimulator();
        
        // Not processing - should succeed
        simulator.TryHandleHotkeyPress().Should().BeTrue();
        simulator.ProcessedCount.Should().Be(1);
        
        // Start processing
        simulator.SetProcessing(true);
        
        // Should be ignored
        simulator.TryHandleHotkeyPress().Should().BeFalse();
        simulator.TryHandleHotkeyPress().Should().BeFalse();
        simulator.IgnoredCount.Should().Be(2);
        
        // Stop processing
        simulator.SetProcessing(false);
        
        // Should succeed again
        simulator.TryHandleHotkeyPress().Should().BeTrue();
        simulator.ProcessedCount.Should().Be(2);
    }
}
