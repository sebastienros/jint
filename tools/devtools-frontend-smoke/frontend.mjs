// The Chrome DevTools front end: where to get one that matches the browser, how to host it, and how to
// read anything out of it.
//
// Two things make loading it possible at all, and both are lifted from `tools/cdp-histogram`'s best-effort
// capture, whose README explains them at length:
//
//   * Chrome refuses to navigate an ordinary page to `devtools://`, so the front end is loaded from
//     chrome-devtools-frontend.appspot.com. That host serves *by devtools-frontend commit* and only serves
//     commits that were rolled into a Chromium release, so the revision is read from the DEPS file of the
//     exact Chrome being driven rather than from the tip of the front-end repository.
//   * Chrome blocks a request from a public origin to a loopback one, and the front end's socket to Jint is
//     exactly that request. The browser that hosts the front-end page therefore runs with Local Network
//     Access checks disabled. Without it the page loads, connects to nothing, and every assertion below
//     fails for a reason that has nothing to do with Jint.
//
// The revision lookup is re-derived here rather than imported: it lives in
// `tools/cdp-histogram/scenarios/devtools-frontend.mjs`, which is a script that runs a whole capture on
// import and exports nothing. `proxy.mjs` next door *is* a module with an export, and is imported.

import puppeteer from 'puppeteer';

/** The devtools-frontend commit that shipped in this exact Chrome, read from Chromium's DEPS. */
export async function revisionFromChromiumDeps(chromeVersion) {
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

/** Picks the front-end build to load, honouring CDP_FRONTEND_REVISION and CDP_FRONTEND_URL. */
export async function resolveFrontend(chromeVersion) {
    if (process.env.CDP_FRONTEND_URL) {
        return { base: process.env.CDP_FRONTEND_URL, revision: 'from CDP_FRONTEND_URL', how: 'CDP_FRONTEND_URL environment variable' };
    }

    const { revisions, how } = process.env.CDP_FRONTEND_REVISION
        ? { revisions: [process.env.CDP_FRONTEND_REVISION], how: 'CDP_FRONTEND_REVISION environment variable' }
        : await revisionFromChromiumDeps(chromeVersion);

    const base = await firstReachable(revisions.map((sha) => `https://chrome-devtools-frontend.appspot.com/serve_rev/@${sha}/js_app.html`));
    if (!base) {
        throw new Error(
            `No hosted DevTools front-end build answered (${how}).\n`
            + 'Set CDP_FRONTEND_URL to a js_app.html that does, or CDP_FRONTEND_REVISION to a devtools-frontend commit.');
    }
    return { base, revision: revisions[0], how };
}

/** Launches the throwaway browser that hosts the front-end page. */
export async function launchHostBrowser() {
    return puppeteer.launch({
        headless: process.env.SMOKE_HEADED === '1' ? false : true,
        defaultViewport: { width: 1680, height: 1050 },
        args: [
            '--no-first-run',
            '--no-default-browser-check',
            '--disable-gpu',
            '--mute-audio',
            '--disable-background-networking',
            '--disable-component-update',
            // The front end lives on a public origin and its socket goes to loopback. Without this the
            // connection is refused with ERR_BLOCKED_BY_LOCAL_NETWORK_ACCESS_CHECKS and nothing says so.
            '--disable-features=LocalNetworkAccessChecks,PrivateNetworkAccessSendPreflights,PrivateNetworkAccessRespectPreflightResults',
        ],
    });
}

// --- reading the front end, which means piercing its shadow roots ------------------------------------------
//
// DevTools is built out of custom elements with closed-ish shadow trees; nothing interesting is reachable
// from `document.querySelector`. One helper is installed into the page before it loads and everything else
// here goes through it: `window.__smoke`, which walks every open shadow root and answers three questions —
// which elements match a selector, what their text says, and where one of them is on screen so the real
// mouse can be pointed at it.

/** Installs `window.__smoke` into every document the page loads. */
export async function installShadowDomHelper(page) {
    await page.evaluateOnNewDocument(() => {
        const normalize = (text) => (text || '').replace(/\s+/g, ' ').trim();

        // Containment across a shadow boundary: Node.contains stops at the host, so walk hosts by hand.
        const containsDeep = (ancestor, node) => {
            let current = node;
            while (current) {
                if (current === ancestor) {
                    return true;
                }
                current = current.parentNode || current.host || null;
            }
            return false;
        };

        const collect = () => {
            const out = [];
            const stack = [document];
            while (stack.length) {
                const root = stack.pop();
                let elements;
                try {
                    elements = root.querySelectorAll('*');
                } catch {
                    continue;
                }
                for (const element of elements) {
                    out.push(element);
                    if (element.shadowRoot) {
                        stack.push(element.shadowRoot);
                    }
                }
            }
            return out;
        };

        const describe = (element) => ({
            tag: element.tagName.toLowerCase(),
            class: element.getAttribute('class') || '',
            role: element.getAttribute('role') || '',
            label: element.getAttribute('aria-label') || element.getAttribute('title') || '',
            text: normalize(element.textContent).slice(0, 300),
        });

        window.__smoke = {
            /** Every element matching `selector`, in every open shadow root. */
            matching(selector) {
                return collect().filter((element) => {
                    try {
                        return element.matches(selector);
                    } catch {
                        return false;
                    }
                });
            },

            /**
             * The innermost elements matching `selector` and, when given, containing `text`. Innermost,
             * because every ancestor of a match also "contains" the text and clicking one is a coin toss.
             */
            deepest(selector, text, exact) {
                const wanted = normalize(text);
                let candidates = this.matching(selector);
                if (wanted) {
                    candidates = candidates.filter((element) => {
                        const actual = normalize(element.textContent);
                        return exact ? actual === wanted : actual.includes(wanted);
                    });
                }
                return candidates.filter((element) => !candidates.some((other) => other !== element && containsDeep(element, other)));
            },

            /**
             * What the matching elements say, deduplicated and capped. `visibleOnly` skips elements that
             * have no box — a collapsed tree keeps its children in the document, and reporting one of
             * those as "the panel shows it" would be a lie.
             */
            texts(selector, limit, visibleOnly) {
                const seen = new Set();
                for (const element of this.matching(selector)) {
                    if (visibleOnly && element.getClientRects().length === 0) {
                        continue;
                    }
                    const text = normalize(element.textContent);
                    if (text) {
                        seen.add(text.slice(0, 400));
                    }
                    if (seen.size >= (limit || 400)) {
                        break;
                    }
                }
                return [...seen];
            },

            /**
             * The whole text of the first match, untruncated. `texts` caps each entry so a dump stays
             * readable; an editor's contents are the one thing that has to come back in full.
             */
            full(selector) {
                const element = this.matching(selector)[0];
                return element ? normalize(element.textContent) : null;
            },

            /** Where to point the mouse for the first visible match, after scrolling it into view. */
            locate(selector, text, exact, index) {
                const matches = this.deepest(selector, text, exact).filter((element) => element.getClientRects().length > 0);
                const element = matches[index || 0];
                if (!element) {
                    return null;
                }
                element.scrollIntoView({ block: 'center', inline: 'nearest' });
                const box = element.getBoundingClientRect();
                return {
                    x: box.x + box.width / 2,
                    y: box.y + box.height / 2,
                    width: box.width,
                    height: box.height,
                    count: matches.length,
                    ...describe(element),
                };
            },

            /** A compact dump of the whole rendered tree, for working out what to select next. */
            outline(limit) {
                return collect()
                    .filter((element) => element.getAttribute('class') || element.getAttribute('role') || element.getAttribute('aria-label'))
                    .slice(0, limit || 4000)
                    .map((element) => {
                        const d = describe(element);
                        return `${d.tag}${d.class ? `.${d.class.split(/\s+/).join('.')}` : ''}${d.role ? `[role=${d.role}]` : ''}${d.label ? `[label=${d.label}]` : ''} :: ${d.text.slice(0, 120)}`;
                    });
            },
        };
    });
}

/** What the elements matching `selector` say; `visibleOnly` counts only what is actually on screen. */
export function deepTexts(page, selector, limit, visibleOnly = false) {
    return page.evaluate((s, l, v) => window.__smoke.texts(s, l, v), selector, limit ?? 400, visibleOnly);
}

/** The whole text of the first element matching `selector`, or null. */
export function deepFullText(page, selector) {
    return page.evaluate((s) => window.__smoke.full(s), selector);
}

/** Where the innermost match is, or null. */
export function deepLocate(page, selector, { text = '', exact = false, index = 0 } = {}) {
    return page.evaluate((s, t, e, i) => window.__smoke.locate(s, t, e, i), selector, text, exact, index);
}

/** Points the real mouse at the innermost match and clicks it. Returns what it clicked, or null. */
export async function deepClick(page, selector, options = {}) {
    const found = await deepLocate(page, selector, options);
    if (!found) {
        return null;
    }
    await page.mouse.click(found.x, found.y);
    return found;
}

/** A compact dump of everything on screen, shadow roots included. */
export function deepOutline(page, limit) {
    return page.evaluate((l) => window.__smoke.outline(l), limit ?? 4000);
}

/**
 * Polls `probe` until it answers something truthy, then returns it. A timeout says what it was waiting for,
 * because "timed out" on its own is the least useful failure a tool like this can produce.
 */
export async function waitFor(label, probe, { timeoutMs = 60000, intervalMs = 250 } = {}) {
    const deadline = Date.now() + timeoutMs;
    let lastError = null;
    for (;;) {
        try {
            const value = await probe();
            if (value) {
                return value;
            }
        } catch (error) {
            lastError = error;
        }
        if (Date.now() > deadline) {
            throw new Error(`timed out after ${Math.round(timeoutMs / 1000)}s waiting for ${label}${lastError ? ` (last error: ${lastError.message})` : ''}`);
        }
        await new Promise((r) => setTimeout(r, intervalMs));
    }
}
