import type { IRuleModelFieldElement } from '../../types/global';

export interface RuleHandlerGlobals {
  ruleHandlerObject?: object;
  ruleHandlerHelper?: object;
  conditionalRuleHandlerObject?: object;
  conditionalRuleHandlerHelper?: object;
}

// Serialized with .toString() and run inside the sandbox iframe: must stay self-contained
// (no references to imports or other module-scope identifiers).
export function collectRuleModelFields(scope: RuleHandlerGlobals): IRuleModelFieldElement[] {
  const fields: IRuleModelFieldElement[] = [];
  const collect = (
    handlers: object | undefined,
    helpers: object | undefined,
    type: 'rule' | 'condition',
  ) => {
    if (!handlers || !helpers) return;
    Object.keys(handlers).forEach((functionName) => {
      const helper = (helpers as Record<string, unknown>)[functionName];
      if (typeof helper === 'function') {
        fields.push({ name: functionName, inputs: helper(), type });
      }
    });
  };
  collect(scope.ruleHandlerObject, scope.ruleHandlerHelper, 'rule');
  collect(scope.conditionalRuleHandlerObject, scope.conditionalRuleHandlerHelper, 'condition');
  return fields;
}
