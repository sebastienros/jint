// Playwright (Node) walking the canonical scenario over chromium.connectOverCDP(url).
import { chromium } from 'playwright';
import { readFile, writeFile } from 'node:fs/promises';
import { createMarker } from '../steps.mjs';

const args = Object.fromEntries(process.argv.slice(2).map((a) => a.split('=', 2)).map(([k, v]) => [k.replace(/^--/, ''), v]));
const { proxy, fixture, meta } = args;

const { mark, step, skipped } = createMarker(proxy);
const notes = {};
const version = JSON.parse(await readFile(new URL('../node_modules/playwright/package.json', import.meta.url), 'utf8')).version;

await mark('connect');
const browser = await chromium.connectOverCDP(proxy);

await mark('newContext');
const context = await browser.newContext();

await mark('newPage');
const page = await context.newPage();

await mark('goto');
await page.goto(fixture, { waitUntil: 'load' });

await step('querySelector', async () => {
    notes.querySelector = await page.$('h1') ? 'found' : 'not found';
});

await step('querySelectorAll', async () => {
    notes.querySelectorAll = `${(await page.$$('a')).length} anchors`;
});

await step('evaluateTitle', async () => {
    notes.evaluateTitle = await page.evaluate(() => document.title);
});

await step('evaluateObject', async () => {
    notes.evaluateObject = JSON.stringify(await page.evaluate(() => ({ a: 1, b: [1, 2] })));
});

await step('click', () => page.click('#btn'));

await step('waitForSelector', () => page.waitForSelector('#late'));

await step('type', () => page.locator('#name').pressSequentially('abc'));

await step('clickCheckbox', () => page.click('input[type=checkbox]'));

await step('selectOption', () => page.selectOption('select', 'b'));

await step('content', async () => {
    notes.content = `${(await page.content()).length} chars`;
});

await step('title', async () => {
    notes.title = await page.title();
});

await step('cookies', async () => {
    notes.cookies = (await context.cookies()).map((c) => c.name).join(',');
});

await step('setCookie', () => context.addCookies([{ name: 'x', value: 'y', url: fixture }]));

await step('evaluateLocalStorage', async () => {
    notes.evaluateLocalStorage = String(await page.evaluate(() => localStorage.getItem('k')));
});

await step('consoleEvent', async () => {
    const seen = [];
    page.on('console', (message) => seen.push(message.text()));
    await page.evaluate(() => console.log('from-scenario'));
    await new Promise((resolve) => setTimeout(resolve, 200));
    notes.consoleEvent = seen.join('|');
});

await step('gotoPage2', () => page.goto(new URL('page2.html', fixture).href, { waitUntil: 'load' }));

// waitUntil: 'commit' rather than the default 'load'. Going back lands on a page Chrome restores from the
// back/forward cache, and a restored page fires no load event, so the default waits out its 30 s timeout.
await step('goBack', () => page.goBack({ waitUntil: 'commit' }));

await step('screenshot', async () => {
    notes.screenshot = `${(await page.screenshot()).length} bytes`;
});

await step('pdf', async () => {
    notes.pdf = `${(await page.pdf()).length} bytes`;
});

await step('interception', async () => {
    const intercepted = [];
    await page.route('**/api.json', (route) => {
        intercepted.push(route.request().url());
        route.continue();
    });
    await page.evaluate(() => fetch('/api.json').then((r) => r.json()));
    notes.interception = `${intercepted.length} intercepted`;
});

await mark('close');
await page.close();
await context.close();
await browser.close();

await new Promise((resolve) => setTimeout(resolve, 250));
await writeFile(meta, JSON.stringify({
    client: 'playwright-node',
    clientVersion: `playwright ${version} (node ${process.versions.node})`,
    entryStyle: 'chromium.connectOverCDP(http url)',
    skipped,
    notes,
}, null, 2));
process.exit(0);
