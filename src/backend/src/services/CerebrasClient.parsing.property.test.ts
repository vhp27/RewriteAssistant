/**
 * Property-based tests for parseRewriteResponse function
 * 
 * Tests for Properties 2 and 3 from the structured-output-enforcement spec
 */

import * as fc from 'fast-check';
import { parseRewriteResponse } from './CerebrasClient';

describe('Property 2: JSON response parsing extracts rewritten_text', () => {
  /**
   * **Feature: structured-output-enforcement, Property 2: JSON response parsing extracts rewritten_text**
   * **Validates: Requirements 1.5**
   * 
   * For any valid JSON string containing a `rewritten_text` field,
   * parsing the response SHALL return exactly the value of that field with no additional content.
   */
  it('should extract rewritten_text from valid JSON responses', () => {
    fc.assert(
      fc.property(fc.string(), (text) => {
        const jsonContent = JSON.stringify({ rewritten_text: text });
        const result = parseRewriteResponse(jsonContent);
        expect(result).toBe(text);
      }),
      { numRuns: 100 }
    );
  });

  /**
   * **Feature: structured-output-enforcement, Property 2: JSON response parsing extracts rewritten_text**
   * **Validates: Requirements 1.5**
   * 
   * For any valid JSON with rewritten_text and additional properties,
   * parsing should still extract only the rewritten_text value.
   */
  it('should extract rewritten_text even when additional properties exist', () => {
    fc.assert(
      fc.property(
        fc.string(),
        fc.dictionary(fc.string().filter(s => s !== 'rewritten_text' && s.length > 0), fc.jsonValue()),
        (text, extraProps) => {
          const jsonContent = JSON.stringify({ rewritten_text: text, ...extraProps });
          const result = parseRewriteResponse(jsonContent);
          expect(result).toBe(text);
        }
      ),
      { numRuns: 100 }
    );
  });

  /**
   * **Feature: structured-output-enforcement, Property 2: JSON response parsing extracts rewritten_text**
   * **Validates: Requirements 1.5**
   * 
   * Empty string is a valid rewritten_text value.
   */
  it('should handle empty string as valid rewritten_text', () => {
    const jsonContent = JSON.stringify({ rewritten_text: '' });
    const result = parseRewriteResponse(jsonContent);
    expect(result).toBe('');
  });

  /**
   * **Feature: structured-output-enforcement, Property 2: JSON response parsing extracts rewritten_text**
   * **Validates: Requirements 1.5**
   * 
   * Unicode and special characters should be preserved.
   */
  it('should preserve unicode and special characters in rewritten_text', () => {
    fc.assert(
      fc.property(fc.fullUnicodeString(), (text) => {
        const jsonContent = JSON.stringify({ rewritten_text: text });
        const result = parseRewriteResponse(jsonContent);
        expect(result).toBe(text);
      }),
      { numRuns: 100 }
    );
  });
});

describe('Property 3: Invalid JSON fallback behavior', () => {
  /**
   * **Feature: structured-output-enforcement, Property 3: Invalid JSON fallback behavior**
   * **Validates: Requirements 1.6**
   * 
   * For any string that is not valid JSON, the parser SHALL return the original string content as fallback.
   */
  it('should return raw content for invalid JSON strings', () => {
    fc.assert(
      fc.property(
        fc.string().filter(s => {
          try {
            JSON.parse(s);
            return false; // Valid JSON, skip
          } catch {
            return true; // Invalid JSON, include
          }
        }),
        (invalidJson) => {
          const result = parseRewriteResponse(invalidJson);
          expect(result).toBe(invalidJson);
        }
      ),
      { numRuns: 100 }
    );
  });

  /**
   * **Feature: structured-output-enforcement, Property 3: Invalid JSON fallback behavior**
   * **Validates: Requirements 1.6**
   * 
   * For valid JSON that does not contain a rewritten_text field,
   * the parser SHALL return the original string content as fallback.
   */
  it('should return raw content when rewritten_text field is missing', () => {
    fc.assert(
      fc.property(
        fc.dictionary(
          fc.string().filter(s => s !== 'rewritten_text' && s.length > 0),
          fc.jsonValue()
        ),
        (obj) => {
          const jsonContent = JSON.stringify(obj);
          const result = parseRewriteResponse(jsonContent);
          expect(result).toBe(jsonContent);
        }
      ),
      { numRuns: 100 }
    );
  });

  /**
   * **Feature: structured-output-enforcement, Property 3: Invalid JSON fallback behavior**
   * **Validates: Requirements 1.6**
   * 
   * For JSON where rewritten_text is not a string, return raw content as fallback.
   */
  it('should return raw content when rewritten_text is not a string', () => {
    const nonStringValues = [123, true, false, null, [], {}];
    
    for (const value of nonStringValues) {
      const jsonContent = JSON.stringify({ rewritten_text: value });
      const result = parseRewriteResponse(jsonContent);
      expect(result).toBe(jsonContent);
    }
  });

  /**
   * **Feature: structured-output-enforcement, Property 3: Invalid JSON fallback behavior**
   * **Validates: Requirements 1.6**
   * 
   * Plain text (non-JSON) should be returned as-is.
   */
  it('should return plain text as-is', () => {
    fc.assert(
      fc.property(
        fc.string().filter(s => !s.startsWith('{') && !s.startsWith('[')),
        (plainText) => {
          const result = parseRewriteResponse(plainText);
          expect(result).toBe(plainText);
        }
      ),
      { numRuns: 100 }
    );
  });
});
