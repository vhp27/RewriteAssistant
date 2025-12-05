/**
 * Property-based tests for prompt-to-style mapping consistency
 * 
 * **Feature: customizable-configuration, Property 9: Prompt-to-style mapping consistency**
 * **Validates: Requirements 2.6, 3.1**
 * 
 * Property: For any style with an assigned promptId, retrieving the prompt for that style
 * should return the correct prompt text, and using that style for rewrite should send
 * the correct prompt to the backend.
 */

import * as fc from 'fast-check';
import { PromptBuilder, ChatMessage } from './PromptBuilder';
import { DEFAULT_PROMPTS } from '../models/defaults';

describe('Property 9: Prompt-to-style mapping consistency', () => {
  const promptBuilder = new PromptBuilder();

  // All valid prompt IDs from defaults
  const allPromptIds: string[] = DEFAULT_PROMPTS.map(p => p.id);

  // Arbitrary for generating valid prompt IDs
  const promptIdArb = fc.constantFrom(...allPromptIds);

  // Arbitrary for generating non-empty text
  const textArb = fc.string({ minLength: 1, maxLength: 1000 });

  /**
   * **Feature: customizable-configuration, Property 9: Prompt-to-style mapping consistency**
   * **Validates: Requirements 2.6, 3.1**
   * 
   * For any valid promptId, the PromptBuilder should always return
   * a consistent system prompt that matches the prompt configuration.
   */
  it('should consistently map each promptId to its unique system prompt', () => {
    fc.assert(
      fc.property(promptIdArb, (promptId) => {
        // Get the system prompt for this promptId
        const systemPrompt1 = promptBuilder.getSystemPrompt(promptId);
        const systemPrompt2 = promptBuilder.getSystemPrompt(promptId);

        // The same promptId should always produce the same prompt (deterministic)
        expect(systemPrompt1).toBe(systemPrompt2);

        // The prompt should be a non-empty string
        expect(typeof systemPrompt1).toBe('string');
        expect(systemPrompt1.length).toBeGreaterThan(0);

        // The prompt should contain style-specific keywords
        if (promptId.includes('grammar')) {
          expect(systemPrompt1.toLowerCase()).toContain('grammar');
        } else if (promptId.includes('formal')) {
          expect(systemPrompt1.toLowerCase()).toContain('formal');
        } else if (promptId.includes('casual')) {
          expect(systemPrompt1.toLowerCase()).toContain('casual');
        } else if (promptId.includes('shorten')) {
          expect(systemPrompt1.toLowerCase()).toContain('shorten');
        } else if (promptId.includes('expand')) {
          expect(systemPrompt1.toLowerCase()).toContain('expand');
        }
      }),
      { numRuns: 100 }
    );
  });


  /**
   * **Feature: customizable-configuration, Property 9: Prompt-to-style mapping consistency**
   * **Validates: Requirements 2.6, 3.1**
   * 
   * For any promptId and text combination, buildMessages should produce
   * consistent message structure with the correct prompt mapping.
   */
  it('should produce consistent message structure for any promptId and text', () => {
    fc.assert(
      fc.property(promptIdArb, textArb, (promptId, text) => {
        const messages1 = promptBuilder.buildMessages(text, promptId);
        const messages2 = promptBuilder.buildMessages(text, promptId);

        // Should always produce exactly 2 messages (system + user)
        expect(messages1).toHaveLength(2);
        expect(messages2).toHaveLength(2);

        // First message should be system message with prompt-specific text
        expect(messages1[0].role).toBe('system');
        expect(messages1[0].content).toBe(promptBuilder.getSystemPrompt(promptId));

        // Second message should be user message with the input text
        expect(messages1[1].role).toBe('user');
        expect(messages1[1].content).toBe(text);

        // Results should be deterministic
        expect(messages1).toEqual(messages2);
      }),
      { numRuns: 100 }
    );
  });

  /**
   * **Feature: customizable-configuration, Property 9: Prompt-to-style mapping consistency**
   * **Validates: Requirements 2.6, 3.1**
   * 
   * Different promptIds should produce different system prompts (uniqueness).
   */
  it('should produce unique prompts for different promptIds', () => {
    // Collect all system prompts
    const prompts = new Map<string, string>();
    for (const promptId of allPromptIds) {
      prompts.set(promptId, promptBuilder.getSystemPrompt(promptId));
    }

    // Each promptId should have a unique prompt
    const uniquePrompts = new Set(prompts.values());
    expect(uniquePrompts.size).toBe(allPromptIds.length);
  });

  /**
   * **Feature: customizable-configuration, Property 9: Prompt-to-style mapping consistency**
   * **Validates: Requirements 2.6, 3.1**
   * 
   * The promptId-to-prompt mapping should be bijective (one-to-one).
   * Given a prompt, we should be able to identify which promptId it belongs to.
   */
  it('should maintain bijective mapping between promptIds and prompts', () => {
    fc.assert(
      fc.property(promptIdArb, promptIdArb, (promptId1, promptId2) => {
        const prompt1 = promptBuilder.getSystemPrompt(promptId1);
        const prompt2 = promptBuilder.getSystemPrompt(promptId2);

        // If promptIds are the same, prompts must be the same
        if (promptId1 === promptId2) {
          expect(prompt1).toBe(prompt2);
        } else {
          // If promptIds are different, prompts must be different
          expect(prompt1).not.toBe(prompt2);
        }
      }),
      { numRuns: 100 }
    );
  });

  /**
   * **Feature: customizable-configuration, Property 9: Prompt-to-style mapping consistency**
   * **Validates: Requirements 2.6, 3.1**
   * 
   * buildMessagesWithCustomPrompt should correctly use the provided prompt text.
   */
  it('should use custom prompt text when provided', () => {
    fc.assert(
      fc.property(textArb, textArb, (inputText, customPrompt) => {
        const messages = promptBuilder.buildMessagesWithCustomPrompt(inputText, customPrompt);

        // Should always produce exactly 2 messages (system + user)
        expect(messages).toHaveLength(2);

        // First message should be system message with the custom prompt
        expect(messages[0].role).toBe('system');
        expect(messages[0].content).toBe(customPrompt);

        // Second message should be user message with the input text
        expect(messages[1].role).toBe('user');
        expect(messages[1].content).toBe(inputText);
      }),
      { numRuns: 100 }
    );
  });
});
