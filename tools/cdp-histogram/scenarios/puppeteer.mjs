// Puppeteer (Node) walking the canonical scenario. Run with --entry=ws to connect through
// browserWSEndpoint, or --entry=url to connect through browserURL (which makes Puppeteer read
// /json/version off the proxy first).
import puppeteer from 'puppeteer';
import { readFile, writeFile } from 'node:fs/promises';
import { createMarker } from '../steps.mjs';

const args = Object.fromEntries(process.argv.slice(2).map((a) => a.split('=', 2)).map(([k, v]) => [k.replace(/^--/, ''), v]));
const { proxy, fixture, entry, meta } = args;

const { mark, step, skipped } = createMarker(proxy);
const notes = {};
const version = JSON.parse(await readFile(new URL('../node_modules/puppeteer/package.json', import.meta.url), 'utf8')).version;

const wsEndpoint = (await (await fetch(`${proxy}/json/version`)).json()).webSocketDebuggerUrl;

await mark('connect');
const browser = entry === 'url'
    ? await puppeteer.connect({ browserURL: proxy })
    : await puppeteer.connect({ browserWSEndpoint: wsEndpoint });

await mark('newContext');
const context = await browser.createBrowserContext();

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

await step('type', () => page.type('#name', 'abc'));

await step('clickCheckbox', () => page.click('input[type=checkbox]'));

await step('selectOption', () => page.select('select', 'b'));

await step('content', async () => {
    notes.content = `${(await page.content()).length} chars`;
});

await step('title', async () => {
    notes.title = await page.title();
});

// Puppeteer 24 moved cookies off the page: they belong to the browser context.
const cookieJar = typeof context.cookies === 'function' ? context : browser;

await step('cookies', async () => {
    notes.cookies = (await cookieJar.cookies()).map((c) => c.name).join(',');
});

await step('setCookie', () => cookieJar.setCookie({ name: 'x', value: 'y', url: fixture }));

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

// No waitUntil: going back lands on a page Chrome may restore from the back/forward cache, and a restored
// page fires no load event. Puppeteer's default waits for the navigation itself, which does happen.
await step('goBack', () => page.goBack());

await step('screenshot', async () => {
    notes.screenshot = `${(await page.screenshot()).length} bytes`;
});

await step('pdf', async () => {
    notes.pdf = `${(await page.pdf()).length} bytes`;
});

await step('interception', async () => {
    await page.setRequestInterception(true);
    const intercepted = [];
    page.on('request', (request) => {
        if (request.url().endsWith('/api.json')) {
            intercepted.push(request.url());
        }
        request.continue().catch(() => { /* already handled */ });
    });
    await page.evaluate(() => fetch('/api.json').then((r) => r.json()));
    notes.interception = `${intercepted.length} intercepted`;
});

await mark('close');
await page.close();
await context.close();
await browser.disconnect();

await new Promise((resolve) => setTimeout(resolve, 250));
await writeFile(meta, JSON.stringify({
    client: entry === 'url' ? 'puppeteer-node-browserURL' : 'puppeteer-node',
    clientVersion: `puppeteer ${version} (node ${process.versions.node})`,
    entryStyle: entry === 'url' ? 'connect({ browserURL })' : 'connect({ browserWSEndpoint })',
    skipped,
    notes,
}, null, 2));
process.exit(0);
