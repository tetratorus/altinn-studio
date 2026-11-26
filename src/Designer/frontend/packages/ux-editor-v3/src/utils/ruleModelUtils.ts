import { parse } from 'acorn';
import type { Expression, Node, ObjectExpression, Property, Statement } from 'acorn';
import type { IRuleModelFieldElement } from '../types/global';

type RuleModelFieldType = IRuleModelFieldElement['type'];
type RuleModelInputs = Record<string, string>;

interface RuleHandlerDefinition {
  handlerObjectName: string;
  helperObjectName: string;
  type: RuleModelFieldType;
}

const ruleHandlerDefinitions: RuleHandlerDefinition[] = [
  { handlerObjectName: 'ruleHandlerObject', helperObjectName: 'ruleHandlerHelper', type: 'rule' },
  {
    handlerObjectName: 'conditionalRuleHandlerObject',
    helperObjectName: 'conditionalRuleHandlerHelper',
    type: 'condition',
  },
];

/**
 * Extracts the rule and condition definitions from the content of a RuleHandler.js file.
 * The file is parsed statically and never executed, since it is supplied by the app repository.
 */
export function parseRuleModel(ruleHandlerContent: string): IRuleModelFieldElement[] {
  const topLevelObjects = findTopLevelObjects(ruleHandlerContent);
  return ruleHandlerDefinitions.flatMap((definition) =>
    getRuleModelFields(topLevelObjects, definition),
  );
}

function findTopLevelObjects(ruleHandlerContent: string): Map<string, ObjectExpression> {
  const topLevelObjects = new Map<string, ObjectExpression>();
  let statements: Statement[];
  try {
    const program = parse(ruleHandlerContent, { ecmaVersion: 'latest', sourceType: 'script' });
    statements = program.body as Statement[];
  } catch {
    return topLevelObjects;
  }

  statements.forEach((statement) => {
    if (statement.type === 'VariableDeclaration') {
      statement.declarations.forEach((declarator) => {
        if (declarator.id.type === 'Identifier' && declarator.init?.type === 'ObjectExpression') {
          topLevelObjects.set(declarator.id.name, declarator.init);
        }
      });
    } else if (
      statement.type === 'ExpressionStatement' &&
      statement.expression.type === 'AssignmentExpression' &&
      statement.expression.operator === '=' &&
      statement.expression.right.type === 'ObjectExpression'
    ) {
      const name = getAssignmentTargetName(statement.expression.left);
      if (name) topLevelObjects.set(name, statement.expression.right);
    }
  });
  return topLevelObjects;
}

function getAssignmentTargetName(target: Node): string | undefined {
  if (target.type === 'Identifier') return (target as Expression & { name: string }).name;
  if (target.type === 'MemberExpression') {
    const { object, property, computed } = target as Extract<
      Expression,
      { type: 'MemberExpression' }
    >;
    if (object.type === 'Identifier' && object.name === 'window' && !computed) {
      return property.type === 'Identifier' ? property.name : undefined;
    }
  }
  return undefined;
}

function getRuleModelFields(
  topLevelObjects: Map<string, ObjectExpression>,
  { handlerObjectName, helperObjectName, type }: RuleHandlerDefinition,
): IRuleModelFieldElement[] {
  const handlerObject = topLevelObjects.get(handlerObjectName);
  if (!handlerObject) return [];
  const helperFunctions = getHelperFunctions(topLevelObjects.get(helperObjectName));

  return getProperties(handlerObject)
    .map(getPropertyName)
    .filter((name) => name !== undefined && helperFunctions.has(name))
    .map((name) => ({ name, inputs: getHelperInputs(helperFunctions.get(name)), type }));
}

function getHelperFunctions(helperObject?: ObjectExpression): Map<string, Expression> {
  const helperFunctions = new Map<string, Expression>();
  if (!helperObject) return helperFunctions;
  getProperties(helperObject).forEach((property) => {
    const name = getPropertyName(property);
    if (name !== undefined && isFunction(property.value)) helperFunctions.set(name, property.value);
  });
  return helperFunctions;
}

function getProperties(objectExpression: ObjectExpression): Property[] {
  return objectExpression.properties.filter(
    (property): property is Property => property.type === 'Property' && property.kind === 'init',
  );
}

function getPropertyName(property: Property): string | undefined {
  const { key, computed } = property;
  if (key.type === 'Identifier' && !computed) return key.name;
  if (key.type === 'Literal' && (typeof key.value === 'string' || typeof key.value === 'number')) {
    return String(key.value);
  }
  return undefined;
}

function isFunction(expression: Expression): boolean {
  return expression.type === 'FunctionExpression' || expression.type === 'ArrowFunctionExpression';
}

function getHelperInputs(helperFunction: Expression): RuleModelInputs {
  const returnedObject = getReturnedObject(helperFunction);
  return returnedObject ? getStringValues(returnedObject) : {};
}

function getReturnedObject(helperFunction: Expression): ObjectExpression | undefined {
  if (
    helperFunction.type !== 'FunctionExpression' &&
    helperFunction.type !== 'ArrowFunctionExpression'
  ) {
    return undefined;
  }
  const { body } = helperFunction;
  if (body.type === 'ObjectExpression') return body;
  if (body.type !== 'BlockStatement') return undefined;

  const bodyStatements = body.body as unknown as Statement[];
  const returnStatement = bodyStatements.find((statement) => statement.type === 'ReturnStatement');
  if (
    returnStatement?.type === 'ReturnStatement' &&
    returnStatement.argument?.type === 'ObjectExpression'
  ) {
    return returnStatement.argument;
  }
  return undefined;
}

function getStringValues(objectExpression: ObjectExpression): RuleModelInputs {
  const values: RuleModelInputs = {};
  getProperties(objectExpression).forEach((property) => {
    const name = getPropertyName(property);
    const value = getStringValue(property.value);
    if (name !== undefined && value !== undefined) values[name] = value;
  });
  return values;
}

function getStringValue(expression: Expression): string | undefined {
  if (expression.type === 'Literal' && typeof expression.value === 'string')
    return expression.value;
  if (expression.type === 'TemplateLiteral' && expression.expressions.length === 0) {
    return expression.quasis[0]?.value.cooked ?? undefined;
  }
  return undefined;
}
