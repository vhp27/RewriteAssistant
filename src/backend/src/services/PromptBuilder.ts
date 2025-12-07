/**
 * PromptBuilder Service
 * Creates structured messages for Cerebras SDK chat completions
 * Requirements: 2.1, 2.6, 7.2, 10.1
 * 
 * Simplified to be stateless - accepts promptText directly instead of looking up by ID
 */

/**
 * Chat message structure for Cerebras SDK
 */
export interface ChatMessage {
  role: 'system' | 'user';
  content: string;
}

/**
 * Interface for PromptBuilder
 */
export interface IPromptBuilder {
  /**
   * Builds messages with direct prompt text
   * @param text - The text to rewrite
   * @param promptText - The system prompt text to use
   * @returns Array of ChatMessage objects
   */
  buildMessages(text: string, promptText: string): ChatMessage[];
}

/**
 * PromptBuilder implementation
 * Creates structured messages for Cerebras SDK - stateless, uses promptText directly
 */
export class PromptBuilder implements IPromptBuilder {
  /**
   * Builds structured messages for Cerebras SDK chat completions
   * @param text - The text to rewrite
   * @param promptText - The system prompt text to use
   * @returns Array of ChatMessage objects (system + user)
   */
  buildMessages(text: string, promptText: string): ChatMessage[] {
    return [
      { role: 'system', content: promptText },
      { role: 'user', content: text }
    ];
  }
}

// Export singleton instance
export const promptBuilder = new PromptBuilder();
