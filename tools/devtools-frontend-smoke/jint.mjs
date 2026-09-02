// Spawns the Jint REPL under --inspect and reads the endpoint back out of its banner.
//
// The banner is the contract this file depends on. `Jint.Repl` prints
//
//     Debugger listening on ws://127.0.0.1:54796/devtools/page/5EB8F253FFCD410A8AC91A0D00000002
//
// on stdout before it evaluates anything, so the host, the port and the target identifier are all readable
// without asking the server anything. `/json/list` carries the same three, and is read afterwards as a
// cross-check: a banner that disagrees with the discovery document is a defect worth failing on rather than
// a detail to paper over.

import { spawn } from 'node:child_process';
import { existsSync } from 'node:fs';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const here = dirname(fileURLToPath(import.meta.url));
const repoRoot = resolve(here, '..', '..');

/** Where a Release build of the REPL lands, in the order they are tried. */
const CANDIDATES = [
    join(repoRoot, 'artifacts', 'bin', 'Jint.Repl', 'release', 'Jint.Repl.exe'),
    join(repoRoot, 'artifacts', 'bin', 'Jint.Repl', 'release', 'Jint.Repl'),
    join(repoRoot, 'artifacts', 'bin', 'Jint.Repl', 'release', 'Jint.Repl.dll'),
];

const BUILD_COMMAND = `dotnet build -c Release ${join(repoRoot, 'Jint.Repl', 'Jint.Repl.csproj')}`;

const BANNER = /Debugger listening on ws:\/\/([^\s:]+):(\d+)(\/devtools\/page\/(\S+))/;

/** Resolves the binary to run, honouring JINT_REPL, and says how to produce it when there is none. */
function resolveBinary() {
    const override = process.env.JINT_REPL;
    const candidates = override ? [override] : CANDIDATES;
    const found = candidates.find((path) => existsSync(path));

    if (!found) {
        const tried = candidates.map((path) => `  ${path}`).join('\n');
        throw new Error(
            `No Jint REPL binary found. Tried:\n${tried}\n\n`
            + `Build one with:\n  ${BUILD_COMMAND}\n\n`
            + 'or point JINT_REPL at an existing Jint.Repl.exe / Jint.Repl.dll.');
    }

    // A .dll is the portable form and needs the muxer; anything else is an apphost.
    return found.endsWith('.dll')
        ? { command: 'dotnet', args: [found], described: `dotnet ${found}` }
        : { command: found, args: [], described: found };
}

/**
 * Starts `Jint.Repl --inspect=0 -f <script> -t <timeoutSeconds>` and waits for its banner.
 *
 * @param {object} options
 * @param {string} options.script            Absolute path to the script the engine runs.
 * @param {number} [options.timeoutSeconds]  What -t is given; the REPL's per-entry execution-time limit.
 * @param {number} [options.startupTimeoutMs]
 */
export async function launchJint({ script, timeoutSeconds = 60, startupTimeoutMs = 30000 }) {
    const binary = resolveBinary();
    const argv = [...binary.args, '--inspect=0', '-f', script, '-t', String(timeoutSeconds)];
    const child = spawn(binary.command, argv, { cwd: here, stdio: ['ignore', 'pipe', 'pipe'] });

    const stdout = [];
    const stderr = [];
    let banner = '';
    let exited = null;

    child.stdout.setEncoding('utf8');
    child.stderr.setEncoding('utf8');
    child.stdout.on('data', (chunk) => { banner += chunk; stdout.push(chunk); });
    child.stderr.on('data', (chunk) => { stderr.push(chunk); });
    child.on('exit', (code) => { exited = code; });

    const deadline = Date.now() + startupTimeoutMs;
    let match = null;
    for (;;) {
        match = BANNER.exec(banner);
        if (match) {
            break;
        }
        if (exited !== null) {
            throw new Error(
                `${binary.described} exited with ${exited} before printing a debugger banner.\n`
                + `stdout:\n${stdout.join('')}\nstderr:\n${stderr.join('')}`);
        }
        if (Date.now() > deadline) {
            child.kill();
            throw new Error(
                `${binary.described} printed no debugger banner within ${startupTimeoutMs} ms.\n`
                + `stdout so far:\n${stdout.join('')}\nstderr so far:\n${stderr.join('')}`);
        }
        await new Promise((r) => setTimeout(r, 50));
    }

    const [, host, port, wsPath, targetId] = match;
    const httpUrl = `http://${host}:${port}`;

    // The discovery document has to agree with the banner, because the two are produced by different code
    // and a client reaches the target through either.
    const targets = await (await fetch(`${httpUrl}/json/list`)).json();
    const target = targets.find((entry) => entry.id === targetId);
    if (!target) {
        child.kill();
        throw new Error(`${httpUrl}/json/list does not list the target the banner named (${targetId}): ${JSON.stringify(targets)}`);
    }

    return {
        command: binary.described,
        argv,
        host,
        port: Number(port),
        httpUrl,
        wsPath,
        targetId,
        target,
        output: () => stdout.join(''),
        errors: () => stderr.join(''),
        exitCode: () => exited,
        kill() {
            // Only ever the process this module spawned, and only by handle.
            if (exited === null) {
                try { child.kill(); } catch { /* already gone */ }
            }
        },
    };
}
