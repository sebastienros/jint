// Records every client, one after another, each against its own freshly launched Chrome, then rebuilds the
// matrix. Serial on purpose: two Chromes and two clients at once would make the per-step slicing a race.
//
//   node run-all.mjs                 # every client
//   node run-all.mjs --only=playwright-node
//   node run-all.mjs --skip=devtools-frontend
//   node run-all.mjs --matrix-only   # rebuild matrix.md from the checked-in results

import { spawn } from 'node:child_process';
import { mkdir, readFile, writeFile, readdir } from 'node:fs/promises';
import { existsSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { dirname, join } from 'node:path';
import { startFixture } from './fixture/serve.mjs';
import { startProxy } from './proxy.mjs';
import { launchChrome } from './chrome.mjs';
import { summarize, buildMatrix } from './analyze.mjs';

const here = dirname(fileURLToPath(import.meta.url));
const rawDir = join(here, 'raw');
const outDir = join(here, '..', 'devtools-protocol', 'handshakes');

const args = Object.fromEntries(process.argv.slice(2).map((a) => a.split('=', 2)).map(([k, v]) => [k.replace(/^--/, ''), v ?? true]));

const DOTNET_ROOT = join(here, 'dotnet');

const CLIENTS = [
    {
        name: 'puppeteer-node',
        run: (ctx) => node(['scenarios/puppeteer.mjs', `--entry=ws`, `--proxy=${ctx.proxy}`, `--fixture=${ctx.fixture}`, `--meta=${ctx.meta}`]),
    },
    {
        name: 'puppeteer-node-browserURL',
        run: (ctx) => node(['scenarios/puppeteer.mjs', `--entry=url`, `--proxy=${ctx.proxy}`, `--fixture=${ctx.fixture}`, `--meta=${ctx.meta}`]),
    },
    {
        name: 'playwright-node',
        run: (ctx) => node(['scenarios/playwright.mjs', `--proxy=${ctx.proxy}`, `--fixture=${ctx.fixture}`, `--meta=${ctx.meta}`]),
    },
    {
        name: 'puppeteersharp-dotnet',
        project: join(DOTNET_ROOT, 'Histogram.PuppeteerSharp', 'Histogram.PuppeteerSharp.csproj'),
        run: (ctx) => dotnet(ctx.self.project, 'Histogram.PuppeteerSharp', ctx),
    },
    {
        name: 'playwright-dotnet',
        project: join(DOTNET_ROOT, 'Histogram.Playwright', 'Histogram.Playwright.csproj'),
        run: (ctx) => dotnet(ctx.self.project, 'Histogram.Playwright', ctx),
    },
];

function run(command, argv, options = {}) {
    return new Promise((resolve) => {
        const child = spawn(command, argv, { cwd: options.cwd ?? here, stdio: ['ignore', 'pipe', 'pipe'], shell: false });
        let out = '';
        child.stdout.on('data', (c) => { out += c; process.stdout.write(c); });
        child.stderr.on('data', (c) => { out += c; process.stderr.write(c); });
        child.on('error', (error) => resolve({ code: -1, out: `${out}\n${error.message}` }));
        child.on('exit', (code) => resolve({ code, out }));
    });
}

const node = (argv) => run(process.execPath, argv);

async function dotnet(project, assembly, ctx) {
    const build = await run('dotnet', ['build', project, '-c', 'Release', '-nr:false', '-v', 'minimal']);
    if (build.code !== 0) {
        return { code: build.code, out: build.out };
    }
    const dir = join(dirname(project), 'bin', 'Release');
    const tfms = existsSync(dir) ? await readdir(dir) : [];
    const exe = tfms.map((tfm) => join(dir, tfm, `${assembly}.exe`)).find(existsSync)
        ?? tfms.map((tfm) => join(dir, tfm, assembly)).find(existsSync);
    if (!exe) {
        return { code: -1, out: `no built binary under ${dir}` };
    }
    return run(exe, [`--proxy=${ctx.proxy}`, `--fixture=${ctx.fixture}`, `--meta=${ctx.meta}`]);
}

async function record(client, fixtureUrl) {
    console.log(`\n=== ${client.name} ===`);
    const chrome = await launchChrome();
    const logPath = join(rawDir, `${client.name}.jsonl`);
    const metaPath = join(rawDir, `${client.name}.meta.json`);
    await writeFile(metaPath, JSON.stringify({ client: client.name, clientVersion: 'unknown', entryStyle: 'unknown', notes: {}, skipped: {} }));
    const proxy = await startProxy({ upstreamHttp: chrome.httpUrl, logPath, client: client.name });
    console.log(`chrome ${chrome.version} at ${chrome.httpUrl}, proxy at ${proxy.httpUrl}`);

    let result;
    try {
        result = await client.run({ proxy: proxy.httpUrl, fixture: fixtureUrl, meta: metaPath, self: client });
    } finally {
        await proxy.close();
        await chrome.kill();
    }

    const meta = JSON.parse(await readFile(metaPath, 'utf8'));
    if (result.code !== 0) {
        meta.failed = `scenario exited with ${result.code}`;
        console.error(`!! ${client.name} exited with ${result.code}`);
    }
    const handshake = await summarize(logPath, meta, chrome);
    if (meta.failed) {
        handshake.incomplete = meta.failed;
    }
    await writeFile(join(outDir, `${client.name}.json`), `${JSON.stringify(handshake, null, 2)}\n`);
    console.log(`${client.name}: ${handshake.counts.distinctMethods} commands, ${handshake.counts.distinctEvents} events`);
    return result.code === 0;
}

// Column order in the matrix. Not alphabetical: the two Puppeteer entry styles belong side by side, the
// two Playwright ones likewise, and a best-effort capture is not the same scenario so it goes last.
const COLUMN_ORDER = [
    'puppeteer-node',
    'puppeteer-node-browserURL',
    'puppeteersharp-dotnet',
    'playwright-node',
    'playwright-dotnet',
    'devtools-frontend',
];

async function rebuildMatrix() {
    const files = (await readdir(outDir)).filter((f) => f.endsWith('.json')).sort();
    const handshakes = [];
    for (const file of files) {
        handshakes.push(JSON.parse(await readFile(join(outDir, file), 'utf8')));
    }
    handshakes.sort((a, b) => {
        const rank = (h) => {
            const index = COLUMN_ORDER.indexOf(h.client);
            return index < 0 ? COLUMN_ORDER.length : index;
        };
        return rank(a) - rank(b) || a.client.localeCompare(b.client);
    });
    await writeFile(join(outDir, 'matrix.md'), `${buildMatrix(handshakes)}`);
    console.log(`\nmatrix.md: ${handshakes.length} clients`);
}

await mkdir(rawDir, { recursive: true });
await mkdir(outDir, { recursive: true });

if (args['matrix-only']) {
    await rebuildMatrix();
    process.exit(0);
}

const skip = String(args.skip ?? '').split(',').filter(Boolean);
const only = args.only ? String(args.only).split(',') : null;
const fixture = await startFixture();
console.log(`fixture at ${fixture.url}`);

let failures = 0;
for (const client of CLIENTS) {
    if (skip.includes(client.name) || (only && !only.includes(client.name))) {
        continue;
    }
    if (!await record(client, fixture.url)) {
        failures++;
    }
}

await fixture.close();

if (!skip.includes('devtools-frontend') && (!only || only.includes('devtools-frontend'))) {
    console.log('\n=== devtools-frontend (best effort) ===');
    const result = await node(['scenarios/devtools-frontend.mjs', `--out=${join(outDir, 'devtools-frontend.json')}`, `--raw=${rawDir}`]);
    if (result.code !== 0) {
        console.error('devtools-frontend capture failed; see the file it wrote for what it got');
    }
}

await rebuildMatrix();
process.exit(failures === 0 ? 0 : 1);
