/**
 * PromptStore Service
 * Manages custom prompts with fast lookup and fallback to defaults
 * Requirements: 2.6, 4.2
 */

import { CustomPrompt } from '../models/types';
import { DEFAULT_PROMPTS, getDefaultPromptText } from '../models/defaults';

/**
 * Interface for PromptStore
 */
export interface IPromptStore {
  /**
   * Sets the prompts in the store, replacing any existing prompts
   * @param prompts - Array of custom prompts to store
   */
  setPrompts(prompts: CustomPrompt[]): void;

  /**
   * Gets a prompt by ID
   * @param promptId - The prompt ID to look up
   * @returns The CustomPrompt or null if not found
   */
  getPrompt(promptId: string): CustomPrompt | null;

  /**
   * Gets the prompt text by ID, with fallback to defaults
   * @param promptId - The prompt ID to look up
   * @returns The prompt text string
   */
  getPromptText(promptId: string): string;

  /**
   * Gets all prompts currently in the store
   * @returns Array of all CustomPrompt objects
   */
  getAllPrompts(): CustomPrompt[];

  /**
   * Checks if a prompt exists in the store
   * @param promptId - The prompt ID to check
   * @returns True if the prompt exists
   */
  hasPrompt(promptId: string): boolean;
}

/**
 * Default fallback prompt text when no prompt is found
 */
const FALLBACK_PROMPT_TEXT = 'You are a helpful text editor. Return ONLY the edited text without any explanations, comments, or formatting.';

/**
 * PromptStore implementation
 * Stores prompts in a Map for O(1) lookup with fallback to default prompts
 */
export class PromptStore implements IPromptStore {
  private prompts: Map<string, CustomPrompt> = new Map();
  private defaultPromptMap: Map<string, CustomPrompt>;

  constructor() {
    // Initialize with default prompts
    this.defaultPromptMap = new Map(
      DEFAULT_PROMPTS.map(p => [p.id, p])
    );
    // Start with defaults loaded
    this.loadDefaults();
  }

  /**
   * Loads default prompts into the store
   */
  private loadDefaults(): void {
    for (const prompt of DEFAULT_PROMPTS) {
      this.prompts.set(prompt.id, { ...prompt });
    }
  }

  /**
   * Sets the prompts in the store, replacing any existing prompts
   * Merges with defaults - custom prompts override defaults with same ID
   */
  setPrompts(prompts: CustomPrompt[]): void {
    this.prompts.clear();
    
    // First load defaults
    this.loadDefaults();
    
    // Then overlay custom prompts (they can override defaults)
    for (const prompt of prompts) {
      this.prompts.set(prompt.id, { ...prompt });
    }
  }

  /**
   * Gets a prompt by ID
   * Falls back to default prompts if not found in custom prompts
   */
  getPrompt(promptId: string): CustomPrompt | null {
    return this.prompts.get(promptId) ?? this.defaultPromptMap.get(promptId) ?? null;
  }

  /**
   * Gets the prompt text by ID
   * Falls back to default prompts, then to a generic fallback
   */
  getPromptText(promptId: string): string {
    // First check custom prompts
    const customPrompt = this.prompts.get(promptId);
    if (customPrompt) {
      return customPrompt.promptText;
    }

    // Then check default prompts
    const defaultText = getDefaultPromptText(promptId);
    if (defaultText) {
      return defaultText;
    }

    // Final fallback
    return FALLBACK_PROMPT_TEXT;
  }

  /**
   * Gets all prompts currently in the store
   */
  getAllPrompts(): CustomPrompt[] {
    return Array.from(this.prompts.values());
  }

  /**
   * Checks if a prompt exists in the store or defaults
   */
  hasPrompt(promptId: string): boolean {
    return this.prompts.has(promptId) || this.defaultPromptMap.has(promptId);
  }
}

// Export singleton instance
export const promptStore = new PromptStore();
