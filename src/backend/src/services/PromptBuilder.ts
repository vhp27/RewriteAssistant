/**
 * PromptBuilder Service
 * Creates structured messages for Cerebras SDK chat completions
 * Requirements: 2.1, 2.6, 7.2, 10.1
 */

import { IPromptStore, promptStore } from './PromptStore';

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
   * Builds messages using a prompt ID
   * @param text - The text to rewrite
   * @param promptId - The prompt ID to use
   * @returns Array of ChatMessage objects
   */
  buildMessages(text: string, promptId: string): ChatMessage[];

  /**
   * Builds messages with direct prompt text (for custom prompts)
   * @param text - The text to rewrite
   * @param promptText - The system prompt text to use
   * @returns Array of ChatMessage objects
   */
  buildMessagesWithCustomPrompt(text: string, promptText: string): ChatMessage[];

  /**
   * Gets the system prompt text for a prompt ID
   * @param promptId - The prompt ID to look up
   * @returns The system prompt string
   */
  getSystemPrompt(promptId: string): string;
}

/**
 * PromptBuilder implementation
 * Creates structured messages for Cerebras SDK using PromptStore
 */
export class PromptBuilder implements IPromptBuilder {
  private promptStore: IPromptStore;

  constructor(store?: IPromptStore) {
    this.promptStore = store ?? promptStore;
  }

  /**
   * Builds structured messages for Cerebras SDK chat completions
   * @param text - The text to rewrite
   * @param promptId - The prompt ID to use
   * @returns Array of ChatMessage objects (system + user)
   */
  buildMessages(text: string, promptId: string): ChatMessage[] {
    const promptText = this.promptStore.getPromptText(promptId);
    return this.buildMessagesWithCustomPrompt(text, promptText);
  }

  /**
   * Builds messages with direct prompt text
   * Useful for custom prompts or when prompt text is already known
   * @param text - The text to rewrite
   * @param promptText - The system prompt text to use
   * @returns Array of ChatMessage objects (system + user)
   */
  buildMessagesWithCustomPrompt(text: string, promptText: string): ChatMessage[] {
    return [
      { role: 'system', content: promptText },
      { role: 'user', content: text }
    ];
  }

  /**
   * Gets the system prompt text for a prompt ID
   * @param promptId - The prompt ID to look up
   * @returns The system prompt string
   */
  getSystemPrompt(promptId: string): string {
    return this.promptStore.getPromptText(promptId);
  }
}

// Export singleton instance
export const promptBuilder = new PromptBuilder();
