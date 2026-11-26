import { parseRuleModel } from './ruleModelUtils';
import ruleHandlerMock, { rule1Name, rule2Name, condition1Name } from '../testing/ruleHandlerMock';

describe('ruleModelUtils', () => {
  describe('parseRuleModel', () => {
    it('Returns rules and conditions defined in the rule handler', () => {
      const result = parseRuleModel(ruleHandlerMock);
      expect(result.filter(({ type }) => type === 'rule').map(({ name }) => name)).toEqual([
        rule1Name,
        rule2Name,
      ]);
      expect(result.find(({ name }) => name === condition1Name)?.type).toBe('condition');
    });

    it('Supports arrow functions, method shorthands, string keys and window assignments', () => {
      const ruleHandler = `
        window.ruleHandlerObject = { 'sum': (obj) => obj.a + obj.b, concat(obj) { return obj.a; } };
        window.ruleHandlerHelper = {
          sum: () => ({ a: 'First', "b": \`Second\` }),
          concat() { return { a: 'Text' }; },
        };
      `;
      expect(parseRuleModel(ruleHandler)).toEqual([
        { name: 'sum', inputs: { a: 'First', b: 'Second' }, type: 'rule' },
        { name: 'concat', inputs: { a: 'Text' }, type: 'rule' },
      ]);
    });

    it('Ignores rules without a helper function', () => {
      const ruleHandler = `
        var ruleHandlerObject = { sum: function(obj) { return obj.a; } };
        var ruleHandlerHelper = { sum: 'not a function' };
      `;
      expect(parseRuleModel(ruleHandler)).toEqual([]);
    });

    it('Ignores input values that are not static strings', () => {
      const ruleHandler = `
        var conditionalRuleHandlerObject = { check: function(obj) { return true; } };
        var conditionalRuleHandlerHelper = {
          check: function() { return { a: 'a', b: getLabel(), c: \`\${1}\` }; },
        };
      `;
      expect(parseRuleModel(ruleHandler)).toEqual([
        { name: 'check', inputs: { a: 'a' }, type: 'condition' },
      ]);
    });

    it('Returns an empty array when the content is not valid JavaScript', () => {
      expect(parseRuleModel('var ruleHandlerObject = {')).toEqual([]);
    });

    it('Does not execute the rule handler content', () => {
      const ruleHandler = `
        window.ruleModelWasExecuted = true;
        var ruleHandlerObject = { sum: function(obj) { return obj.a; } };
        var ruleHandlerHelper = { sum: function() { window.ruleModelWasExecuted = true; return {}; } };
      `;
      parseRuleModel(ruleHandler);
      expect(window).not.toHaveProperty('ruleModelWasExecuted');
    });
  });
});
