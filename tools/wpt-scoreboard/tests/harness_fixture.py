"""A one-document stand-in for the wpt server, so the CDP half can be tested without the whole runner.

Everything a real ``wpt run`` serves at ``/resources/`` is served here from the same two sources it comes
from: upstream's ``testharness.js`` (the copy this repository vendors, at the pin
``Jint.Tests/Wpt/Vendor/README.md`` names) and ``wptrunner``'s own ``testharnessreport.js`` preceded by its
``message-queue.js``, formatted with the same four properties ``TestEnvironment.get_routes`` formats them
with.  Nothing in the results path is this file's own, which is the point: a fixture that shipped its own
report script would prove the fixture works.
"""

from __future__ import annotations

import http.server
import os
import threading
from typing import Optional

REPOSITORY_ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", ".."))
VENDORED_RESOURCES = os.path.join(REPOSITORY_ROOT, "Jint.Tests", "Wpt", "Vendor", "resources")

#: The values ``TestEnvironment.get_routes`` substitutes when nothing on the command line moves them.
REPORT_PROPERTIES = {"output": 0, "timeout_multiplier": 1, "explicit_timeout": "false", "debug": "false"}

SMOKE_DOCUMENT = """<!doctype html>
<meta charset="utf-8">
<title>jint-browser scoreboard smoke test</title>
<script src="/resources/testharness.js"></script>
<script src="/resources/testharnessreport.js"></script>
<div id="log"></div>
<script>
test(function () {
  assert_equals(document.title, "jint-browser scoreboard smoke test");
}, "the document is the one that was navigated to");

test(function () {
  assert_equals(document.querySelector("#log").id, "log");
}, "the harness has a DOM to render into");

async_test(function (t) {
  t.step_timeout(function () {
    t.done();
  }, 1);
}, "a timer callback resumes the harness");

test(function () {
  assert_true(false, "this subtest fails on purpose");
}, "a failing subtest is reported as a failure");
</script>
"""


def report_script(wpt_root: str) -> bytes:
    """``message-queue.js`` followed by ``wptrunner``'s ``testharnessreport.js``, exactly as wptrunner serves it."""
    executors = os.path.join(wpt_root, "tools", "wptrunner", "wptrunner", "executors")
    report = os.path.join(wpt_root, "tools", "wptrunner", "wptrunner", "testharnessreport.js")

    with open(os.path.join(executors, "message-queue.js"), encoding="utf-8") as handle:
        queue_source = handle.read()
    with open(report, encoding="utf-8") as handle:
        report_source = handle.read() % REPORT_PROPERTIES

    return (queue_source + "\n" + report_source).encode("utf-8")


class HarnessServer:
    """Serves ``/resources/*`` and one document, on loopback, on a port the operating system picks."""

    def __init__(self, wpt_root: str, document: str = SMOKE_DOCUMENT, path: str = "/smoke.html") -> None:
        report = report_script(wpt_root)
        body = document.encode("utf-8")

        class Handler(http.server.BaseHTTPRequestHandler):
            protocol_version = "HTTP/1.1"

            def do_GET(self) -> None:  # noqa: N802 - the base class names it
                if self.path == path:
                    self._answer(body, "text/html; charset=utf-8")
                elif self.path == "/resources/testharnessreport.js":
                    self._answer(report, "text/javascript; charset=utf-8")
                elif self.path.startswith("/resources/") and ".." not in self.path:
                    name = self.path[len("/resources/"):]
                    source = os.path.join(VENDORED_RESOURCES, name)
                    if os.path.isfile(source):
                        with open(source, "rb") as handle:
                            self._answer(handle.read(), "text/javascript; charset=utf-8")
                    else:
                        self.send_error(404)
                else:
                    self.send_error(404)

            def _answer(self, payload: bytes, content_type: str) -> None:
                self.send_response(200)
                self.send_header("Content-Type", content_type)
                self.send_header("Content-Length", str(len(payload)))
                self.end_headers()
                self.wfile.write(payload)

            def log_message(self, format: str, *args: object) -> None:
                pass

        self._server = http.server.ThreadingHTTPServer(("127.0.0.1", 0), Handler)
        self._thread: Optional[threading.Thread] = None
        self.path = path

    @property
    def origin(self) -> str:
        host, port = self._server.server_address[:2]
        return f"http://{host}:{port}"

    @property
    def url(self) -> str:
        return self.origin + self.path

    def __enter__(self) -> "HarnessServer":
        self._thread = threading.Thread(target=self._server.serve_forever, name="harness-fixture", daemon=True)
        self._thread.start()
        return self

    def __exit__(self, *exception: object) -> None:
        self._server.shutdown()
        self._server.server_close()
        if self._thread is not None:
            self._thread.join(timeout=5.0)
