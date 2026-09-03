"""The executor against a real ``jint-browser serve``, on one known-good document.

This is the test the nightly workflow runs before it runs anything else, and it is the only one that can
tell "the scoreboard is zero because the engine fails everything" from "the scoreboard is zero because the
runner never reached the page".  A document with three passing subtests and one deliberately failing one is
enough for that: a bridge that reported nothing would time out, and a bridge that reported optimistically
would not have the failure in it.
"""

from __future__ import annotations

import pytest

from harness_fixture import HarnessServer
from wpt_jint_browser.cdp import CdpConnection, browser_endpoint

pytestmark = pytest.mark.usefixtures("wpt_root")


@pytest.fixture
def page(jint_browser):
    """A page target with the bridge installed, driven the way the executor drives it."""
    from wpt_jint_browser.executor import BINDING_NAME, BRIDGE_SOURCE

    connection = CdpConnection(browser_endpoint(jint_browser.url))
    try:
        target = connection.send("Target.createTarget", {"url": "about:blank"})["targetId"]
        session = connection.send("Target.attachToTarget", {"targetId": target, "flatten": True})["sessionId"]

        connection.send("Page.enable", session_id=session)
        connection.send("Runtime.enable", session_id=session)
        connection.send("Runtime.addBinding", {"name": BINDING_NAME}, session_id=session)
        connection.send("Page.addScriptToEvaluateOnNewDocument", {"source": BRIDGE_SOURCE}, session_id=session)

        yield connection, session
    finally:
        connection.close()


def _report(connection, session, url, timeout=60.0):
    import json
    import time

    result = connection.send("Page.navigate", {"url": url}, session_id=session)
    assert not result.get("errorText"), result.get("errorText")

    deadline = time.monotonic() + timeout
    while time.monotonic() < deadline:
        event = connection.next_event(deadline - time.monotonic())
        if event is None:
            break
        if event.get("method") == "Runtime.bindingCalled" and event["params"].get("name") == "__wptrunner_cdp_report":
            return json.loads(event["params"]["payload"])

    raise AssertionError(f"no report from {url} within {timeout:g}s")


def test_the_harness_reports_through_the_binding(page, wpt_root):
    connection, session = page

    with HarnessServer(wpt_root) as server:
        message_type, payload = _report(connection, session, server.url)

    assert message_type == "complete"

    status, message, _stack, subtests = payload
    assert status == 0, f"the harness did not finish cleanly: {message}"

    by_name = {name: (subtest_status, subtest_message) for name, subtest_status, subtest_message, _ in subtests}
    assert len(by_name) == 4, by_name

    assert by_name["the document is the one that was navigated to"][0] == 0
    assert by_name["the harness has a DOM to render into"][0] == 0
    assert by_name["a timer callback resumes the harness"][0] == 0

    failing_status, failing_message = by_name["a failing subtest is reported as a failure"]
    assert failing_status == 1, "a failing subtest must arrive as a failure, not be swallowed"
    assert "on purpose" in (failing_message or "")


def test_the_bridge_leaves_no_binding_on_the_global(page, wpt_root):
    """The runner's plumbing must not show up in a document's own enumeration of ``window``."""
    connection, session = page

    with HarnessServer(wpt_root) as server:
        _report(connection, session, server.url)

        answer = connection.send(
            "Runtime.evaluate",
            {"expression": "typeof window.__wptrunner_cdp_report", "returnByValue": True},
            session_id=session,
        )

    assert answer["result"]["value"] == "undefined"


def test_the_payload_converts_the_way_wptrunner_expects(page, wpt_root):
    """The seam between :mod:`bridge` and upstream's converter, which nothing else exercises."""
    from wptrunner.executors.base import CallbackHandler, strip_server

    connection, session = page

    with HarnessServer(wpt_root) as server:
        url = server.url
        message_type, payload = _report(connection, session, url)

    handler = CallbackHandler(_NullLogger(), None, None)
    done, result = handler([url, message_type, payload])

    assert done
    result_url, status, _message, _stack, subtests = result
    assert result_url == strip_server(url)
    assert status == 0
    assert sorted(subtest[1] for subtest in subtests) == [0, 0, 0, 1]


class _NullLogger:
    def debug(self, *args, **kwargs):
        pass

    def warning(self, *args, **kwargs):
        pass

    def info(self, *args, **kwargs):
        pass
