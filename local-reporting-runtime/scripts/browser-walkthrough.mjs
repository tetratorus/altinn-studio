// Drive the frontend-test app in a real browser end to end: MockPorten sign-in -> Task_1 -> Task_3 (group)
// -> Task_4 (likert) -> Task_5 (datalist) -> confirm -> receipt, with a screenshot per step in
// evidence/browser-<run>/. Uses the Playwright package from the Studio repo root.
//
//   node scripts/browser-walkthrough.mjs                 launch Playwright's Chromium
//   LRR_CDP_URL=http://localhost:9222 node scripts/...  attach to an already running Chrome instead
import { createRequire } from 'node:module';
import { mkdirSync, writeFileSync } from 'node:fs';
import path from 'node:path';

const require = createRequire(import.meta.url);
const { chromium } = require(path.resolve(import.meta.dirname, '../../node_modules/playwright'));

const APP_URL = 'https://app.local.altinn.cloud/ttd/frontend-test/';
const PID = process.env.LRR_PID ?? '02856221086';
const UNAUTHORIZED_PID = process.env.LRR_UNAUTHORIZED_PID ?? '15877749964';
const PASSWORD = process.env.LRR_MOCKPORTEN_PASSWORD ?? 'lrr-synthetic-only';
const run = new Date().toISOString().replace(/[-:]/g, '').replace(/\..*/, 'Z');
const out = path.resolve(import.meta.dirname, `../evidence/browser-${run}`);
mkdirSync(out, { recursive: true });

const browser = process.env.LRR_CDP_URL
  ? await chromium.connectOverCDP(process.env.LRR_CDP_URL)
  : await chromium.launch();
const context = await browser.newContext({
  ignoreHTTPSErrors: true,
  viewport: { width: 1280, height: 900 },
  locale: 'nb-NO',
});
let page = await context.newPage();
page.setDefaultTimeout(30_000);

const log = [];
let n = 0;
async function step(name, fn) {
  n += 1;
  const file = `${String(n).padStart(2, '0')}-${name}.png`;
  try {
    await fn();
    await page.waitForTimeout(500);
    await page.screenshot({ path: path.join(out, file), fullPage: true });
    log.push(`- PASS: ${name} (${file})`);
    console.log(`PASS ${name}`);
  } catch (e) {
    await page.screenshot({ path: path.join(out, file), fullPage: true }).catch(() => {});
    log.push(`- FAIL: ${name} (${file}): ${String(e.message).split('\n')[0]}`);
    console.log(`FAIL ${name}: ${e.message}`);
    await finish(1);
  }
}
async function finish(code) {
  writeFileSync(
    path.join(out, 'README.md'),
    `# Browser walkthrough ${run}\n\nURL at end: ${page.url()}\n\n${log.join('\n')}\n`,
  );
  await browser.close();
  process.exit(code);
}
const loaded = () => page.locator('#finishedLoading').waitFor({ state: 'attached' });
const idle = () => page.getByRole('progressbar').waitFor({ state: 'detached' });
const submit = async () => {
  await page.getByRole('button', { name: /^Send inn/ }).click();
  await idle();
};

await step('mockporten-login-form', async () => {
  await page.goto(APP_URL);
  await page.locator('input[name="Pid"]').waitFor();
});
await step('signed-in-task1-message', async () => {
  await page.locator('input[name="Pid"]').fill(PID);
  await page.locator('input[name="Password"]').fill(PASSWORD);
  await Promise.all([
    page.waitForURL(/app\.local\.altinn\.cloud/),
    page.locator('form[action="/Authorize"] [type=submit]').click(),
  ]);
  // Earlier runs leave active instances behind; the app then asks whether to continue one or start over.
  const startOver = page.getByRole('button', { name: 'Start på nytt' });
  await Promise.race([
    startOver.waitFor(),
    page.locator('#finishedLoading').waitFor({ state: 'attached' }),
  ]);
  if (await startOver.isVisible()) await startOver.click();
  await loaded();
  await idle();
});
await step('task1-goto-task3', async () => {
  await page.getByRole('button', { name: 'Velg neste steg' }).click();
  await page.getByRole('radio', { name: 'Repeterende grupper (Task_3)' }).click();
  await page.getByRole('button', { name: 'Gå til' }).click();
  await loaded();
  await idle();
});
await step('task3-group-row-added', async () => {
  await page.getByRole('button', { name: /Neste/ }).first().click();
  await page.locator('#showGroupToContinue').getByRole('checkbox', { name: 'Ja' }).check();
  await page.locator('[id^="add-button-mainGroup"]').click();
  await page.locator('input[id^="currentValue"]').fill('1');
  await page.locator('input[id^="currentValue"]').blur();
  await page.locator('input[id^="newValue"]').fill('2');
  await page.locator('input[id^="newValue"]').blur();
  await page
    .locator('#group-mainGroup [data-testid=group-edit-container]')
    .getByRole('button', { name: 'Neste' })
    .click();
  const addSub = page.getByRole('button', { name: 'Legg til ny' });
  if (await addSub.isVisible().catch(() => false)) await addSub.click({ force: true });
  await page.locator('input[id^="comments"]').fill('synthetic browser walkthrough');
  await page.locator('input[id^="comments"]').blur();
  await page.locator('button[id*="save-button-subGroup"]').click();
  await page.locator('[id^="save-button-mainGroup"]').click();
  await idle();
});
await step('task3-summary', async () => {
  await page
    .getByRole('navigation')
    .getByRole('button', { name: /^\d+\. hide$/ })
    .click()
    .catch(async () => {
      await page.getByRole('button', { name: /hide/ }).first().click();
    });
  await page.locator('#sendersName').fill('Synthetic Sender');
  await page.getByRole('button', { name: /Neste/ }).first().click();
  await page.locator('#send-in-text').waitFor();
});
await step('task4-likert', async () => {
  await submit();
  await loaded();
  const table = page.getByRole('table', { name: /Medvirkning/i });
  const questions = [
    'Hører skolen på elevenes forslag?',
    'Er dere elever med på å lage regler for hvordan dere skal ha det i klassen/gruppa?',
    'De voksne på skolen synes det er viktig at vi elever er greie med hverandre.',
  ];
  const options = ['Alltid', 'Nesten alltid', 'Ofte'];
  for (const [i, q] of questions.entries()) {
    const esc = q.replace(/[.*+?^${}()|[\]\\/]/g, '\\$&');
    await table
      .getByRole('row', { name: new RegExp(esc) })
      .getByRole('radio', { name: new RegExp(options[i]) })
      .check();
  }
  await idle();
});
await step('task5-datalist', async () => {
  await submit();
  await loaded();
  await page
    .getByRole('row', { name: /Caroline/ })
    .getByRole('radio')
    .check();
  await idle();
});
await step('task5-summary', async () => {
  await page.getByRole('button', { name: 'Neste' }).last().click();
  await idle();
});
await step('confirm', async () => {
  await submit();
  await loaded();
  await idle();
});
await step('receipt', async () => {
  const confirm = page.getByRole('button', { name: /^(Send inn|Bekreft)/ });
  if (await confirm.isVisible().catch(() => false)) await confirm.click();
  await page
    .getByText(/Skjemaet er sendt inn|Kvittering/i)
    .first()
    .waitFor({ timeout: 60_000 });
  const pdfs = await page.locator('a[href*="/data/"]').count();
  log.push(`  - receipt lists ${pdfs} downloadable data element link(s)`);
});

// Cross-party denial: a second, unrelated user opens the submitted instance's URL in a fresh session.
const instanceUrl = page.url();
await step('unauthorized-user-denied', async () => {
  page = await (
    await browser.newContext({
      ignoreHTTPSErrors: true,
      viewport: { width: 1280, height: 900 },
      locale: 'nb-NO',
    })
  ).newPage();
  page.setDefaultTimeout(30_000);
  await page.goto(APP_URL);
  await page.locator('input[name="Pid"]').fill(UNAUTHORIZED_PID);
  await page.locator('input[name="Password"]').fill(PASSWORD);
  await Promise.all([
    page.waitForURL(/app\.local\.altinn\.cloud/),
    page.locator('form[action="/Authorize"] [type=submit]').click(),
  ]);
  await page.goto(instanceUrl);
  await page
    .getByText(/mangler rettighet|ikke tilgang|Feil 403/i)
    .first()
    .waitFor();
  if (await page.getByText('Skjemaet er sendt inn').isVisible())
    throw new Error('receipt shown to unauthorized user');
});
await finish(0);
