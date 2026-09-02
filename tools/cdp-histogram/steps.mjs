// The canonical scenario, in order. Every client walks exactly this list; a client that cannot do a step
// reports it as skipped rather than reordering or substituting, so the per-step columns of the matrix
// compare like with like.
export const STEPS = [
    'connect',
    'newContext',
    'newPage',
    'goto',
    'querySelector',
    'querySelectorAll',
    'evaluateTitle',
    'evaluateObject',
    'click',
    'waitForSelector',
    'type',
    'clickCheckbox',
    'selectOption',
    'content',
    'title',
    'cookies',
    'setCookie',
    'evaluateLocalStorage',
    'consoleEvent',
    'gotoPage2',
    'goBack',
    'screenshot',
    'pdf',
    'interception',
    'close',
];

// How long a step is given to fall quiet before the next mark is written. Protocol traffic is
// asynchronous, so without this the events a step causes would be attributed to the step after it. The
// .NET harnesses hold the same number, and it has to stay the same or their columns would not compare.
export const SETTLE_MS = 250;

/**
 * Builds the two helpers the Node scenarios use.
 *
 * `mark` is an HTTP GET on the proxy, which writes the step name into the same log as the frames, so
 * slicing the log per step needs no clock comparison. `step` marks and then runs one step, recording a
 * failure as skipped instead of aborting: a client that cannot do step 21 has still told us what it sends
 * for steps 1..20 and 22..25, and losing that to one unhandled rejection would be the whole point missed.
 */
export function createMarker(proxyHttpUrl) {
    const skipped = {};

    async function mark(step) {
        await new Promise((resolve) => setTimeout(resolve, SETTLE_MS));
        await fetch(`${proxyHttpUrl}/__mark?name=${encodeURIComponent(step)}`);
    }

    async function step(name, body) {
        await mark(name);
        try {
            await body();
        } catch (error) {
            const message = String(error?.message ?? error).split(String.fromCharCode(10))[0].trim();
            skipped[name] = `${error?.constructor?.name ?? 'Error'}: ${message}`;
            console.error(`step ${name} failed: ${skipped[name]}`);
        }
    }

    return { mark, step, skipped };
}
