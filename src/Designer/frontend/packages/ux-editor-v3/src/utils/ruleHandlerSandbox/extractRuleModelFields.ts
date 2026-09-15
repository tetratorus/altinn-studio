import { v4 as uuidv4 } from 'uuid';
import type { IRuleModelFieldElement } from '../../types/global';
import { collectRuleModelFields } from './collectRuleModelFields';

// RuleHandler.js is untrusted repository content and must never run in the Designer origin.
// Execute it in an opaque-origin iframe and only accept sanitized metadata from that iframe.

export const escapeForInlineScript = (code: string): string =>
  code.replace(/<\/(script)/gi, '<\\/$1').replace(/<!--/g, '<\\!--');

export const sanitizeFields = (value: unknown): IRuleModelFieldElement[] => {
  if (!Array.isArray(value)) return [];

  return value.flatMap((item) => {
    if (typeof item !== 'object' || item === null || Array.isArray(item)) return [];

    const candidate = item as Record<string, unknown>;
    const { name, type, inputs } = candidate;
    if (
      typeof name !== 'string' ||
      (type !== 'rule' && type !== 'condition') ||
      typeof inputs !== 'object' ||
      inputs === null ||
      Array.isArray(inputs)
    ) {
      return [];
    }

    const sanitizedInputs = Object.fromEntries(
      Object.entries(inputs).filter(([, input]) => typeof input === 'string'),
    );
    return [{ name, type, inputs: sanitizedInputs }];
  });
};

export function extractRuleModelFields(
  ruleModel: string,
): Promise<IRuleModelFieldElement[]> {
  const requestId = uuidv4();
  const html = `<!DOCTYPE html><html><head>
<meta http-equiv="Content-Security-Policy" content="default-src 'none'; script-src 'unsafe-inline'">
</head><body>
<script>${escapeForInlineScript(ruleModel)}</script>
<script>
  (function(){
    var fields = [];
    try { fields = (${collectRuleModelFields.toString()})(window); } catch (e) { fields = []; }
    window.parent.postMessage({ type: 'ruleModelFields', requestId: ${JSON.stringify(requestId)}, fields: JSON.parse(JSON.stringify(fields)) }, '*');
  })();
</script></body></html>`;

  return new Promise((resolve, reject) => {
    const iframe = document.createElement('iframe');
    iframe.setAttribute('sandbox', 'allow-scripts');
    iframe.style.display = 'none';
    iframe.setAttribute('aria-hidden', 'true');
    iframe.srcdoc = html;

    let timeoutId: ReturnType<typeof setTimeout>;
    const cleanup = () => {
      window.removeEventListener('message', handleMessage);
      iframe.remove();
      clearTimeout(timeoutId);
    };
    const handleMessage = (event: MessageEvent) => {
      if (
        event.source !== iframe.contentWindow ||
        event.data?.type !== 'ruleModelFields' ||
        event.data.requestId !== requestId
      ) {
        return;
      }

      cleanup();
      resolve(sanitizeFields(event.data.fields));
    };

    window.addEventListener('message', handleMessage);
    timeoutId = setTimeout(() => {
      cleanup();
      reject(new Error('Timed out while reading rule handler'));
    }, 5000);
    document.body.appendChild(iframe);
  });
}
