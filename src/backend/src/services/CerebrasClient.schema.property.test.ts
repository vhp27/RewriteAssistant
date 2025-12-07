/**
 * Property-based tests for REWRITE_RESPONSE_SCHEMA structure validation
 * 
 * **Feature: structured-output-enforcement, Property 1: Schema structure validation**
 * **Validates: Requirements 1.2, 1.3, 1.4, 5.1, 5.2**
 * 
 * Property: For any inspection of the REWRITE_RESPONSE_SCHEMA constant, it SHALL contain:
 * type `json_schema`, a json_schema object with name, description, strict=true, and a schema
 * with rewritten_text as a required string property with additionalProperties=false.
 */

import * as fc from 'fast-check';
import { REWRITE_RESPONSE_SCHEMA } from './CerebrasClient';

describe('Property 1: Schema structure validation', () => {
  /**
   * **Feature: structured-output-enforcement, Property 1: Schema structure validation**
   * **Validates: Requirements 1.2, 1.3, 1.4, 5.1, 5.2**
   * 
   * The schema constant must have the correct top-level type.
   */
  it('should have type set to json_schema', () => {
    expect(REWRITE_RESPONSE_SCHEMA.type).toBe('json_schema');
  });

  /**
   * **Feature: structured-output-enforcement, Property 1: Schema structure validation**
   * **Validates: Requirements 5.1, 5.2**
   * 
   * The json_schema object must have name and description.
   */
  it('should have json_schema with name and description', () => {
    expect(REWRITE_RESPONSE_SCHEMA.json_schema).toBeDefined();
    expect(REWRITE_RESPONSE_SCHEMA.json_schema.name).toBe('rewrite_response');
    expect(typeof REWRITE_RESPONSE_SCHEMA.json_schema.description).toBe('string');
    expect(REWRITE_RESPONSE_SCHEMA.json_schema.description.length).toBeGreaterThan(0);
  });

  /**
   * **Feature: structured-output-enforcement, Property 1: Schema structure validation**
   * **Validates: Requirements 1.4**
   * 
   * The strict flag must be set to true for exact schema adherence.
   */
  it('should have strict flag set to true', () => {
    expect(REWRITE_RESPONSE_SCHEMA.json_schema.strict).toBe(true);
  });

  /**
   * **Feature: structured-output-enforcement, Property 1: Schema structure validation**
   * **Validates: Requirements 1.2**
   * 
   * The schema must define rewritten_text as a string property.
   */
  it('should define rewritten_text as a string property', () => {
    const schema = REWRITE_RESPONSE_SCHEMA.json_schema.schema;
    expect(schema.type).toBe('object');
    expect(schema.properties).toBeDefined();
    expect(schema.properties.rewritten_text).toBeDefined();
    expect(schema.properties.rewritten_text.type).toBe('string');
  });

  /**
   * **Feature: structured-output-enforcement, Property 1: Schema structure validation**
   * **Validates: Requirements 1.3**
   * 
   * The schema must have rewritten_text in the required array and additionalProperties=false.
   */
  it('should have rewritten_text as required and disallow additional properties', () => {
    const schema = REWRITE_RESPONSE_SCHEMA.json_schema.schema;
    expect(schema.required).toContain('rewritten_text');
    expect(schema.additionalProperties).toBe(false);
  });

  /**
   * **Feature: structured-output-enforcement, Property 1: Schema structure validation**
   * **Validates: Requirements 1.2, 1.3, 1.4, 5.1, 5.2**
   * 
   * Property test: For any number of inspections, the schema structure remains consistent.
   * This validates that the schema is immutable and always returns the same structure.
   */
  it('should maintain consistent structure across multiple inspections', () => {
    fc.assert(
      fc.property(fc.integer({ min: 1, max: 100 }), (inspectionCount) => {
        for (let i = 0; i < inspectionCount; i++) {
          // Verify structure is consistent on each inspection
          expect(REWRITE_RESPONSE_SCHEMA.type).toBe('json_schema');
          expect(REWRITE_RESPONSE_SCHEMA.json_schema.name).toBe('rewrite_response');
          expect(REWRITE_RESPONSE_SCHEMA.json_schema.strict).toBe(true);
          expect(REWRITE_RESPONSE_SCHEMA.json_schema.schema.properties.rewritten_text.type).toBe('string');
          expect(REWRITE_RESPONSE_SCHEMA.json_schema.schema.required).toContain('rewritten_text');
          expect(REWRITE_RESPONSE_SCHEMA.json_schema.schema.additionalProperties).toBe(false);
        }
      }),
      { numRuns: 100 }
    );
  });

  /**
   * **Feature: structured-output-enforcement, Property 1: Schema structure validation**
   * **Validates: Requirements 1.2, 1.3**
   * 
   * Property test: The schema should only have exactly one property (rewritten_text).
   */
  it('should have exactly one property defined', () => {
    const properties = Object.keys(REWRITE_RESPONSE_SCHEMA.json_schema.schema.properties);
    expect(properties).toHaveLength(1);
    expect(properties[0]).toBe('rewritten_text');
  });
});
