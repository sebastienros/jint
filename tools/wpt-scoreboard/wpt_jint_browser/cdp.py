"""The Chrome DevTools Protocol, as much of it as a test runner needs.

One socket to the browser endpoint and flattened sessions on top of it — which is not a preference, it is
the only model ``Jint.Browser`` offers: ``Target.attachToTarget`` and ``Target.setAutoAttach`` both refuse
``flatten: false``.  Every command therefore carries a ``sessionId``, and so does every event, which is what
lets one reader thread hand an event to the right waiter.

Reading happens on a thread of its own because the interesting events — ``Runtime.bindingCalled`` above all
— arrive while no command is outstanding.  A caller waits on a :class:`queue.Queue` with a deadline, so a
page that never reports is a timeout in the runner rather than a blocked reader.
"""

from __future__ import annotations

import json
import queue
import threading
import time
import urllib.request
from typing import Any, Dict, Optional

from .websocket import WebSocket, WebSocketError

__all__ = ["CdpError", "CdpConnection", "browser_endpoint"]


class CdpError(Exception):
    """A command the browser answered with an error, or a connection that ended under one."""


def browser_endpoint(http_url: str, timeout: float = 30.0) -> str:
    """Reads ``webSocketDebuggerUrl`` out of ``/json/version``, the way every recorded client does."""
    base = http_url.rstrip("/")
    with urllib.request.urlopen(base + "/json/version", timeout=timeout) as response:  # noqa: S310
        document = json.loads(response.read().decode("utf-8"))

    endpoint = document.get("webSocketDebuggerUrl")
    if not endpoint:
        raise CdpError(f"{base}/json/version named no webSocketDebuggerUrl")
    return endpoint


def wait_for_endpoint(http_url: str, deadline_seconds: float = 60.0) -> str:
    """Polls ``/json/version`` until the server answers, so a start-up race is a wait and not a failure."""
    deadline = time.monotonic() + deadline_seconds
    last: Optional[Exception] = None

    while time.monotonic() < deadline:
        try:
            return browser_endpoint(http_url, timeout=5.0)
        except Exception as error:  # noqa: BLE001 - any failure here is "not up yet" until the deadline
            last = error
            time.sleep(0.25)

    raise CdpError(f"no browser answered at {http_url} within {deadline_seconds:g}s: {last}")


class CdpConnection:
    """One connection to a browser endpoint, with a reader thread behind it."""

    def __init__(self, endpoint: str, connect_timeout: float = 30.0) -> None:
        self._socket = WebSocket(endpoint, connect_timeout=connect_timeout)
        self._next_id = 0
        self._lock = threading.Lock()
        self._pending: Dict[int, "queue.Queue[Any]"] = {}
        self._events: "queue.Queue[Dict[str, Any]]" = queue.Queue()
        self._failure: Optional[BaseException] = None
        self._stopping = False
        self._reader = threading.Thread(target=self._read_loop, name="cdp-reader", daemon=True)
        self._reader.start()

    # -- the reader -----------------------------------------------------------------------------------

    def _read_loop(self) -> None:
        try:
            while True:
                message = json.loads(self._socket.recv())
                identifier = message.get("id")

                if identifier is None:
                    self._events.put(message)
                    continue

                with self._lock:
                    waiter = self._pending.pop(identifier, None)
                if waiter is not None:
                    waiter.put(message)
        except BaseException as error:  # noqa: BLE001 - the thread's job is to record why it stopped
            if not self._stopping:
                self._failure = error
            self._fail_everyone(error)

    def _fail_everyone(self, error: BaseException) -> None:
        with self._lock:
            waiters = list(self._pending.values())
            self._pending.clear()
        for waiter in waiters:
            waiter.put({"error": {"message": f"connection ended: {error}"}})
        # Wake a caller blocked on the event queue rather than leaving it on its full deadline.
        self._events.put({"method": "__jint.disconnected", "params": {"reason": str(error)}})

    # -- commands -------------------------------------------------------------------------------------

    def send(
        self,
        method: str,
        params: Optional[Dict[str, Any]] = None,
        session_id: Optional[str] = None,
        timeout: float = 30.0,
    ) -> Dict[str, Any]:
        """Sends one command and returns its ``result``, raising :class:`CdpError` on a protocol error."""
        with self._lock:
            self._next_id += 1
            identifier = self._next_id
            waiter: "queue.Queue[Any]" = queue.Queue(maxsize=1)
            self._pending[identifier] = waiter

        message: Dict[str, Any] = {"id": identifier, "method": method, "params": params or {}}
        if session_id is not None:
            message["sessionId"] = session_id

        try:
            self._socket.send_text(json.dumps(message))
        except WebSocketError as error:
            with self._lock:
                self._pending.pop(identifier, None)
            raise CdpError(f"{method}: {error}") from error

        try:
            response = waiter.get(timeout=timeout)
        except queue.Empty as error:
            with self._lock:
                self._pending.pop(identifier, None)
            raise CdpError(f"{method}: no answer within {timeout:g}s") from error

        if "error" in response:
            detail = response["error"]
            raise CdpError(f"{method}: {detail.get('code', '')} {detail.get('message', detail)}".strip())

        return response.get("result", {})

    # -- events ---------------------------------------------------------------------------------------

    def next_event(self, timeout: float) -> Optional[Dict[str, Any]]:
        """Returns the next event, or ``None`` once ``timeout`` seconds have passed with none."""
        try:
            return self._events.get(timeout=max(timeout, 0.0))
        except queue.Empty:
            return None

    def drain_events(self) -> None:
        """Throws away every event queued so far, which is what starting a new test wants."""
        while True:
            try:
                self._events.get_nowait()
            except queue.Empty:
                return

    @property
    def failure(self) -> Optional[BaseException]:
        """Why the reader stopped, or ``None`` while the connection is healthy."""
        return self._failure

    def is_alive(self) -> bool:
        """Whether the reader thread is still reading."""
        return self._failure is None and self._reader.is_alive()

    def close(self) -> None:
        """Closes the socket and lets the reader thread end."""
        self._stopping = True
        self._socket.close()
        self._reader.join(timeout=5.0)
