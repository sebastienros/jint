// Drives the real Chrome DevTools front end against a real Jint process, and asserts through the front
// end's own DOM.
//
//   node smoke.mjs
//
// Everything else in this repository that claims DevTools works claims it from the protocol side: a test
// sends a command and reads the answer. That proves the server answers; it does not prove a human opening
// DevTools sees anything. This walks the other half — Chrome's own front end, unmodified, loaded from the
// build that shipped in the Chrome being driven, pointed at `Jint.Repl --inspect` — and reads the panels.
//
// It is deliberately not in CI. It downloads a front-end build over the network, drives a user interface
// through its shadow roots, and depends on DevTools' own markup; any of those can change without Jint
// changing. What it produces is evidence for a human: screenshots, a per-step verdict, and a full recording
// of the protocol traffic between the front end and Jint.
//
// The one rule that makes the output worth reading: **a step that could not be driven says so.** It is
// recorded as `not-driven` with what was observed instead, never as a pass and never as a weaker assertion
// wearing the strong one's name. Where the UI could not be driven, the recording of what the front end
// actually sent and what Jint answered stands in for it.

import { mkdir, readFile, rm, writeFile } from 'node:fs/promises';
import { existsSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { launchJint } from './jint.mjs';
import {
    deepClick, deepFullText, deepLocate, deepOutline, deepTexts, installShadowDomHelper,
    launchHostBrowser, resolveFrontend, waitFor,
} from './frontend.mjs';

const here = dirname(fileURLToPath(import.meta.url));
const outDir = join(here, 'out');
const fixture = join(here, 'fixture', 'app.js');

// The step budget. Generous, because the front end downloads a few megabytes before it connects, and the
// engine only reaches a breakpoint on the next turn of a one-second timer.
const STEP_TIMEOUT = 60000;

// Selectors, all of them piercing shadow roots through `window.__smoke`. Named rather than inlined because
// every one of them is a bet on DevTools' markup, and when one stops matching this is the list to read.
const UI = {
    panelTab: '.tabbed-pane-header-tab',
    navigatorFolder: 'li.navigator-folder-tree-item',
    navigatorFile: 'li.navigator-file-tree-item',
    editorContent: '.cm-content:not([aria-label="Console prompt"])',
    gutterLine: '.cm-lineNumbers .cm-gutterElement, .cm-gutterElement',
    consoleFilter: '[role="textbox"][aria-label="Filter"]',
    consolePrompt: '.cm-content[aria-label="Console prompt"]',
    consoleMessage: '.console-message-text',
    consoleResult: '.console-user-command-result',
    consoleAnchor: '.console-message-anchor',
    consoleStack: '.stack-preview-container, .console-message-stack-trace-wrapper',
    callFrame: '.call-frame-item-title, .call-frame-title-text',
    callFrameLocation: '.call-frame-location',
    scopeSection: '.scope-chain-sidebar-pane-section-title',
    scopeBinding: '.object-properties-section span.name-and-value',
    infoMessage: '.gray-info-message',
    resumeButton: 'button[aria-label*="Resume script execution"]',
    pausedStatus: '.paused-status, .paused-message',
};

const results = [];
let stepIndex = 0;

/** The line the breakpoint goes on, found in the fixture rather than hard-coded. */
async function breakpointLine() {
    const lines = (await readFile(fixture, 'utf8')).split(/\r?\n/);
    const index = lines.findIndex((line) => line.includes('total += lineTotal'));
    if (index < 0) {
        throw new Error(`fixture/app.js no longer holds the accumulator line the breakpoint step needs`);
    }
    return { number: index + 1, text: lines[index].trim() };
}

/**
 * Runs one step, screenshots it, and records the verdict.
 *
 * `body` returns what it observed. It may return `{ status: 'not-driven', observed, instead }` to say the
 * UI could not be driven; anything it throws is a failure, and the message says what was being waited for.
 */
async function step(page, proxy, name, proves, body) {
    stepIndex++;
    const label = `${String(stepIndex).padStart(2, '0')}-${name}`;
    await fetch(`${proxy.httpUrl}/__mark?name=${encodeURIComponent(name)}`).catch(() => { /* recording only */ });
    process.stdout.write(`\n[${label}] ${proves}\n`);

    const started = Date.now();
    const entry = { step: name, proves, status: 'fail', observed: null, elapsedMs: 0, screenshot: `${label}.png` };
    try {
        const outcome = await body();
        if (outcome && outcome.status === 'not-driven') {
            entry.status = 'not-driven';
            entry.observed = outcome.observed ?? null;
            entry.instead = outcome.instead ?? null;
            process.stdout.write(`  NOT DRIVEN: ${outcome.instead ?? ''}\n`);
        } else {
            entry.status = 'pass';
            entry.observed = outcome ?? null;
            process.stdout.write(`  PASS\n`);
        }
    } catch (error) {
        entry.error = error.message;
        process.stdout.write(`  FAIL: ${error.message}\n`);
    }
    entry.elapsedMs = Date.now() - started;

    await page.screenshot({ path: join(outDir, `${label}.png`) }).catch(() => { /* the page may be gone */ });
    const outline = await deepOutline(page).catch(() => null);
    if (outline) {
        await writeFile(join(outDir, 'dom', `${label}.txt`), outline.join('\n'));
    }
    results.push(entry);
    return entry;
}

/** Reads the recording back, sliced by the marks each step wrote. */
async function readProtocol(logPath) {
    const rows = (await readFile(logPath, 'utf8'))
        .split('\n')
        .filter(Boolean)
        .map((line) => { try { return JSON.parse(line); } catch { return null; } })
        .filter(Boolean);

    const slices = [];
    let current = { step: '(before the first step)', commands: [], events: [], errors: [] };
    for (const row of rows) {
        if (row.direction === 'mark') {
            slices.push(current);
            current = { step: row.step, commands: [], events: [], errors: [] };
            continue;
        }
        if (row.direction === 'c2b' && row.method) {
            current.commands.push(row.method);
        } else if (row.event && row.method) {
            current.events.push(row.method);
        }
        if (row.error) {
            current.errors.push(`${row.method}: ${row.error.code} ${row.error.message}`);
        }
    }
    slices.push(current);

    const distinct = (values) => [...new Set(values)];
    return {
        rows: rows.length,
        slices: slices.map((slice) => ({
            step: slice.step,
            commands: distinct(slice.commands),
            events: distinct(slice.events),
            errors: distinct(slice.errors),
        })),
        allCommands: distinct(rows.filter((r) => r.direction === 'c2b' && r.method).map((r) => r.method)),
        allEvents: distinct(rows.filter((r) => r.event && r.method).map((r) => r.method)),
        allErrors: distinct(rows.filter((r) => r.error).map((r) => `${r.method}: ${r.error.code} ${r.error.message}`)),
    };
}

/** Whichever tick the heartbeat has reached, according to what the Console is rendering. */
async function latestTick(page) {
    const texts = await deepTexts(page, UI.consoleMessage, 300);
    const ticks = texts
        .map((text) => /heartbeat \{tick: (\d+)/.exec(text))
        .filter(Boolean)
        .map((match) => Number(match[1]));
    return ticks.length ? Math.max(...ticks) : null;
}

/** Clicks a main panel tab by its name and waits for it to be the selected one. */
async function openPanel(page, panel) {
    const clicked = await deepClick(page, UI.panelTab, { text: panel, exact: true });
    if (!clicked) {
        throw new Error(`no main panel tab reading "${panel}"`);
    }
    await waitFor(`the ${panel} panel to become the selected tab`, async () => {
        const selected = await deepTexts(page, `${UI.panelTab}.selected`, 10);
        return selected.includes(panel);
    }, { timeoutMs: 15000 });
}

/** Types into one of the front end's contenteditable prompts, replacing whatever was in it. */
async function typeInto(page, selector, text) {
    const found = await waitFor(`an editable element matching ${selector}`,
        () => deepLocate(page, selector), { timeoutMs: 30000 });
    await page.mouse.click(found.x, found.y);
    await page.keyboard.down('Control');
    await page.keyboard.press('KeyA');
    await page.keyboard.up('Control');
    await page.keyboard.press('Backspace');
    if (text) {
        await page.keyboard.type(text, { delay: 20 });
    }
}

async function main() {
    await rm(outDir, { recursive: true, force: true }).catch(() => { /* first run */ });
    await mkdir(join(outDir, 'dom'), { recursive: true });

    if (!existsSync(fixture)) {
        throw new Error(`the fixture is missing: ${fixture}`);
    }

    // The recording proxy is `tools/cdp-histogram/proxy.mjs`, imported rather than copied. It resolves `ws`
    // from its own directory, so the import is dynamic and the failure is explained rather than raw.
    let startProxy;
    try {
        ({ startProxy } = await import('../cdp-histogram/proxy.mjs'));
    } catch (error) {
        throw new Error(
            `could not load the recording proxy from tools/cdp-histogram/proxy.mjs: ${error.message}\n`
            + 'It imports "ws", which Node resolves from *its* directory. Run:\n'
            + '  node -e "await (await import(\'node:fs/promises\')).cp(\'node_modules/ws\', \'../cdp-histogram/node_modules/ws\', { recursive: true })"\n'
            + 'from tools/devtools-frontend-smoke after npm install.');
    }

    const line = await breakpointLine();
    const environment = { startedAt: new Date().toISOString() };

    const jint = await launchJint({ script: fixture.replace(/\\/g, '/'), timeoutSeconds: 60 });
    environment.jint = { command: jint.command, argv: jint.argv, port: jint.port, targetId: jint.targetId, target: jint.target };
    console.log(`jint:     ${jint.command}\n          ${jint.httpUrl}${jint.wsPath}`);

    const logPath = join(outDir, 'protocol.jsonl');
    const proxy = await startProxy({ upstreamHttp: jint.httpUrl, logPath, client: 'devtools-frontend-smoke' });
    console.log(`proxy:    ${proxy.httpUrl} (recording to out/protocol.jsonl)`);

    const browser = await launchHostBrowser();
    let page = null;
    try {
        const chromeVersion = await browser.version();
        const frontend = await resolveFrontend(chromeVersion);
        environment.chrome = chromeVersion;
        environment.frontend = { revision: frontend.revision, how: frontend.how, base: frontend.base };
        console.log(`chrome:   ${chromeVersion}`);
        console.log(`frontend: ${frontend.revision} (${frontend.how})`);

        const url = `${frontend.base}?v8only=true&panel=sources&ws=${proxy.httpUrl.replace('http://', '')}${jint.wsPath}`;
        environment.frontendUrl = url;

        page = await browser.newPage();
        await installShadowDomHelper(page);

        // The front end reports its own protocol failures to its console, and that is the cheapest place to
        // see a command Jint refused: "Request <Method> failed. {code, message}".
        const frontendErrors = [];
        page.on('console', (message) => { if (message.type() === 'error') { frontendErrors.push(message.text().slice(0, 400)); } });
        page.on('pageerror', (error) => frontendErrors.push(String(error).slice(0, 400)));

        // --- 1 -------------------------------------------------------------------------------------------
        await step(page, proxy, 'frontend-connected',
            'the real DevTools front end loads and completes a handshake with Jint',
            async () => {
                await page.goto(url, { waitUntil: 'domcontentloaded', timeout: STEP_TIMEOUT });
                const tabs = await waitFor('the DevTools panel tabs to render', async () => {
                    const found = await deepTexts(page, UI.panelTab, 20);
                    return found.length ? found : null;
                }, { timeoutMs: STEP_TIMEOUT });

                const protocol = await waitFor('Runtime.enable and Debugger.scriptParsed on the wire', async () => {
                    const read = await readProtocol(logPath);
                    return read.allCommands.includes('Runtime.enable') && read.allEvents.includes('Debugger.scriptParsed') ? read : null;
                }, { timeoutMs: STEP_TIMEOUT });

                return { panelTabs: tabs, handshakeCommands: protocol.allCommands, handshakeEvents: protocol.allEvents };
            });

        // --- 2 -------------------------------------------------------------------------------------------
        let scriptEntry = null;
        await step(page, proxy, 'sources-lists-script',
            'the Sources navigator lists the script the engine is running',
            async () => {
                // The navigator groups scripts under a domain and then under the folders of their path, and
                // starts collapsed — so the file is in the document before it is on screen, and every node
                // down to it has to be opened. Expanding is part of the step: an assertion that read the
                // collapsed subtree would be reporting the DOM rather than the panel.
                const opened = [];
                const entries = await waitFor('a visible file entry in the Sources navigator', async () => {
                    for (const text of await deepTexts(page, UI.navigatorFolder, 20, true)) {
                        if (opened.includes(text)) {
                            continue;
                        }
                        opened.push(text);
                        if (await deepClick(page, UI.navigatorFolder, { text, exact: true })) {
                            await page.keyboard.press('ArrowRight');
                        }
                    }

                    const found = await deepTexts(page, UI.navigatorFile, 50, true);
                    return found.length ? found : null;
                }, { timeoutMs: STEP_TIMEOUT });

                const folder = { text: opened[0] };

                scriptEntry = entries.find((text) => text.replace(/\\/g, '/').endsWith('app.js'));
                if (!scriptEntry) {
                    throw new Error(`the navigator shows ${JSON.stringify(entries)}, none of which ends with app.js`);
                }

                // Jint publishes an absolute path as a file:// URL, so the navigator groups the script under
                // a domain and names it by its file name — not "(no domain)" with the whole path for a name.
                const folderText = folder?.text ?? folders[0];
                if (!folderText.startsWith('file://')) {
                    throw new Error(`the navigator groups the script under "${folderText}", not under a file:// domain`);
                }
                if (scriptEntry !== 'app.js') {
                    throw new Error(`the navigator names the script "${scriptEntry}" rather than "app.js"`);
                }

                return { folder: folderText, navigatorEntries: entries, matched: scriptEntry };
            });

        // --- 3 -------------------------------------------------------------------------------------------
        await step(page, proxy, 'sources-shows-source-text',
            'opening the script shows the source text the engine retained',
            async () => {
                if (!scriptEntry) {
                    throw new Error('the previous step never found the script, so there is nothing to open');
                }
                await waitFor(`the navigator entry ${JSON.stringify(scriptEntry)} to be clickable`,
                    () => deepClick(page, UI.navigatorFile, { text: scriptEntry, exact: true }), { timeoutMs: 30000 });

                const wanted = ['lineTotal', 'computeTotal', 'describeOrders', 'heartbeat', 'reconcile'];
                const editor = await waitFor(`the editor to show ${wanted.join(', ')}`, async () => {
                    const body = await deepFullText(page, UI.editorContent);
                    return body && wanted.every((name) => body.includes(name)) ? body : null;
                }, { timeoutMs: STEP_TIMEOUT });

                return {
                    functionsFound: wanted,
                    editorLength: editor.length,
                    declarationsRead: editor.match(/function \w+\([^)]*\)/g) ?? [],
                };
            });

        // --- 4 -------------------------------------------------------------------------------------------
        await step(page, proxy, 'console-shows-log-with-object-preview',
            'the Console panel shows the console.log, object preview and all',
            async () => {
                await openPanel(page, 'Console');
                // The console viewport only keeps the visible tail in the DOM, and the heartbeat pushes
                // everything else off it. Filtering is how the message from before the front end connected
                // is brought back — which also proves the journal Jint replays on Runtime.enable arrived.
                await typeInto(page, UI.consoleFilter, 'order book ready');

                const message = await waitFor('a console message reading "order book ready"', async () => {
                    const texts = await deepTexts(page, UI.consoleMessage, 50);
                    return texts.find((text) => text.includes('order book ready')) ?? null;
                }, { timeoutMs: STEP_TIMEOUT });

                const preview = ['count', 'total', 'first', '30.75', 'widget'].filter((part) => message.includes(part));
                if (preview.length !== 5) {
                    throw new Error(`the message is "${message}", which is missing ${['count', 'total', 'first', '30.75', 'widget'].filter((p) => !message.includes(p)).join(', ')} from the object preview`);
                }
                return { message, previewParts: preview };
            });

        // --- 5 -------------------------------------------------------------------------------------------
        await step(page, proxy, 'console-message-has-a-source-anchor',
            'each console message links to the line of the script it was logged from',
            async () => {
                // The filter from the previous step is still on the "order book ready" line, which was
                // logged from the top level of app.js.
                const anchors = await waitFor('a source anchor beside a console message', async () => {
                    const found = await deepTexts(page, UI.consoleAnchor, 20, true);
                    return found.length ? found : null;
                }, { timeoutMs: STEP_TIMEOUT });

                const anchored = anchors.filter((text) => /app\.js:\d+/.test(text));
                if (anchored.length === 0) {
                    throw new Error(`the console anchors read ${JSON.stringify(anchors)}, none of which names app.js and a line`);
                }

                return { anchors, anchored };
            });

        // --- 6 -------------------------------------------------------------------------------------------
        await step(page, proxy, 'uncaught-exception-shows-its-stack',
            'the uncaught throw from inside a timer callback shows an expanded stack naming the callback',
            async () => {
                await typeInto(page, UI.consoleFilter, 'reconciliation failed');

                const stack = await waitFor('the error message with its stack expanded', async () => {
                    const messages = await deepTexts(page, UI.consoleMessage, 30, true);
                    const failure = messages.find((text) => text.includes('reconciliation failed'));
                    if (!failure) {
                        return null;
                    }

                    const frames = await deepTexts(page, UI.consoleStack, 10, true);
                    const text = frames.join('\n');

                    // `reconcile` is the timer callback itself: the frame the engine was entered at, which
                    // is absent from the stack unless the callback owns a call-stack frame.
                    return text.includes('reconcile') ? { failure, frames } : null;
                }, { timeoutMs: STEP_TIMEOUT });

                await typeInto(page, UI.consoleFilter, '');
                return stack;
            });

        // --- 7 -------------------------------------------------------------------------------------------
        await step(page, proxy, 'console-evaluates-expression',
            'typing an expression at the Console prompt evaluates it in the engine',
            async () => {
                await typeInto(page, UI.consoleFilter, '');
                await typeInto(page, UI.consolePrompt, '1+1');
                await page.keyboard.press('Enter');

                const result = await waitFor('the console to show the result of 1+1', async () => {
                    const texts = await deepTexts(page, UI.consoleResult, 20);
                    return texts.find((text) => text.trim() === '2') ?? null;
                }, { timeoutMs: STEP_TIMEOUT });

                return { typed: '1+1', result };
            });

        // --- 8 -------------------------------------------------------------------------------------------
        let tickBeforeResume = null;
        let pausedForReal = false;
        await step(page, proxy, 'breakpoint-set-in-the-gutter-is-hit',
            'a breakpoint the front end set by clicking the gutter pauses the engine',
            async () => {
                await openPanel(page, 'Sources');
                // The drawer console, so the heartbeat stays readable while the Sources panel is in front:
                // that is how the resume step proves the engine really continued.
                await page.keyboard.press('Escape');

                const gutter = await waitFor(`the editor gutter to show line ${line.number}`,
                    () => deepLocate(page, UI.gutterLine, { text: String(line.number), exact: true }),
                    { timeoutMs: STEP_TIMEOUT });
                await page.mouse.click(gutter.x, gutter.y);

                const paused = await waitFor(`the debugger to pause on line ${line.number} (${line.text})`, async () => {
                    const status = await deepTexts(page, UI.pausedStatus, 5, true);
                    const info = await deepTexts(page, UI.infoMessage, 10, true);
                    const resume = await deepLocate(page, UI.resumeButton);
                    return (status.length || resume) && !info.includes('Not paused')
                        ? { status, resumeButton: resume?.label ?? null }
                        : null;
                }, { timeoutMs: STEP_TIMEOUT });

                pausedForReal = true;
                tickBeforeResume = await latestTick(page);

                // The Call Stack pane, which is where the entry frame shows. `computeTotal` was called by
                // `heartbeat`, the setInterval callback — the frame the engine was entered at. Without a
                // frame of its own the pane showed `computeTotal` over an unnamed row.
                const frames = await deepTexts(page, UI.callFrame, 20, true);
                if (!frames.includes('heartbeat')) {
                    throw new Error(`the Call Stack reads ${JSON.stringify(frames)}, which does not name the timer callback`);
                }

                // Every row carries a location, which is what makes it clickable.
                const locations = await deepTexts(page, UI.callFrameLocation, 20, true);
                const located = locations.filter((text) => /app\.js:\d+/.test(text));
                if (located.length < 2) {
                    throw new Error(`the Call Stack rows link to ${JSON.stringify(locations)}, fewer than two of which name app.js`);
                }

                return {
                    breakpointLine: `${line.number}: ${line.text}`,
                    pausedIndicator: paused.status,
                    resumeButton: paused.resumeButton,
                    callStackSays: frames,
                    callStackLinksTo: locations,
                    tickWhenPaused: tickBeforeResume,
                };
            });

        // --- 9 -------------------------------------------------------------------------------------------
        await step(page, proxy, 'scope-pane-names-its-functions',
            'the Scope pane shows the paused frame\'s bindings',
            async () => {
                if (!pausedForReal) {
                    return { status: 'not-driven', instead: 'the engine never paused, so there was no frame to show scopes for' };
                }
                const scope = await waitFor('the Scope pane to list a scope and the bindings of the paused frame', async () => {
                    const scopes = await deepTexts(page, UI.scopeSection, 20, true);
                    const all = await deepTexts(page, UI.scopeBinding, 200, true);
                    const bindings = all.filter((text) => /^(total|items|i|this|arguments):/.test(text));
                    return scopes.length && bindings.length ? { scopes, bindings, all } : null;
                }, { timeoutMs: STEP_TIMEOUT });

                // Every function-valued binding is rendered from RemoteObject.description, which the front
                // end parses as Function.prototype.toString output. A label there made all of them read
                // "ƒ undefined()"; the source text makes each read "ƒ <its own name>(<its parameters>)".
                const functionValues = scope.all.filter((text) => text.includes(': ƒ '));
                const rendered = [...new Set(functionValues.map((text) => text.replace(/^[^:]+: /, '')))];

                const undefinedOnes = rendered.filter((text) => text.startsWith('ƒ undefined('));
                if (undefinedOnes.length > 0) {
                    throw new Error(`the Scope pane renders ${undefinedOnes.length} function(s) as "ƒ undefined()"`);
                }

                // The frame's own function-valued local, which is a *script* function: its name and its
                // parameter can only have come from the declaration Jint retained. A native one would prove
                // the `[native code]` half alone.
                if (!rendered.includes('ƒ describeLine(order)')) {
                    throw new Error(`the Scope pane renders ${JSON.stringify(rendered)}, none of which is ƒ describeLine(order)`);
                }

                return {
                    scopes: scope.scopes,
                    bindings: scope.bindings,
                    functionValuedBindings: { count: functionValues.length, rendered: rendered.slice(0, 8) },
                };
            });

        // --- 10 ------------------------------------------------------------------------------------------
        await step(page, proxy, 'resume-continues-execution',
            'Resume lets the engine run on, which the heartbeat advancing proves',
            async () => {
                if (!pausedForReal) {
                    return { status: 'not-driven', instead: 'the engine never paused, so there was nothing to resume' };
                }
                // Take the breakpoint back off first, or the next heartbeat pauses again a second later and
                // "did it resume" becomes a race with the answer.
                const gutter = await deepLocate(page, UI.gutterLine, { text: String(line.number), exact: true });
                if (gutter) {
                    await page.mouse.click(gutter.x, gutter.y);
                }

                const resumed = await deepClick(page, UI.resumeButton);
                if (!resumed) {
                    throw new Error('no Resume button in the debugger toolbar');
                }

                const advanced = await waitFor(`the heartbeat to pass tick ${tickBeforeResume}`, async () => {
                    const tick = await latestTick(page);
                    return tick !== null && tickBeforeResume !== null && tick > tickBeforeResume ? tick : null;
                }, { timeoutMs: STEP_TIMEOUT });

                const info = await deepTexts(page, UI.infoMessage, 10, true);
                return {
                    tickWhenPaused: tickBeforeResume,
                    tickAfterResume: advanced,
                    callStackSays: info.find((text) => text === 'Not paused') ?? info.slice(0, 3),
                };
            });

        environment.frontendConsoleErrors = frontendErrors;
    } finally {
        // Always, and only ever what this process started.
        if (page) {
            await page.close().catch(() => { /* already gone */ });
        }
        await browser.close().catch(() => { /* already gone */ });
        await proxy.close();
        jint.kill();
    }

    const protocol = await readProtocol(logPath);
    environment.finishedAt = new Date().toISOString();
    environment.jintOutputTail = jint.output().split(/\r?\n/).slice(-12);
    environment.jintErrorTail = jint.errors().split(/\r?\n/).slice(-6);

    const summary = { environment, steps: results, protocol };
    await writeFile(join(outDir, 'summary.json'), `${JSON.stringify(summary, null, 2)}\n`);

    console.log('\n--- verdict ------------------------------------------------------------------');
    for (const entry of results) {
        console.log(`${entry.status.toUpperCase().padEnd(11)} ${entry.step}${entry.error ? ` — ${entry.error}` : ''}`);
    }
    console.log(`\nout/summary.json, out/protocol.jsonl (${protocol.rows} frames), ${results.length} screenshots in out/.`);
    if (environment.frontendConsoleErrors?.length) {
        console.log(`\nthe front end's own console reported ${environment.frontendConsoleErrors.length} errors; summary.json has them all.`);
    }

    return results.some((entry) => entry.status === 'fail') ? 1 : 0;
}

process.exitCode = await main().catch((error) => {
    console.error(`\n${error.message}`);
    return 1;
});
