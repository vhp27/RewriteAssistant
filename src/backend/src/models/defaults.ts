/**
 * Default built-in prompts and styles
 * These serve as the single source of truth for default configurations
 * Requirements: 7.4
 */

import { CustomPrompt, CustomStyle, HotkeyConfig } from './types';

/**
 * Default built-in prompts
 */
export const DEFAULT_PROMPTS: CustomPrompt[] = [
  {
    id: 'grammar_fix_prompt',
    name: 'Grammar Fix',
    promptText: 'You are a text editor that fixes grammar and spelling errors. Preserve the original meaning and tone. Return ONLY the corrected text without any explanations, comments, or formatting.',
    isBuiltIn: true
  },
  {
    id: 'formal_tone_prompt',
    name: 'Formal Tone',
    promptText: 'You are a text editor that rewrites text in a formal, professional tone. Return ONLY the rewritten text without any explanations, comments, or formatting.',
    isBuiltIn: true
  },
  {
    id: 'casual_tone_prompt',
    name: 'Casual Tone',
    promptText: 'You are a text editor that rewrites text in a casual, friendly tone. Return ONLY the rewritten text without any explanations, comments, or formatting.',
    isBuiltIn: true
  },
  {
    id: 'shorten_text_prompt',
    name: 'Shorten Text',
    promptText: 'You are a text editor that shortens text while preserving the key message. Return ONLY the shortened text without any explanations, comments, or formatting.',
    isBuiltIn: true
  },
  {
    id: 'expand_text_prompt',
    name: 'Expand Text',
    promptText: 'You are a text editor that expands text with more detail and clarity. Return ONLY the expanded text without any explanations, comments, or formatting.',
    isBuiltIn: true
  }
];

/**
 * Default built-in styles linking to prompts
 */
export const DEFAULT_STYLES: CustomStyle[] = [
  {
    id: 'grammar_fix',
    name: 'Grammar Fix',
    promptId: 'grammar_fix_prompt',
    hotkey: {
      id: 'grammar_fix',
      modifiers: ['ctrl', 'shift'],
      key: 'G'
    },
    isBuiltIn: true
  },
  {
    id: 'formal_tone',
    name: 'Formal Tone',
    promptId: 'formal_tone_prompt',
    hotkey: {
      id: 'formal_tone',
      modifiers: ['ctrl', 'shift'],
      key: 'F'
    },
    isBuiltIn: true
  },
  {
    id: 'casual_tone',
    name: 'Casual Tone',
    promptId: 'casual_tone_prompt',
    hotkey: {
      id: 'casual_tone',
      modifiers: ['ctrl', 'shift'],
      key: 'C'
    },
    isBuiltIn: true
  },
  {
    id: 'shorten_text',
    name: 'Shorten Text',
    promptId: 'shorten_text_prompt',
    isBuiltIn: true
  },
  {
    id: 'expand_text',
    name: 'Expand Text',
    promptId: 'expand_text_prompt',
    isBuiltIn: true
  }
];

/**
 * Map of prompt IDs to prompt text for quick lookup
 */
export const DEFAULT_PROMPT_MAP: Map<string, string> = new Map(
  DEFAULT_PROMPTS.map(p => [p.id, p.promptText])
);

/**
 * Get default prompt text by ID
 * @param promptId - The prompt ID to look up
 * @returns The prompt text or undefined if not found
 */
export function getDefaultPromptText(promptId: string): string | undefined {
  return DEFAULT_PROMPT_MAP.get(promptId);
}
