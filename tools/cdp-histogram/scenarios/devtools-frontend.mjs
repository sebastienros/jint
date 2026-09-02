// Best effort: what the Chrome DevTools frontend itself sends, against a Node-flavoured target.
//
// This one is not the canonical scenario and cannot be. The frontend is a user interface, not a client
// library: it has no goto/click/evaluate API, and it is pointed at a Node inspector rather than a page,
// because a Node target is what Jint.DevTools will look like (type "node", v8only frontend). What it
// records is the frontend's *handshake* — the domains it enables and the state it pulls before a human
// touches anything — plus whatever the Sources panel asks for, which is requested by loading the frontend
// with &panel=sources rather than by driving its UI (driving the UI through its shadow roots would be a
// brittle test of DevTools' own markup, not of the protocol).
//
// The frontend has to be reachable. Chrome refuses to navigate an ordinary page to devtools://, so the
// hosted build at chrome-devtools-frontend.appspot.com is used. That host serves by devtools-frontend
// commit and only serves revisions that were actually rolled into a Chromium release, so the revision is
// taken from the DEPS file of the Chrome build being driven rather than from the tip of the frontend
// repository — which is how the frontend and the browser stay the same vintage. If nothing answers, this
// writes a file that says so instead of a fake recording.

import { spawn } from 'node:child_process';
import { writeFile } from 'node:fs/promises';
import { join } from 'node:path';
import puppeteer from 'puppeteer';
import { startProxy } from '../proxy.mjs';
import { summarize } from '../analyze.mjs';

const args = Object.fromEntries(process.argv.slice(2).map((a) => a.split('=', 2)).map(([k, v]) => [k.replace(/^--/, ''), v]));
const outPath = args.out;
const rawDir = args.raw;

const CLIENT = 'devtools-frontend';

async function firstReachable(urls) {
    for (const url of urls) {
        try {
            const response = await fetch(url, { redirect: 'follow' });
            if (response.ok && (await response.text()).includes('<script')) {
                return url;
            }
        } catch {
            // Next candidate.
        }
    }
    return null;
}

/** The devtools-frontend revision that shipped in this exact Chrome, read from Chromium's DEPS. */
async function revisionFromChromiumDeps(chromeVersion) {
    const tag = /\d+\.\d+\.\d+\.\d+/.exec(chromeVersion)?.[0];
    if (!tag) {
        return { revisions: [], how: `could not read a Chromium tag out of "${chromeVersion}"` };
    }
    try {
        const response = await fetch(`https://chromium.googlesource.com/chromium/src/+/refs/tags/${tag}/DEPS?format=TEXT`);
        if (!response.ok) {
            return { revisions: [], how: `chromium.googlesource.com answered ${response.status} for tag ${tag}` };
        }
        const deps = Buffer.from(await response.text(), 'base64').toString('utf8');
        const match = /'devtools_frontend_revision':\s*'([0-9a-f]{40})'/.exec(deps);
        return match
            ? { revisions: [match[1]], how: `devtools_frontend_revision from Chromium DEPS at tag ${tag}` }
            : { revisions: [], how: `no devtools_frontend_revision in Chromium DEPS at tag ${tag}` };
    } catch (error) {
        return { revisions: [], how: `could not reach chromium.googlesource.com: ${error.message}` };
    }
}

async function notRecorded(reason, extra = {}) {
    await writeFile(outPath, `${JSON.stringify({
        client: CLIENT,
        clientVersion: 'not recorded',
        entryStyle: 'hosted js_app.html?v8only=true&ws=<proxy>',
        recordedAt: new Date().toISOString().slice(0, 10),
        bestEffort: true,
        notRecorded: reason,
        counts: { distinctMethods: 0, distinctEvents: 0, domains: [] },
        sessionModel: { flattened: false, setAutoAttachParams: null, attachToTargetParams: null, sessionCount: 0 },
        scenarioSteps: [],
        allMethods: [],
        allEvents: [],
        errors: [],
        ...extra,
    }, null, 2)}\n`);
    console.error(`devtools-frontend: ${reason}`);
}

// --- the Node target ------------------------------------------------------------------------------------

const inspected = spawn(process.execPath, ['--inspect=0', '-e', 'setInterval(() => {}, 1000)'], { stdio: ['ignore', 'ignore', 'pipe'] });
let stderr = '';
const nodeWs = await new Promise((resolve) => {
    inspected.stderr.on('data', (chunk) => {
        stderr += chunk;
        const match = /ws:\/\/(\S+)/.exec(stderr);
        if (match) {
            resolve(match[0]);
        }
    });
    setTimeout(() => resolve(null), 10000);
});

if (!nodeWs) {
    inspected.kill();
    await notRecorded(`node --inspect did not report a websocket URL: ${stderr.trim()}`);
    process.exit(1);
}

const nodeUrl = new URL(nodeWs.replace('ws://', 'http://'));
const wsPath = nodeUrl.pathname;
const logPath = join(rawDir, `${CLIENT}.jsonl`);
const proxy = await startProxy({ upstreamHttp: `http://${nodeUrl.host}`, logPath, client: CLIENT });

// --- the browser that hosts the frontend page -------------------------------------------------------------

// Chrome 148 blocks a request from a public origin (the hosted frontend) to a loopback one with
// net::ERR_BLOCKED_BY_LOCAL_NETWORK_ACCESS_CHECKS, and the frontend's socket to the proxy is exactly that
// request. Disabling Local Network Access for this throwaway browser is what makes the capture possible;
// without it the recording is silently empty and looks like "the frontend sends nothing".
const browser = await puppeteer.launch({
    headless: true,
    args: [
        '--no-first-run',
        '--disable-gpu',
        '--disable-features=LocalNetworkAccessChecks,PrivateNetworkAccessSendPreflights,PrivateNetworkAccessRespectPreflightResults',
    ],
});
const hostVersion = await browser.version();

const { revisions, how } = process.env.CDP_FRONTEND_REVISION
    ? { revisions: [process.env.CDP_FRONTEND_REVISION], how: 'CDP_FRONTEND_REVISION environment variable' }
    : await revisionFromChromiumDeps(hostVersion);

const frontendBase = process.env.CDP_FRONTEND_URL
    ?? await firstReachable(revisions.map((sha) => `https://chrome-devtools-frontend.appspot.com/serve_rev/@${sha}/js_app.html`));

if (!frontendBase) {
    await browser.close();
    await proxy.close();
    inspected.kill();
    await notRecorded(`no hosted DevTools frontend build answered (${how}); set CDP_FRONTEND_URL to a js_app.html that does`, {
        revisionsTried: revisions,
    });
    process.exit(1);
}

// v8only=true is the Node flavour: no Page, DOM, Network or Emulation panels. panel=sources opens the panel
// the debugger traffic comes from without touching the UI.
const frontendUrl = `${frontendBase}?v8only=true&panel=sources&ws=${proxy.httpUrl.replace('http://', '')}${wsPath}`;

const page = await browser.newPage();
const notes = { frontendRevision: revisions[0] ?? 'from CDP_FRONTEND_URL', howChosen: how, hostBrowser: hostVersion };

// The frontend reports its own failures to its console, and a failure to open the socket is exactly the
// failure that would otherwise look like "the frontend sends nothing".
const frontendErrors = [];
page.on('console', (message) => {
    if (message.type() === 'error') {
        frontendErrors.push(message.text().slice(0, 200));
    }
});
page.on('pageerror', (error) => frontendErrors.push(String(error).slice(0, 200)));

try {
    await fetch(`${proxy.httpUrl}/__mark?name=connect`);
    await page.goto(frontendUrl, { waitUntil: 'domcontentloaded', timeout: 60000 });
    await new Promise((resolve) => setTimeout(resolve, 10000));
    await fetch(`${proxy.httpUrl}/__mark?name=handshakeSettled`);
    // The Sources panel is already the active one; give it time to ask for scripts and scope state.
    await new Promise((resolve) => setTimeout(resolve, 8000));
    await fetch(`${proxy.httpUrl}/__mark?name=close`);
    notes.frontendPageTitle = await page.title();
    if (frontendErrors.length) {
        notes.frontendConsoleErrors = frontendErrors.join(' | ');
    }
} catch (error) {
    notes.error = String(error);
} finally {
    await page.close().catch(() => { /* already gone */ });
    await browser.close().catch(() => { /* already gone */ });
    await proxy.close();
    inspected.kill();
}

const handshake = await summarize(logPath, {
    client: CLIENT,
    clientVersion: `hosted devtools-frontend @${(revisions[0] ?? 'custom').slice(0, 12)}, loaded in ${hostVersion}`,
    entryStyle: 'hosted js_app.html?v8only=true&panel=sources&ws=<proxy>/<node inspector uuid>',
    notes,
    skipped: {},
}, { version: `node ${process.versions.node} inspector (not Chrome)` });

handshake.bestEffort = true;
handshake.bestEffortNote = 'Not the canonical scenario: the DevTools frontend has no automation API, and its target '
    + 'here is a Node inspector, not a page. This is its passive handshake plus what the Sources panel asks for, '
    + 'obtained by loading the hosted frontend with &panel=sources. No UI was driven and no breakpoint was set.';
await writeFile(outPath, `${JSON.stringify(handshake, null, 2)}\n`);
console.log(`devtools-frontend: ${handshake.counts.distinctMethods} commands, ${handshake.counts.distinctEvents} events`);
process.exit(0);
