"""A wptrunner executor that drives ``Jint.Browser`` over the Chrome DevTools Protocol.

``wpt run chrome`` cannot be pointed at this browser: its product module requires a ``--webdriver-binary``,
its browser class launches that ``chromedriver`` process, and every one of its executors speaks WebDriver
classic — the CDP it uses is tunnelled through ``chromedriver``'s ``goog/cdp/execute`` extension command.
``Jint.Browser`` speaks CDP and nothing else, so there is no chromedriver to put in the middle.  This is the
executor that closes that gap, and it is deliberately the smallest thing that can: it navigates a page and
reads the results the harness posts, and everything else is upstream's.

The result path is upstream's from end to end.  ``wptrunner``'s own ``testharnessreport.js`` (served at
``/resources/testharnessreport.js`` by the test environment, together with ``message-queue.js``) calls
``add_completion_callback`` and pushes ``{type: "complete", tests, status}`` onto ``__wptrunner_message_queue``;
``bridge.js`` is that queue, and hands each item to a ``Runtime.addBinding`` binding as one JSON string;
:class:`~wptrunner.executors.base.CallbackHandler` turns it into the tuple ``testharness_result_converter``
wants.  Nothing here decides whether a subtest passed.
"""

from __future__ import annotations

import json
import os
import time
from typing import Any, Dict, List, Optional

from wptrunner.executors.base import (
    CallbackHandler,
    CrashtestExecutor,
    RefTestExecutor,
    TestharnessExecutor,
)
from wptrunner.executors.protocol import BaseProtocolPart, Protocol, TestDriverProtocolPart

from .cdp import CdpConnection, CdpError, wait_for_endpoint

__all__ = [
    "JintBrowserProtocol",
    "JintBrowserTestharnessExecutor",
    "JintBrowserRefTestExecutor",
    "JintBrowserCrashtestExecutor",
]

here = os.path.dirname(__file__)

#: The global the bridge is handed and then deletes.  Anything a page could plausibly define itself would be
#: a name the page could also shadow, so it carries the runner's prefix.
BINDING_NAME = "__wptrunner_cdp_report"

with open(os.path.join(here, "bridge.js"), encoding="utf-8") as _bridge:
    BRIDGE_SOURCE = _bridge.read() % {"binding": BINDING_NAME}


class JintBrowserBaseProtocolPart(BaseProtocolPart):
    name = "base"

    def setup(self) -> None:
        self.connection = self.parent.connection
        self.session_id = self.parent.session_id

    def execute_script(self, script: str, asynchronous: bool = False) -> Any:
        if asynchronous:
            # Nothing in this executor asks for one: the results come back through the binding, not through
            # a script that resolves. Saying so is better than a wrapper nothing exercises.
            raise NotImplementedError("asynchronous execute_script is not part of this executor")

        result = self.parent.command(
            "Runtime.evaluate",
            {"expression": f"(function() {{{script}}})()", "returnByValue": True, "awaitPromise": False},
        )
        return result.get("result", {}).get("value")

    def set_timeout(self, timeout: float) -> None:
        # There is no browser-side script timeout to set; the runner's own deadline is the only one.
        pass

    def wait(self) -> bool:
        # Reached only under --pause-after-test, which has nothing to pause in a browser with no window.
        return False

    def create_window(self, type: str = "tab", **kwargs: Any) -> str:
        raise NotImplementedError("this executor drives one top-level page")

    @property
    def current_window(self) -> Optional[str]:
        return self.parent.target_id


class JintBrowserTestDriverProtocolPart(TestDriverProtocolPart):
    name = "testdriver"

    def setup(self) -> None:
        self.connection = self.parent.connection

    def run(self, url: str, script_resume: str, test_window: Optional[str] = None) -> Any:
        # The executor owns the navigate-and-wait loop, because the results arrive as events rather than as
        # the return value of a script the runner is blocked in.
        raise NotImplementedError("JintBrowserTestharnessExecutor.do_test drives the loop")

    def get_next_message(self, url: str, script_resume: str, test_window: Optional[str]) -> Any:
        raise NotImplementedError("JintBrowserTestharnessExecutor.do_test drives the loop")

    def send_message(self, cmd_id: int, message_type: str, status: str, message: Optional[str] = None) -> None:
        """Answers one ``test_driver`` action, the way ``testdriver-extra.js`` expects to be answered."""
        payload: Dict[str, Any] = {
            "cmd_id": cmd_id,
            "type": f"testdriver-{message_type}",
            "status": str(status),
        }
        if message:
            payload["message"] = str(message)

        self.parent.command(
            "Runtime.evaluate",
            {"expression": f"window.postMessage({json.dumps(payload)}, '*');", "returnByValue": True},
        )


class JintBrowserProtocol(Protocol):
    """One CDP connection, one attached page target, and the bridge installed on every document of it."""

    implements = [JintBrowserBaseProtocolPart, JintBrowserTestDriverProtocolPart]

    def __init__(self, executor, browser, endpoint_url: str, startup_timeout: float = 60.0) -> None:
        self.endpoint_url = endpoint_url
        self.startup_timeout = startup_timeout
        self.connection: Optional[CdpConnection] = None
        self.session_id: Optional[str] = None
        self.target_id: Optional[str] = None
        super().__init__(executor, browser)

    # -- lifecycle ------------------------------------------------------------------------------------

    def connect(self) -> None:
        endpoint = wait_for_endpoint(self.endpoint_url, self.startup_timeout)
        self.logger.debug(f"Connecting to {endpoint}")
        self.connection = CdpConnection(endpoint)

        created = self.connection.send("Target.createTarget", {"url": "about:blank"})
        self.target_id = created["targetId"]
        attached = self.connection.send(
            "Target.attachToTarget", {"targetId": self.target_id, "flatten": True}
        )
        self.session_id = attached["sessionId"]

    def after_connect(self) -> None:
        self.command("Page.enable")
        self.command("Runtime.enable")
        self.command("Runtime.addBinding", {"name": BINDING_NAME})
        self.command("Page.addScriptToEvaluateOnNewDocument", {"source": BRIDGE_SOURCE})

    def teardown(self) -> None:
        super().teardown()
        if self.connection is None:
            return

        try:
            if self.target_id is not None:
                self.connection.send("Target.closeTarget", {"targetId": self.target_id}, timeout=10.0)
        except Exception:  # noqa: BLE001 - teardown must not turn a finished run into a failure
            pass
        finally:
            self.connection.close()
            self.connection = None

    def is_alive(self) -> bool:
        return self.connection is not None and self.connection.is_alive()

    # -- the page -------------------------------------------------------------------------------------

    def command(self, method: str, params: Optional[Dict[str, Any]] = None, timeout: float = 30.0) -> Dict[str, Any]:
        """Sends one command on the page's session."""
        if self.connection is None:
            raise CdpError(f"{method}: not connected")
        return self.connection.send(method, params, session_id=self.session_id, timeout=timeout)

    def navigate(self, url: str, timeout: float = 30.0) -> None:
        result = self.command("Page.navigate", {"url": url}, timeout=timeout)
        if result.get("errorText"):
            raise CdpError(f"navigation to {url} failed: {result['errorText']}")

    def next_report(self, deadline: float) -> Optional[List[Any]]:
        """Waits for the bridge's next message, or ``None`` when the deadline passes with none."""
        assert self.connection is not None

        while True:
            # The disconnection marker is delivered once, so a connection that ended before this call would
            # otherwise turn every remaining test into a full-length wait for a report nothing can send.
            if not self.connection.is_alive():
                raise CdpError(f"connection ended: {self.connection.failure}")

            remaining = deadline - time.monotonic()
            if remaining <= 0:
                return None

            event = self.connection.next_event(remaining)
            if event is None:
                return None

            method = event.get("method")
            if method == "__jint.disconnected":
                raise CdpError(f"connection ended: {event['params']['reason']}")
            if method != "Runtime.bindingCalled":
                continue

            params = event.get("params", {})
            if params.get("name") != BINDING_NAME:
                continue
            if event.get("sessionId") not in (None, self.session_id):
                continue

            return json.loads(params["payload"])


class _JintBrowserExecutorMixin:
    """The connection, and the reset between documents, which both testharness and crashtest need."""

    protocol_cls = JintBrowserProtocol

    def reset(self) -> None:
        # --rerun starts the sequence again; a blank document is the cheapest way back to a known state.
        if self.protocol.connection is not None:
            self.protocol.navigate("about:blank")


class JintBrowserTestharnessExecutor(_JintBrowserExecutorMixin, TestharnessExecutor):
    # False, so wptrunner *skips* a test the manifest marks as needing `testdriver.js` and says why, rather
    # than running it into an action this executor answers "not implemented". A capability the runner lacks
    # is not a conformance failure of the engine, and the in-process lane already drives `test_driver`
    # through the same `InputDispatcher` the `Input` domain reaches. `send_message` below is implemented
    # anyway, for a document that reaches `test_driver` without carrying the flag: upstream's refusal is a
    # readable failure, and a call with no answer at all is a wait until the deadline.
    supports_testdriver = False

    def __init__(
        self,
        logger,
        browser,
        server_config,
        timeout_multiplier=1,
        debug_info=None,
        endpoint_url="http://127.0.0.1:9222",
        startup_timeout=60.0,
        **kwargs,
    ) -> None:
        TestharnessExecutor.__init__(
            self, logger, browser, server_config, timeout_multiplier=timeout_multiplier, debug_info=debug_info, **kwargs
        )
        self.protocol = JintBrowserProtocol(self, browser, endpoint_url, startup_timeout)

    def do_test(self, test):
        url = self.test_url(test)
        timeout = test.timeout * self.timeout_multiplier
        deadline = time.monotonic() + timeout + self.extra_timeout

        self.protocol.connection.drain_events()

        try:
            self.protocol.navigate(url, timeout=timeout + self.extra_timeout)
        except CdpError as error:
            return (test.make_result("ERROR", str(error)), [])

        handler = CallbackHandler(self.logger, self.protocol, None)

        while True:
            message = self.protocol.next_report(deadline)

            if message is None:
                # Nothing more will come from this document; leaving it loaded would leave its timers
                # running underneath the next one.
                self._blank()
                return (test.make_result("EXTERNAL-TIMEOUT", None), [])

            message_type, payload = message
            done, result = handler([url, message_type, payload])
            if done:
                return self.convert_result(test, result)

    def _blank(self) -> None:
        try:
            self.protocol.navigate("about:blank", timeout=10.0)
        except CdpError as error:
            self.logger.warning(f"could not reset the page after a timeout: {error}")


class JintBrowserCrashtestExecutor(_JintBrowserExecutorMixin, CrashtestExecutor):
    """A crashtest passes if the browser is still answering afterwards, which is all this can check."""

    def __init__(
        self,
        logger,
        browser,
        server_config,
        timeout_multiplier=1,
        debug_info=None,
        endpoint_url="http://127.0.0.1:9222",
        startup_timeout=60.0,
        **kwargs,
    ) -> None:
        CrashtestExecutor.__init__(
            self, logger, browser, server_config, timeout_multiplier=timeout_multiplier, debug_info=debug_info, **kwargs
        )
        self.protocol = JintBrowserProtocol(self, browser, endpoint_url, startup_timeout)

    def do_test(self, test):
        try:
            self.protocol.navigate(self.test_url(test), timeout=test.timeout * self.timeout_multiplier)
            self.protocol.command("Runtime.evaluate", {"expression": "1", "returnByValue": True})
        except CdpError as error:
            return (test.make_result("CRASH", str(error)), [])

        return self.convert_result(test, {"status": "PASS", "message": None})


class JintBrowserRefTestExecutor(RefTestExecutor):
    """There are no reftests here, and there is no honest way to pretend otherwise.

    ``Jint.Browser`` does not render, so there is nothing to screenshot and no comparison to make.  A
    reftest reaches this only if one is selected by mistake; answering ``PRECONDITION_FAILED`` says that in
    the report instead of failing the run or, worse, reporting a pass nothing measured.
    """

    def __init__(self, logger, browser, server_config, **kwargs) -> None:
        RefTestExecutor.__init__(self, logger, browser, server_config, **kwargs)
        self.protocol = None

    def setup(self, runner, protocol=None) -> None:
        self.runner = runner

    def teardown(self) -> None:
        pass

    def do_test(self, test):
        return (
            test.make_result("PRECONDITION_FAILED", "Jint.Browser renders nothing, so it runs no reftests"),
            [],
        )

    def wait(self) -> bool:
        return False
