import {
  escapeForInlineScript,
  extractRuleModelFields,
  sanitizeFields,
} from './extractRuleModelFields';

describe('sanitizeFields', () => {
  it('drops invalid field and input values', () => {
    expect(
      sanitizeFields([
        { name: 'valid', type: 'rule', inputs: { valid: 'value', number: 1 } },
        { name: 1, type: 'rule', inputs: {} },
        { name: 'bad-type', type: 'other', inputs: {} },
        { name: 'array-inputs', type: 'rule', inputs: [] },
        { name: 'null-inputs', type: 'rule', inputs: null },
        { name: 'not-object-inputs', type: 'rule', inputs: 'value' },
        { name: 'condition', type: 'condition', inputs: { valid: 'value' } },
        null,
      ]),
    ).toEqual([
      { name: 'valid', type: 'rule', inputs: { valid: 'value' } },
      { name: 'condition', type: 'condition', inputs: { valid: 'value' } },
    ]);
  });

  it('returns an empty array for a non-array', () => {
    expect(sanitizeFields({ name: 'field' })).toEqual([]);
  });
});

describe('escapeForInlineScript', () => {
  it('escapes script tags and HTML comments', () => {
    expect(escapeForInlineScript('</script><!-- code')).toBe('<\\/script><\\!-- code');
  });
});

describe('extractRuleModelFields', () => {
  it('appends a sandboxed iframe and resolves matching messages', async () => {
    jest.useFakeTimers();
    const ruleModel =
      'var ruleHandlerObject = { field: function () {} }; var ruleHandlerHelper = { field: function () { return { label: "Label" }; } };';
    const promise = extractRuleModelFields(ruleModel);
    const iframe = document.querySelector('iframe');

    expect(iframe).toHaveAttribute('sandbox', 'allow-scripts');
    expect(iframe?.srcdoc).toContain(ruleModel);

    if (!iframe?.contentWindow) {
      promise.catch(() => undefined);
      jest.advanceTimersByTime(5000);
      jest.useRealTimers();
      return;
    }

    const requestId = iframe.srcdoc.match(/requestId: "([^"]+)"/)?.[1];
    expect(requestId).toBeDefined();
    window.dispatchEvent(
      new MessageEvent('message', {
        data: {
          type: 'ruleModelFields',
          requestId,
          fields: [{ name: 'field', type: 'rule', inputs: { label: 'Label' } }],
        },
        source: iframe.contentWindow,
      }),
    );

    await expect(promise).resolves.toEqual([
      { name: 'field', type: 'rule', inputs: { label: 'Label' } },
    ]);
    jest.useRealTimers();
  });

  it('ignores messages with a wrong request ID', async () => {
    jest.useFakeTimers();
    const promise = extractRuleModelFields('var ruleHandlerObject = {};');
    const iframe = document.querySelector('iframe');

    if (!iframe?.contentWindow) {
      jest.advanceTimersByTime(5000);
      jest.useRealTimers();
      return;
    }

    window.dispatchEvent(
      new MessageEvent('message', {
        data: {
          type: 'ruleModelFields',
          requestId: 'wrong-request-id',
          fields: [{ name: 'field', type: 'rule', inputs: {} }],
        },
        source: iframe.contentWindow,
      }),
    );
    jest.advanceTimersByTime(5000);
    await expect(promise).rejects.toThrow('Timed out while reading rule handler');
    jest.useRealTimers();
  });
});
