# `wpt run` against `Jint.Browser`

A [wptrunner](https://web-platform-tests.org/tools/wptrunner/README.html) **product** called
`jint_browser`, and the script that turns the run's `wptreport.json` into
[the published scoreboard](https://github.com/sebastienros/jint/blob/wpt-scoreboard/docs/wpt-scoreboard.md)
— a page that exists once the first nightly run has written the branch it lives on, and never before.

This is the *other* web-platform-tests number in this repository, and the difference is the point:

| | measured by | runs | is it a gate? |
| --- | --- | --- | --- |
| **The scoreboard** (here) | upstream's `wpt run`, over upstream's `wptserve` | every case the manifest generates for ten suites | **no** — `.github/workflows/wpt-scoreboard.yml` is nightly and publishes a page |
| **The census** ([`Jint.Tests.Browser/Wpt/`](../../Jint.Tests.Browser/Wpt/README.md)) | our own driver, in process | a vendored subset, one exclusion row per failure | **yes** — the not-passing column only ever goes down |

Neither replaces the other. The census is the one an engine change has to keep; the scoreboard is the one
that can be compared with what other engines report, because upstream measured it.

## The spike: why this is a product plugin and not `wpt run chrome`

`wpt run chrome` cannot drive this browser, and the reason is structural rather than a missing flag.

* `wptrunner/browsers/chrome.py` calls `require_arg(kwargs, "webdriver_binary")`, and its browser class is a
  `WebDriverBrowser` that launches that `chromedriver` process and talks **WebDriver classic** to it.
* Its executors (`ChromeDriverTestharnessExecutor` and friends) reach CDP only through
  `chromedriver`'s `goog/cdp/execute` extension command — a WebDriver endpoint. There is no
  `debuggerAddress` capability anywhere in the wpt tree, so there is no supported way to point that product
  at a browser somebody else started.
* `Jint.Browser` speaks CDP and nothing else. Lightpanda ships a WebDriver front end *as well as* CDP for
  exactly this reason; this package is the other way of closing the same gap.

So: a custom product. It needs no fork of wpt, because `wptrunner.products` loads external products from the
`wptrunner.products` [entry-point group](https://web-platform-tests.org/tools/wptrunner/README.html) —
`pip install` this package into the virtualenv `wpt run` uses and `wpt run jint-browser …` finds it.
`wpt run` maps the hyphen to an underscore itself, and an unknown-but-loadable product gets
`GenericBrowserSetup`, which requires no binary and installs no driver.

**What is not re-implemented:** the harness, the report format, the result conversion, the server, the
manifest, the timeouts, and the decision about whether a subtest passed. `wptrunner`'s own
`testharnessreport.js` is what the server hands each page, and `CallbackHandler` +
`testharness_result_converter` are what turn its output into results. What this package adds is a transport.

## How a result gets out of a page

1. `Page.addScriptToEvaluateOnNewDocument` installs [`bridge.js`](wpt_jint_browser/bridge.js) on every
   document. `Jint.Browser`'s `PageTarget` commits a document by announcing the navigation, swapping the
   engine, re-installing the `Runtime.addBinding` bindings and *then* running these scripts — all before the
   document's first inline script — which is the only ordering in which any of this works.
2. `bridge.js` **is** upstream's `message-queue.js`. That file opens by returning if the queue already
   exists, because "another script already set up the testdriver infrastructure"; this is that script. So
   `testharnessreport.js` still calls `add_completion_callback` and still pushes
   `{type: "complete", tests, status}`, unchanged.
3. Instead of handing the item to an asynchronous WebDriver script, the bridge hands it to a
   `Runtime.addBinding` binding as one JSON string. It captures the binding function and deletes the global,
   so a document that enumerates `window` does not find the runner's plumbing among its results.
4. `Runtime.bindingCalled` arrives on the executor's CDP connection; `CallbackHandler` turns it into the
   tuple `testharness_result_converter` wants, with the URL the executor navigated to rather than the one
   the page reports — which is what upstream's `__wptrunner_url` dance exists to get right.

The CDP client is [`cdp.py`](wpt_jint_browser/cdp.py) over [`websocket.py`](wpt_jint_browser/websocket.py),
about 200 lines of RFC 6455 with no dependencies. That is deliberate: `wpt run` builds its own virtualenv,
and every published CDP client for Python would add a dependency chain to it. The frame codec is unit-tested
over a socket pair, because the two ways it can be wrong — an unmasked client frame and a mis-taken length
branch — both look like the browser hanging up rather than like a bug.

## What is deliberately left out

* **Reftests.** `Jint.Browser` renders nothing. `JintBrowserRefTestExecutor` answers `PRECONDITION_FAILED`
  with a sentence rather than failing the run or, worse, reporting a pass nothing measured. The workflow
  passes `--test-types testharness` so one is never reached in practice.
* **`testdriver.js` actions.** `supports_testdriver` is `False`, so wptrunner **skips** a test that needs
  one and says why, rather than failing it. Turning an element into a point needs `getClientRects` inside
  the page, which is real work; the in-process lane already maps `test_driver` onto the same
  `InputDispatcher` the `Input` domain reaches, so this is a gap in the scoreboard rather than in the
  engine. `send_message` is implemented anyway, so a document that reaches `test_driver` without the
  manifest flag gets upstream's "not implemented" answer instead of hanging until its timeout.
* **HTTPS.** `Jint.Browser` has no way to be told about an extra certificate authority, so a run over wpt's
  pregenerated certificates would fail every `https` case for the environment's reason. The workflow passes
  `--ssl-type=none`, and wptrunner then leaves https tests out of the run entirely
  (`include_https=ssl_enabled`) rather than running them into a wall. Installing wpt's CA into the runner's
  trust store would lift this; it is the obvious next step and is not in v1.
* **`--untrusted`.** The nightly does not pass it. `BrowserOptions.ForUntrustedContent` applies
  `UntrustedCodeLimits.Default` to every page engine — one second per engine entry, 50 000 statements,
  10 000-element arrays, recursion depth 64, a 250 ms regex budget — and the command line has no switch for
  any of the five. Those are the right numbers for a page nobody vouches for and the wrong ones for a
  conformance measurement: a scoreboard taken under them would be a report on the hardened profile. What is
  served is vendored, pinned content on loopback.

## Running it yourself

The nightly is `.github/workflows/wpt-scoreboard.yml` and it is the specification; this is the same thing
by hand.

```bash
# 1. a wpt checkout at the pin Jint.Tests/Wpt/Vendor/README.md names
git clone https://github.com/web-platform-tests/wpt.git
git -C wpt checkout <pin>
cp tools/wpt-scoreboard/wpt-config.json wpt/config.json
(cd wpt && ./wpt make-hosts-file | sudo tee -a /etc/hosts)

# 2. a virtualenv with this product in it, built with the python ./wpt will run under
python3 -m venv .wpt-venv
.wpt-venv/bin/pip install -e tools/wpt-scoreboard pytest

# 3. the browser
dotnet build Jint.Browser.Tool/Jint.Browser.Tool.csproj -c Release -f net10.0
dotnet artifacts/bin/Jint.Browser.Tool/release_net10.0/Jint.Browser.Tool.dll serve --max-task-duration 0 &

# 4. the run
cd wpt && ./wpt --venv ../.wpt-venv run jint-browser \
  --ssl-type none --no-manifest-download --processes 1 --test-types testharness \
  --no-fail-on-unexpected \
  --log-wptreport ../wptreport.json --log-mach - \
  dom/

# 5. the page
.wpt-venv/bin/python -m wpt_jint_browser.scoreboard wptreport.json --markdown docs/wpt-scoreboard.md
```

### What the run costs, and the flag that decides it

The wall time is dominated by the cases that fail by **not reporting**: a document that never completes
costs its whole timeout, and a `<meta name=timeout content=long>` file's timeout is 60 seconds.
`html/semantics/scripting-1/the-script-element/moving-between-documents/` is 52 of those, and at the time of
writing every one of them times out on the same defect — a `<script src>` whose fetch fails fires neither
`load` nor `error`, so the harness never finishes — so that one directory is most of an hour.

**There is therefore no `--timeout-multiplier`**, and the reason is not only cost. Every per-test timeout in
the corpus is the test author's, and wpt.fyi's published figures are taken at the default of 1; a scoreboard
measured at 2 would be systematically more generous than the numbers it invites comparison with. Raising it
doubles exactly the cases that are already failing and changes nothing about the ones that pass.

### If the nightly gets too long

The run restarts the test runner after almost every test, and that is not a defect: `restart_on_unexpected`
defaults to on, this repository ships no wpt expectation metadata, so *every* failure is unexpected. It
costs about a second per failing test — measured at roughly 15% of the wall time. `--no-restart-on-unexpected`
removes it, and is safe here for a reason that is specific to this browser rather than general: a navigation
already disposes the previous document's engine and cancels everything it had in flight, and a runner restart
only mints a new page target rather than a new process, so the isolation the flag buys is isolation the next
`Page.navigate` gives anyway. It is not passed today because upstream's default is what a reader expects.

### `wpt-config.json`, and why it exists

It is copied into the checkout as its `config.json`, which is the override
`TestEnvironment.build_config` reads before it starts the servers. All it does is null three ports out:
`ConfigBuilder._get_ports` skips the bare `https` and `wss` schemes when SSL is off, but **not**
`https-local`, `https-public` or `h2`. Left in, those three try to start with no certificate and the whole
environment dies with `Servers failed to start: https-local:8445, https-public:8446`. The four `.h2.` test
files are excluded by name in the workflow for the same reason, so that they are absent rather than internal
errors.

### On a machine whose hosts file you cannot write

`wpt run` refuses to start without the hosts entries, and that refusal is right — a third of `fetch/api` and
`xhr` need a second origin. If you cannot have them, you can get a run out of the tree by pointing the
*server* at a wildcard-loopback domain in the same `config.json`, and calling `wptrunner` directly rather
than through `wpt run`:

```json
{
  "browser_host": "web-platform.localtest.me",
  "alternate_hosts": { "alt": "not-web-platform.localtest.me" },
  "ports": { "https-local": [null], "https-public": [null], "h2": [null] }
}
```

Every name under `localtest.me` resolves to `127.0.0.1` in public DNS. It is correct and it is *slow* —
every subresource is a DNS round trip, which multiplies a page load by about ten — so it is a way to
develop, never a way to measure.

## The tests

`tests/` runs under `pytest` and is what the workflow runs before anything else.

* `test_websocket.py` — the frame codec over a socket pair. No browser, no server, no environment
  variables; it always runs.
* `test_scoreboard.py` — a report in, a page out. Likewise.
* `test_executor_smoke.py` — the whole path against a real `jint-browser serve`, on one document with three
  passing subtests and one that fails on purpose. It needs `WPT_ROOT` (a wpt checkout, for `testharness.js`
  and `wptrunner`'s report script) and `JINT_BROWSER_COMMAND` (how to start the built tool), and **skips**
  naming the missing one when it does not have them.

```bash
WPT_ROOT=$PWD/wpt \
JINT_BROWSER_COMMAND="dotnet $PWD/artifacts/bin/Jint.Browser.Tool/release_net10.0/Jint.Browser.Tool.dll" \
  .wpt-venv/bin/python -m pytest tools/wpt-scoreboard/tests -q
```

## What this is pinned against

`wptrunner`'s external-product contract is young: `products.Product`, `get_all_products()` and the
`wptrunner.products` entry-point group are what `product.py` builds against, and `Product` is constructed
directly rather than through the older `__wptrunner__` dict so that a field it grows is a `TypeError` here
rather than a silent default. The workflow always checks wpt out at the corpus pin, so an upstream change to
that contract arrives with a deliberate pin bump and not on a random night.
