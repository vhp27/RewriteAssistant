/**
 * Property-based tests for PromptBuilder
 * 
 * **Feature: architecture-hardening, Property 1: Backend uses promptText from request**
 * **Validates: Requirements 1.1, 1.5**
 * 
 * Property: For any promptText provided, the PromptBuilder should create
 * consistent message structures using that exact promptText.
 */

import * as fc from 'fast-check';
import { PromptBuilder, ChatMessage } from './PromptBuilder';

describe('Property 1: Backend uses promptText from request', () => {
  const promptBuilder = new PromptBuilder();

  // Arbitrary for generating non-empty prompt text
  const promptTextArb = fc.string({ minLength: 1, maxLength: 1000 });

  // Arbitrary for generating non-empty user text
  const textArb = fc.string({ minLength: 1, maxLength: 1000 });

  /**
   * **Feature: architecture-hardening, Property 1: Backend uses promptText from request**
   * **Validates: Requirements 1.1, 1.5**
   * 
   * For any promptText and text combination, buildMessages should produce
   * consistent message structure with the exact promptText provided.
   */
  it('should use exactly the promptText provided in the request', () => {
    fc.assert(
      fc.property(textArb, promptTextArb, (text, promptText) => {
        const messages = promptBuilder.buildMessages(text, promptText);

        // Should always produce exactly 2 messages (system + user)
        expect(messages).toHaveLength(2);

        // First message should be system message with the exact promptText
        expect(messages[0].role).toBe('system');
        expect(messages[0].content).toBe(promptText);

        // Second message should be user message with the input text
        expect(messages[1].role).toBe('user');
        expect(messages[1].content).toBe(text);
      }),
      { numRuns: 100 }
    );
  });

  /**
   * **Feature: architecture-hardening, Property 1: Backend uses promptText from request**
   * **Validates: Requirements 1.1, 1.5**
   * 
   * The same inputs should always produce the same outputs (deterministic).
   */
  it('should produce deterministic results for the same inputs', () => {
    fc.assert(
      fc.property(textArb, promptTextArb, (text, promptText) => {
        const messages1 = promptBuilder.buildMessages(text, promptText);
        const messages2 = promptBuilder.buildMessages(text, promptText);

        // Results should be identical
        expect(messages1).toEqual(messages2);
      }),
      { numRuns: 100 }
    );
  });

  /**
   * **Feature: architecture-hardening, Property 1: Backend uses promptText from request**
   * **Validates: Requirements 1.1, 1.5**
   * 
   * Different promptText values should produce different system messages.
   */
  it('should produce different system messages for different promptText values', () => {
    fc.assert(
      fc.property(textArb, promptTextArb, promptTextArb, (text, promptText1, promptText2) => {
        // Skip if prompts are the same
        fc.pre(promptText1 !== promptText2);

        const messages1 = promptBuilder.buildMessages(text, promptText1);
        const messages2 = promptBuilder.buildMessages(text, promptText2);

        // System messages should be different
        expect(messages1[0].content).not.toBe(messages2[0].content);
        
        // User messages should be the same (same input text)
        expect(messages1[1].content).toBe(messages2[1].content);
      }),
      { numRuns: 100 }
    );
  });

  /**
   * **Feature: architecture-hardening, Property 1: Backend uses promptText from request**
   * **Validates: Requirements 1.1, 1.5**
   * 
   * The message structure should always be valid for Cerebras SDK.
   */
  it('should produce valid message structure for Cerebras SDK', () => {
    fc.assert(
      fc.property(textArb, promptTextArb, (text, promptText) => {
        const messages = promptBuilder.buildMessages(text, promptText);

        // Validate structure
        expect(Array.isArray(messages)).toBe(true);
        expect(messages.length).toBe(2);

        // Validate first message (system)
        expect(messages[0]).toHaveProperty('role');
        expect(messages[0]).toHaveProperty('content');
        expect(messages[0].role).toBe('system');
        expect(typeof messages[0].content).toBe('string');

        // Validate second message (user)
        expect(messages[1]).toHaveProperty('role');
        expect(messages[1]).toHaveProperty('content');
        expect(messages[1].role).toBe('user');
        expect(typeof messages[1].content).toBe('string');
      }),
      { numRuns: 100 }
    );
  });
});
