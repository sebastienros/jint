// The fixture site the canonical scenario drives. Static files plus the two dynamic endpoints the page
// needs: POST /submit (the form target) and GET /api.json (the fetch on load, and the request the
// interception step intercepts).
import { createServer } from 'node:http';
import { readFile } from 'node:fs/promises';
import { fileURLToPath, pathToFileURL } from 'node:url';
import { dirname, join, normalize } from 'node:path';

const here = dirname(fileURLToPath(import.meta.url));

const types = {
    '.html': 'text/html; charset=utf-8',
    '.json': 'application/json; charset=utf-8',
    '.js': 'text/javascript; charset=utf-8',
    '.css': 'text/css; charset=utf-8',
};

/**
 * Starts the fixture server on an ephemeral loopback port.
 * @returns {Promise<{url: string, close: () => Promise<void>}>}
 */
export async function startFixture() {
    const server = createServer((req, res) => {
        const url = new URL(req.url, 'http://127.0.0.1');

        if (req.method === 'POST' && url.pathname === '/submit') {
            let body = '';
            req.on('data', (chunk) => { body += chunk; });
            req.on('end', () => {
                res.writeHead(200, { 'content-type': types['.html'] });
                res.end(`<!doctype html><html><head><meta charset="utf-8"><title>submitted</title></head>`
                    + `<body><h1>submitted</h1><pre id="body">${body.replace(/[<&]/g, '')}</pre></body></html>`);
            });
            return;
        }

        if (url.pathname === '/api.json') {
            res.writeHead(200, { 'content-type': types['.json'] });
            res.end(JSON.stringify({ ok: true, from: 'fixture' }));
            return;
        }

        const name = url.pathname === '/' ? 'index.html' : url.pathname.slice(1);
        const path = join(here, normalize(name).replace(/^(\.\.[/\\])+/, ''));
        readFile(path).then(
            (bytes) => {
                const dot = path.lastIndexOf('.');
                res.writeHead(200, { 'content-type': types[path.slice(dot)] ?? 'application/octet-stream' });
                res.end(bytes);
            },
            () => {
                res.writeHead(404, { 'content-type': 'text/plain' });
                res.end('not found');
            });
    });

    await new Promise((resolve) => server.listen(0, '127.0.0.1', resolve));
    const { port } = server.address();
    return {
        url: `http://127.0.0.1:${port}/`,
        close: () => new Promise((resolve) => server.close(resolve)),
    };
}

// Also runnable on its own, for looking at the fixture in a browser.
if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
    const fixture = await startFixture();
    console.log(fixture.url);
}
