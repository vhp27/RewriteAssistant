using FluentAssertions;
using FsCheck;
using FsCheck.Xunit;
using RewriteAssistant.Models;
using Xunit;

namespace RewriteAssistant.Tests;

/// <summary>
/// Property-based tests for default prompt compliance
/// 
/// **Feature: structured-output-enforcement, Property 5: Default prompts comply with format requirements**
/// **Validates: Requirements 4.1, 4.2**
/// 
/// Property: For any default prompt in the system, the prompt text SHALL NOT contain phrases 
/// like "Return ONLY", "without any explanations", or "without any comments", AND SHALL 
/// contain reference to JSON format output.
/// </summary>
public class DefaultPromptsPropertyTests
{
    /// <summary>
    /// Phrases that should NOT appear in prompts (negative constraints)
    /// </summary>
    private static readonly string[] ForbiddenPhrases = new[]
    {
        "Return ONLY",
        "return ONLY",
        "RETURN ONLY",
        "without any explanations",
        "without any comments",
        "without explanations",
        "without comments"
    };

    /// <summary>
    /// Phrases that MUST appear in prompts (JSON format reference)
    /// </summary>
    private static readonly string[] RequiredPhrases = new[]
    {
        "JSON format"
    };

    /// <summary>
    /// **Feature: structured-output-enforcement, Property 5: Default prompts comply with format requirements**
    /// **Validates: Requirements 4.1, 4.2**
    /// 
    /// For any default prompt, it should not contain forbidden negative constraint phrases.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property DefaultPrompts_ShouldNotContainForbiddenPhrases()
    {
        var defaultConfig = AppConfiguration.CreateDefault();
        var prompts = defaultConfig.Prompts;

        return Prop.ForAll(
            Gen.Elements(prompts.ToArray()).ToArbitrary(),
            prompt =>
            {
                var promptText = prompt.PromptText;
                
                foreach (var forbidden in ForbiddenPhrases)
                {
                    if (promptText.Contains(forbidden, StringComparison.OrdinalIgnoreCase))
                    {
                        return false;
                    }
                }
                
                return true;
            });
    }

    /// <summary>
    /// **Feature: structured-output-enforcement, Property 5: Default prompts comply with format requirements**
    /// **Validates: Requirements 4.1, 4.2**
    /// 
    /// For any default prompt, it should contain reference to JSON format output.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property DefaultPrompts_ShouldContainJsonFormatReference()
    {
        var defaultConfig = AppConfiguration.CreateDefault();
        var prompts = defaultConfig.Prompts;

        return Prop.ForAll(
            Gen.Elements(prompts.ToArray()).ToArbitrary(),
            prompt =>
            {
                var promptText = prompt.PromptText;
                
                foreach (var required in RequiredPhrases)
                {
                    if (promptText.Contains(required, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
                
                return false;
            });
    }

    /// <summary>
    /// **Feature: structured-output-enforcement, Property 5: Default prompts comply with format requirements**
    /// **Validates: Requirements 4.1, 4.2**
    /// 
    /// All default prompts should comply with both requirements simultaneously.
    /// </summary>
    [Fact]
    public void AllDefaultPrompts_ShouldComplyWithFormatRequirements()
    {
        var defaultConfig = AppConfiguration.CreateDefault();
        
        foreach (var prompt in defaultConfig.Prompts)
        {
            // Check no forbidden phrases
            foreach (var forbidden in ForbiddenPhrases)
            {
                prompt.PromptText.Should().NotContain(forbidden,
                    $"Prompt '{prompt.Name}' should not contain forbidden phrase '{forbidden}'");
            }
            
            // Check contains JSON format reference
            var containsJsonReference = RequiredPhrases.Any(required => 
                prompt.PromptText.Contains(required, StringComparison.OrdinalIgnoreCase));
            
            containsJsonReference.Should().BeTrue(
                $"Prompt '{prompt.Name}' should contain reference to JSON format");
        }
    }

    /// <summary>
    /// **Feature: structured-output-enforcement, Property 5: Default prompts comply with format requirements**
    /// **Validates: Requirements 4.1, 4.2**
    /// 
    /// Default prompts should focus on transformation action (have meaningful content).
    /// </summary>
    [Fact]
    public void AllDefaultPrompts_ShouldFocusOnTransformationAction()
    {
        var defaultConfig = AppConfiguration.CreateDefault();
        
        foreach (var prompt in defaultConfig.Prompts)
        {
            // Each prompt should describe a transformation action
            prompt.PromptText.Should().Contain("text editor",
                $"Prompt '{prompt.Name}' should describe the text editor role");
            
            // Prompt should not be empty or too short
            prompt.PromptText.Length.Should().BeGreaterThan(50,
                $"Prompt '{prompt.Name}' should have meaningful content");
        }
    }
}
