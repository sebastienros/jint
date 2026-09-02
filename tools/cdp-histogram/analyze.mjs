// Turns one recorded JSON-lines log plus its scenario metadata into the checked-in handshake file, and a
// set of handshake files into the cross-client matrix.
//
// Nothing here talks to a browser; it is pure aggregation, so a matrix can be rebuilt from checked-in
// results without re-recording anything.

import { readFile } from 'node:fs/promises';
import { STEPS } from './steps.mjs';

function domainOf(method) {
    const dot = method.indexOf('.');
    return dot < 0 ? method : method.slice(0, dot);
}

/** One distinct value stays a scalar; more than one stays a list, because that is the finding. */
function collapse(values) {
    return Object.fromEntries(Object.entries(values).map(([key, seen]) => [key, seen.length === 1 ? seen[0] : seen]));
}

function mergeKeys(into, keys) {
    for (const key of keys ?? []) {
        if (!into.includes(key)) {
            into.push(key);
        }
    }
}

/**
 * Reads a log and produces the handshake object.
 * @param {string} logPath  JSON lines written by proxy.mjs
 * @param {object} meta     what the scenario script wrote about itself
 * @param {object} chrome   {version, executablePath}
 */
export async function summarize(logPath, meta, chrome) {
    const lines = (await readFile(logPath, 'utf8')).split('\n').filter(Boolean).map((line) => JSON.parse(line));

    const steps = new Map();           // step -> {methods: Map, events: Map}
    const allMethods = [];
    const allEvents = [];
    const errors = [];
    const sessionModel = { flattened: false, setAutoAttachParams: null, attachToTargetParams: null, sessionCount: 0 };
    const sessions = new Set();
    let current = 'preamble';

    const stepOf = (name) => {
        if (!steps.has(name)) {
            steps.set(name, { methods: new Map(), events: new Map() });
        }
        return steps.get(name);
    };

    // (sessionId, id) -> the method entry the response belongs to, so a result lands on the request's row
    // even when the reply arrives after the next mark.
    const pending = new Map();
    const key = (sessionId, id) => `${sessionId ?? ''}#${id}`;

    for (const row of lines) {
        if (row.direction === 'mark') {
            current = row.step;
            continue;
        }
        if (row.direction === 'meta') {
            continue;
        }
        if (row.sessionId) {
            sessions.add(row.sessionId);
        }

        if (row.direction === 'c2b') {
            if (!row.method) {
                continue;
            }
            const bucket = stepOf(current).methods;
            let entry = bucket.get(row.method);
            if (!entry) {
                entry = { method: row.method, count: 0, paramsKeys: [], resultKeys: [], errors: [] };
                bucket.set(row.method, entry);
            }
            entry.count++;
            mergeKeys(entry.paramsKeys, row.paramsKeys);
            if (row.paramsValues) {
                // Distinct values, not the last one: a client that calls Runtime.callFunctionOn 36 times
                // with returnByValue both ways is telling us it needs both, and merging would hide one.
                entry.paramsValues ??= {};
                for (const [key, value] of Object.entries(row.paramsValues)) {
                    const seen = (entry.paramsValues[key] ??= []);
                    const encoded = JSON.stringify(value);
                    if (!seen.some((existing) => JSON.stringify(existing) === encoded)) {
                        seen.push(value);
                    }
                }
            }
            if (row.wrapped) {
                entry.viaSendMessageToTarget = true;
            }
            if (!allMethods.includes(row.method)) {
                allMethods.push(row.method);
            }
            if (row.method === 'Target.setAutoAttach' && row.paramsValues) {
                sessionModel.setAutoAttachParams = { ...(sessionModel.setAutoAttachParams ?? {}), ...row.paramsValues };
                sessionModel.flattened ||= row.paramsValues.flatten === true;
            }
            if (row.method === 'Target.attachToTarget' && row.paramsValues) {
                sessionModel.attachToTargetParams = { ...(sessionModel.attachToTargetParams ?? {}), ...row.paramsValues };
                sessionModel.flattened ||= row.paramsValues.flatten === true;
            }
            if (row.method === 'Target.attachToTarget') {
                sessionModel.usesExplicitAttach = true;
            }
            if (row.id !== undefined) {
                pending.set(key(row.sessionId, row.id), entry);
            }
            continue;
        }

        if (row.response) {
            const entry = pending.get(key(row.sessionId, row.id));
            pending.delete(key(row.sessionId, row.id));
            if (!entry) {
                continue;
            }
            if (row.error) {
                const text = `${row.error.code}: ${row.error.message}`;
                if (!entry.errors.includes(text)) {
                    entry.errors.push(text);
                }
                if (!errors.some((e) => e.method === entry.method && e.error === text)) {
                    errors.push({ method: entry.method, error: text });
                }
            } else {
                mergeKeys(entry.resultKeys, row.resultKeys);
            }
            continue;
        }

        if (row.method) {
            const bucket = stepOf(current).events;
            let entry = bucket.get(row.method);
            if (!entry) {
                entry = { method: row.method, count: 0, paramsKeys: [] };
                bucket.set(row.method, entry);
            }
            entry.count++;
            mergeKeys(entry.paramsKeys, row.paramsKeys);
            if (!allEvents.includes(row.method)) {
                allEvents.push(row.method);
            }
        }
    }

    sessionModel.sessionCount = sessions.size;

    const order = ['preamble', ...STEPS];
    const scenarioSteps = [...steps.keys()]
        .sort((a, b) => order.indexOf(a) - order.indexOf(b))
        .map((name) => ({
            step: name,
            skipped: meta.skipped?.[name] ?? undefined,
            note: meta.notes?.[name] ?? undefined,
            methods: [...steps.get(name).methods.values()].map((entry) => ({
                ...entry,
                paramsValues: entry.paramsValues ? collapse(entry.paramsValues) : undefined,
                errors: entry.errors.length ? entry.errors : undefined,
                resultKeys: entry.resultKeys.length ? entry.resultKeys : undefined,
            })),
            events: [...steps.get(name).events.values()],
        }))
        .filter((step) => step.methods.length || step.events.length || step.skipped);

    return {
        client: meta.client,
        clientVersion: meta.clientVersion,
        entryStyle: meta.entryStyle,
        chromeVersion: chrome.version,
        recordedAt: new Date().toISOString().slice(0, 10),
        scenario: 'connect, newContext, newPage, goto, $, $$, evaluate x2, click, waitForSelector, type, '
            + 'checkbox, select, content, title, cookies, setCookie, localStorage, console, goto page2, '
            + 'goBack, screenshot, pdf, request interception, close',
        counts: {
            distinctMethods: allMethods.length,
            distinctEvents: allEvents.length,
            domains: [...new Set([...allMethods, ...allEvents].map(domainOf))].sort(),
        },
        sessionModel,
        scenarioSteps,
        allMethods,
        allEvents,
        errors,
        notes: meta.notes,
        skipped: meta.skipped && Object.keys(meta.skipped).length ? meta.skipped : undefined,
        bestEffort: meta.bestEffort,
    };
}

// --- the matrix -----------------------------------------------------------------------------------------

// The steps that make up "connect until the first script result", and the steps that add the element path
// on top of it. Everything before `goto` is setup a client does exactly once, so it is the part an
// implementation has to answer before a client gets anywhere at all.
const MINIMUM_STEPS = ['preamble', 'connect', 'newContext', 'newPage', 'goto', 'evaluateTitle', 'evaluateObject'];
const ELEMENT_STEPS = ['querySelector', 'querySelectorAll', 'click', 'waitForSelector'];

function totalsByName(handshake) {
    const methods = new Map();
    const events = new Map();
    for (const step of handshake.scenarioSteps) {
        for (const entry of step.methods) {
            methods.set(entry.method, (methods.get(entry.method) ?? 0) + entry.count);
        }
        for (const entry of step.events) {
            events.set(entry.method, (events.get(entry.method) ?? 0) + entry.count);
        }
    }
    return { methods, events };
}

function methodsOfSteps(handshake, steps) {
    const set = new Set();
    for (const step of handshake.scenarioSteps) {
        if (steps.includes(step.step)) {
            for (const entry of step.methods) {
                set.add(entry.method);
            }
        }
    }
    return set;
}

export function buildMatrix(handshakes) {
    const names = handshakes.map((h) => h.client);
    const totals = handshakes.map(totalsByName);

    const rows = new Map(); // "kind\tname" -> per-client counts
    handshakes.forEach((handshake, index) => {
        for (const [method, count] of totals[index].methods) {
            const id = `command\t${method}`;
            if (!rows.has(id)) {
                rows.set(id, new Array(names.length).fill(0));
            }
            rows.get(id)[index] += count;
        }
        for (const [method, count] of totals[index].events) {
            const id = `event\t${method}`;
            if (!rows.has(id)) {
                rows.set(id, new Array(names.length).fill(0));
            }
            rows.get(id)[index] += count;
        }
    });

    const sorted = [...rows.entries()]
        .map(([id, counts]) => {
            const [kind, name] = id.split('\t');
            return { kind, name, domain: domainOf(name), counts };
        })
        .sort((a, b) => a.domain.localeCompare(b.domain)
            || a.kind.localeCompare(b.kind)
            || a.name.localeCompare(b.name));

    const lines = [];
    lines.push('# CDP client handshake matrix');
    lines.push('');
    lines.push('Generated by `tools/cdp-histogram` — do not edit by hand. Every row is a CDP command or event that at');
    lines.push('least one automation client sent or received while driving one Chrome build through the same canonical');
    lines.push('scenario. A cell is the number of times that client saw it across the whole scenario; blank means the');
    lines.push('client never used it. Counts are indicative (they move with timing), the **method sets** are the answer.');
    lines.push('');
    lines.push('| Client | Version | Entry | Distinct commands | Distinct events | Sessions |');
    lines.push('| --- | --- | --- | ---: | ---: | ---: |');
    for (const handshake of handshakes) {
        lines.push(`| ${handshake.client} | ${handshake.clientVersion} | \`${handshake.entryStyle}\` | `
            + `${handshake.counts.distinctMethods} | ${handshake.counts.distinctEvents} | ${handshake.sessionModel.sessionCount} |`);
    }
    lines.push('');
    lines.push(`Chrome: ${handshakes[0]?.chromeVersion ?? 'unknown'}. Recorded ${handshakes[0]?.recordedAt ?? ''}.`);
    lines.push('');
    lines.push('## Every method and event, by domain');
    lines.push('');
    lines.push(`| Domain | Kind | Method | ${names.join(' | ')} |`);
    lines.push(`| --- | --- | --- | ${names.map(() => '---').join(' | ')} |`);
    for (const row of sorted) {
        const cells = row.counts.map((count) => (count ? `✓ ${count}` : ''));
        lines.push(`| ${row.domain} | ${row.kind} | \`${row.name}\` | ${cells.join(' | ')} |`);
    }
    lines.push('');

    // The minimum must-answer set. Only over the clients that walked the canonical scenario: a best-effort
    // capture is a different scenario against a different kind of target, and folding it into an
    // intersection would quietly shrink the floor to whatever those two runs happen to share.
    const canonical = handshakes.filter((handshake) => !handshake.bestEffort);
    const minimums = canonical.map((handshake) => methodsOfSteps(handshake, MINIMUM_STEPS));
    const union = new Set(minimums.flatMap((set) => [...set]));
    const intersection = [...union].filter((method) => minimums.every((set) => set.has(method)));

    lines.push('## Minimum must-answer set for connect → newPage → goto → evaluate');
    lines.push('');
    lines.push('Derived from the `connect`, `newContext`, `newPage`, `goto` and the two `evaluate` steps only — the');
    lines.push('commands an implementation has to answer before any client gets as far as its first script result.');
    lines.push(`Over the ${canonical.length} clients that walked the canonical scenario; a best-effort capture is a`);
    lines.push('different scenario and is left out of the arithmetic.');
    lines.push('');
    lines.push(`**Every client sends these ${intersection.length}** — answer fewer and nothing works:`);
    lines.push('');
    for (const method of intersection.sort()) {
        lines.push(`- \`${method}\``);
    }
    lines.push('');
    const optional = [...union].filter((method) => !intersection.includes(method)).sort();
    lines.push(`**At least one client also sends these ${optional.length}** before its first script result:`);
    lines.push('');
    for (const method of optional) {
        const senders = canonical.filter((_, index) => minimums[index].has(method)).map((h) => h.client);
        lines.push(`- \`${method}\` — ${senders.join(', ')}`);
    }
    lines.push('');

    // What the element path adds on top.
    const elements = canonical.map((handshake) => methodsOfSteps(handshake, ELEMENT_STEPS));
    const elementUnion = [...new Set(elements.flatMap((set) => [...set]))].filter((method) => !union.has(method)).sort();
    lines.push('### What `$`, `$$`, `click` and `waitForSelector` add on top');
    lines.push('');
    lines.push('Commands that appear in those four steps and in none of the steps above.');
    lines.push('');
    for (const method of elementUnion) {
        const senders = canonical.filter((_, index) => elements[index].has(method)).map((h) => h.client);
        lines.push(`- \`${method}\` — ${senders.length === canonical.length ? 'every client' : senders.join(', ')}`);
    }
    lines.push('');

    lines.push('## Methods Chrome answered with an error');
    lines.push('');
    const errorRows = [];
    for (const handshake of handshakes) {
        for (const error of handshake.errors) {
            errorRows.push({ client: handshake.client, ...error });
        }
    }
    if (errorRows.length === 0) {
        lines.push('None: every command in every scenario got a result.');
    } else {
        lines.push('| Client | Method | Error |');
        lines.push('| --- | --- | --- |');
        for (const row of errorRows) {
            lines.push(`| ${row.client} | \`${row.method}\` | ${row.error.replace(/\|/g, '\\|')} |`);
        }
    }
    lines.push('');
    lines.push('## Per-step commands');
    lines.push('');
    lines.push('The same data sliced by scenario step, so a step whose traffic is not what its name suggests is');
    lines.push('visible. A step missing from a client\'s column was skipped by that client.');
    lines.push('');
    const stepNames = [...new Set(handshakes.flatMap((h) => h.scenarioSteps.map((s) => s.step)))];
    const order = ['preamble', ...STEPS];
    stepNames.sort((a, b) => order.indexOf(a) - order.indexOf(b));
    lines.push(`| Step | ${names.join(' | ')} |`);
    lines.push(`| --- | ${names.map(() => '---').join(' | ')} |`);
    for (const step of stepNames) {
        const cells = handshakes.map((handshake) => {
            const found = handshake.scenarioSteps.find((s) => s.step === step);
            if (!found) {
                return '—';
            }
            if (found.skipped) {
                return `skipped: ${found.skipped}`;
            }
            const methods = found.methods.map((m) => m.method);
            return methods.length ? methods.map((m) => `\`${m}\``).join(', ') : '(no commands)';
        });
        lines.push(`| ${step} | ${cells.join(' | ')} |`);
    }
    lines.push('');
    return lines.join('\n');
}
