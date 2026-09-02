// Launches the one Chrome build every client in this tool drives: the Chrome for Testing that
// `npm install puppeteer` downloaded. It is spawned as a bare child process rather than through
// puppeteer.launch(), because puppeteer.launch() would immediately open its own CDP connection and do its
// own Target.setAutoAttach on the browser session — and then "what this client sends" would be a recording
// of two clients. Nothing here talks the protocol; it only reads the port Chrome wrote down.

import { spawn } from 'node:child_process';
import { mkdtemp, readFile, rm } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import puppeteer from 'puppeteer';

const ARGS = [
    '--headless=new',
    '--remote-debugging-port=0',
    '--remote-debugging-address=127.0.0.1',
    '--no-first-run',
    '--no-default-browser-check',
    '--disable-background-networking',
    '--disable-component-update',
    '--disable-default-apps',
    '--disable-sync',
    '--disable-gpu',
    '--mute-audio',
    'about:blank',
];

async function readPort(userDataDir, timeoutMs) {
    const deadline = Date.now() + timeoutMs;
    const path = join(userDataDir, 'DevToolsActivePort');
    for (;;) {
        try {
            const text = await readFile(path, 'utf8');
            const [port, wsPath] = text.split('\n');
            if (port && wsPath) {
                return { port: Number(port), wsPath: wsPath.trim() };
            }
        } catch {
            // Chrome has not written it yet.
        }
        if (Date.now() > deadline) {
            throw new Error(`Chrome did not report a debugging port within ${timeoutMs} ms`);
        }
        await new Promise((resolve) => setTimeout(resolve, 50));
    }
}

/**
 * Spawns Chrome for Testing with an ephemeral remote-debugging port and a throwaway profile.
 * @returns {Promise<{httpUrl: string, wsUrl: string, executablePath: string, version: string, kill: () => Promise<void>}>}
 */
export async function launchChrome() {
    const executablePath = puppeteer.executablePath();
    const userDataDir = await mkdtemp(join(tmpdir(), 'cdp-histogram-'));
    const child = spawn(executablePath, [...ARGS, `--user-data-dir=${userDataDir}`], { stdio: ['ignore', 'ignore', 'pipe'] });

    let stderr = '';
    child.stderr.on('data', (chunk) => { stderr += chunk; });

    let exited = false;
    child.on('exit', () => { exited = true; });

    let port;
    try {
        ({ port } = await readPort(userDataDir, 30000));
    } catch (error) {
        throw new Error(`${error.message}\nChrome stderr:\n${stderr}`);
    }

    const httpUrl = `http://127.0.0.1:${port}`;
    const version = await (await fetch(`${httpUrl}/json/version`)).json();

    return {
        httpUrl,
        wsUrl: version.webSocketDebuggerUrl,
        executablePath,
        version: version.Browser,
        userAgent: version['User-Agent'],
        async kill() {
            // Only ever the process this module spawned, and only by handle.
            if (!exited) {
                child.kill();
                await new Promise((resolve) => {
                    const timer = setTimeout(() => { try { child.kill('SIGKILL'); } catch { /* gone */ } resolve(); }, 5000);
                    child.on('exit', () => { clearTimeout(timer); resolve(); });
                });
            }
            await rm(userDataDir, { recursive: true, force: true, maxRetries: 5 }).catch(() => { /* Windows may still hold a handle */ });
        },
    };
}
