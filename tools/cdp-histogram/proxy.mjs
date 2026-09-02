// A recording man-in-the-middle for the Chrome DevTools Protocol.
//
// It sits between an automation client and a real Chrome (or a Node inspector) and writes one JSON line per
// protocol frame. It is deliberately dumb: it never rewrites a frame, never answers a method itself, and
// never reorders anything. The only rewriting it does is in the HTTP discovery documents, where every
// ws:// URL has to point back at the proxy or the client would connect straight to the browser and the
// recording would be empty.
//
// What a recorded line holds is decided by `describeFrame` below: method names, top-level parameter KEYS,
// and — only for the handful of parameters whose VALUE decides the session model or the shape of a
// result — those values. No script source, no page content, no cookie values.

import { createServer } from 'node:http';
import { createWriteStream } from 'node:fs';
import { WebSocket, WebSocketServer } from 'ws';

// The parameters whose values are part of the answer, per method. Everything else is recorded by key only.
const RECORDED_PARAM_VALUES = {
    // The session model. waitForDebuggerOnStart is the parameter's real name on setAutoAttach;
    // waitForDebugger is its name elsewhere and is listed so a client that sends it is not missed.
    'Target.setAutoAttach': ['flatten', 'waitForDebuggerOnStart', 'waitForDebugger', 'autoAttach', 'filter'],
    'Target.attachToTarget': ['flatten'],
    'Target.setDiscoverTargets': ['discover'],
    // The shape of a result, and whether the call may run script or block.
    'Runtime.evaluate': ['awaitPromise', 'returnByValue', 'generatePreview', 'userGesture'],
    'Runtime.callFunctionOn': ['awaitPromise', 'returnByValue', 'generatePreview', 'userGesture'],
    // What a client names the world and binding it installs, which an implementation has to accept.
    'Page.createIsolatedWorld': ['worldName', 'grantUniveralAccess'],
    'Runtime.addBinding': ['name', 'executionContextName'],
    'Fetch.enable': ['patterns', 'handleAuthRequests'],
    'Emulation.setDeviceMetricsOverride': ['width', 'height', 'deviceScaleFactor', 'mobile'],
    'Debugger.setPauseOnExceptions': ['state'],
    'Debugger.setAsyncCallStackDepth': ['maxDepth'],
};

function keysOf(value) {
    return value && typeof value === 'object' && !Array.isArray(value) ? Object.keys(value) : [];
}

function recordedValues(method, params) {
    const wanted = RECORDED_PARAM_VALUES[method];
    if (!wanted || !params) {
        return undefined;
    }
    const out = {};
    let any = false;
    for (const key of wanted) {
        if (Object.hasOwn(params, key)) {
            out[key] = params[key];
            any = true;
        }
    }
    return any ? out : undefined;
}

/**
 * Starts the recording proxy in front of one CDP endpoint.
 *
 * @param {object} options
 * @param {string} options.upstreamHttp   Base HTTP URL of the browser or Node inspector, e.g. http://127.0.0.1:9222
 * @param {string} options.logPath        Where the JSON lines go.
 * @param {string} [options.client]       Name recorded in the log header.
 */
export async function startProxy({ upstreamHttp, logPath, client = 'unknown' }) {
    const upstream = new URL(upstreamHttp);
    const log = createWriteStream(logPath, { flags: 'w' });
    const started = Date.now();
    let seq = 0;

    function write(row) {
        log.write(JSON.stringify({ seq: seq++, t: Date.now() - started, ...row }) + '\n');
    }

    write({ direction: 'meta', client, upstreamHttp });

    // (sessionId, id) -> the request row, so a response can be attributed to the method that asked for it.
    const pending = new Map();
    const key = (sessionId, id) => `${sessionId ?? ''}#${id}`;

    /** Records one protocol frame. `wrapped` marks a frame that arrived inside Target.*MessageFromTarget. */
    function record(direction, text, wrapped) {
        let frame;
        try {
            frame = JSON.parse(text);
        } catch {
            write({ direction, parseError: true });
            return;
        }

        const sessionId = frame.sessionId;

        if (direction === 'c2b') {
            const row = {
                direction,
                sessionId,
                id: frame.id,
                method: frame.method,
                paramsKeys: keysOf(frame.params),
            };
            const values = recordedValues(frame.method, frame.params);
            if (values) {
                row.paramsValues = values;
            }
            if (wrapped) {
                row.wrapped = true;
            }
            write(row);
            if (frame.id !== undefined) {
                pending.set(key(sessionId, frame.id), { method: frame.method, seq: seq - 1 });
            }

            // Pre-flattened session model: the real call travels inside the params of another call.
            if (frame.method === 'Target.sendMessageToTarget' && typeof frame.params?.message === 'string') {
                record('c2b', frame.params.message, true);
            }
            return;
        }

        if (frame.id !== undefined) {
            const request = pending.get(key(sessionId, frame.id));
            pending.delete(key(sessionId, frame.id));
            const row = {
                direction,
                sessionId,
                id: frame.id,
                method: request?.method,
                response: true,
            };
            if (frame.error) {
                row.error = { code: frame.error.code, message: frame.error.message };
            } else {
                row.resultKeys = keysOf(frame.result);
            }
            if (wrapped) {
                row.wrapped = true;
            }
            write(row);
            return;
        }

        const row = {
            direction,
            sessionId,
            method: frame.method,
            event: true,
            paramsKeys: keysOf(frame.params),
        };
        if (wrapped) {
            row.wrapped = true;
        }
        write(row);

        if (frame.method === 'Target.receivedMessageFromTarget' && typeof frame.params?.message === 'string') {
            record('b2c', frame.params.message, true);
        }
    }

    // --- HTTP: the discovery documents, with every ws:// URL pointed back here -------------------------

    let publicBase = '';

    function rewrite(text) {
        // Chrome and Node both hand out absolute ws:// URLs, and the frontend URL embeds a host:port pair
        // as a query parameter (?ws=127.0.0.1:9222/devtools/page/ID) rather than as a URL.
        const hostPorts = [`${upstream.hostname}:${upstream.port}`, `localhost:${upstream.port}`, `[::1]:${upstream.port}`];
        let out = text;
        for (const hostPort of hostPorts) {
            out = out.split(`ws://${hostPort}`).join(`ws://${publicBase}`);
            out = out.split(`ws=${hostPort}`).join(`ws=${publicBase}`);
            out = out.split(`http://${hostPort}`).join(`http://${publicBase}`);
        }
        return out;
    }

    const marks = [];

    const server = createServer(async (req, res) => {
        const url = new URL(req.url, `http://${publicBase}`);

        // The scenario scripts call this between steps, so the log can be sliced per step.
        if (url.pathname === '/__mark') {
            write({ direction: 'mark', step: url.searchParams.get('name') ?? '' });
            marks.push(url.searchParams.get('name'));
            res.writeHead(200, { 'content-type': 'text/plain' });
            res.end('ok');
            return;
        }

        try {
            // No forwarded headers: Chrome's DevTools HTTP endpoint refuses a Host header it did not expect
            // (DNS-rebinding defence), and undici sets the right one for us because the target is loopback.
            const upstreamResponse = await fetch(new URL(req.url, upstream), { method: req.method === 'HEAD' ? 'GET' : req.method });
            const body = await upstreamResponse.text();
            const contentType = upstreamResponse.headers.get('content-type') ?? 'application/json; charset=UTF-8';
            const rewritten = rewrite(body);
            res.writeHead(upstreamResponse.status, { 'content-type': contentType });
            res.end(rewritten);
        } catch (error) {
            res.writeHead(502, { 'content-type': 'text/plain' });
            res.end(String(error));
        }
    });

    await new Promise((resolve) => server.listen(0, '127.0.0.1', resolve));
    const port = server.address().port;
    publicBase = `127.0.0.1:${port}`;

    // --- WebSocket: one upstream socket per client socket, same path, frames relayed verbatim ----------

    const sockets = new Set();
    const wss = new WebSocketServer({ noServer: true });

    server.on('upgrade', (req, socket, head) => {
        wss.handleUpgrade(req, socket, head, (client) => {
            const target = new URL(req.url, `ws://${upstream.hostname}:${upstream.port}`);
            target.protocol = 'ws:';
            const browser = new WebSocket(target.href, { perMessageDeflate: false, maxPayload: 512 * 1024 * 1024 });
            sockets.add(client);
            sockets.add(browser);

            const queued = [];
            let open = false;

            client.on('message', (data) => {
                const text = data.toString();
                record('c2b', text, false);
                if (open) {
                    browser.send(text);
                } else {
                    queued.push(text);
                }
            });
            browser.on('open', () => {
                open = true;
                for (const text of queued) {
                    browser.send(text);
                }
                queued.length = 0;
            });
            browser.on('message', (data) => {
                const text = data.toString();
                record('b2c', text, false);
                if (client.readyState === WebSocket.OPEN) {
                    client.send(text);
                }
            });

            const closeBoth = () => {
                try { client.close(); } catch { /* already gone */ }
                try { browser.close(); } catch { /* already gone */ }
            };
            client.on('close', closeBoth);
            browser.on('close', closeBoth);
            client.on('error', closeBoth);
            browser.on('error', (error) => {
                write({ direction: 'meta', upstreamSocketError: String(error) });
                closeBoth();
            });
        });
    });

    return {
        httpUrl: `http://127.0.0.1:${port}`,
        port,
        marks,
        async close() {
            for (const socket of sockets) {
                try { socket.terminate(); } catch { /* already gone */ }
            }
            wss.close();
            await new Promise((resolve) => server.close(resolve));
            await new Promise((resolve) => log.end(resolve));
        },
    };
}
