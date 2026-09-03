"""Fixtures for the two tests that need something running: a wpt checkout, and a browser.

Neither is downloaded or built here.  ``WPT_ROOT`` names a checkout — the workflow's, or a local clone at
the pin — and ``JINT_BROWSER_COMMAND`` names how to start the server, which is a built ``dotnet`` command
rather than a source tree so that a test run cannot silently measure a stale build.  A test that needs one
of them and does not have it **skips**, and says which variable was missing.
"""

from __future__ import annotations

import os
import re
import shlex
import subprocess
import sys
import threading
from typing import Iterator, List, Optional

import pytest

HERE = os.path.dirname(os.path.abspath(__file__))
PACKAGE_ROOT = os.path.dirname(HERE)

if PACKAGE_ROOT not in sys.path:
    sys.path.insert(0, PACKAGE_ROOT)


def _wpt_root() -> Optional[str]:
    root = os.environ.get("WPT_ROOT")
    if root and os.path.isdir(os.path.join(root, "tools", "wptrunner")):
        return os.path.abspath(root)
    return None


@pytest.fixture(scope="session")
def wpt_root() -> str:
    """A web-platform-tests checkout, with ``wptrunner`` and its vendored dependencies importable."""
    root = _wpt_root()
    if root is None:
        pytest.skip("set WPT_ROOT to a web-platform-tests checkout to run this test")

    tools = os.path.join(root, "tools")
    if tools not in sys.path:
        sys.path.insert(0, tools)
    # `localpaths` is how every wpt entry point puts wptrunner and its vendored dependencies on the path;
    # doing the same here means this test sees exactly what `wpt run` sees.
    import localpaths  # noqa: F401

    return root


class ServerProcess:
    """A running ``jint-browser serve``, with the endpoint it printed."""

    def __init__(self, process: subprocess.Popen, url: str, output: List[str]) -> None:
        self.process = process
        self.url = url
        self.output = output


_BANNER = re.compile(r"listening on (http://\S+)")


@pytest.fixture(scope="session")
def jint_browser() -> Iterator[ServerProcess]:
    """Starts one server for the session and reads its endpoint off the banner it prints."""
    command = os.environ.get("JINT_BROWSER_COMMAND")
    if not command:
        pytest.skip("set JINT_BROWSER_COMMAND to the built `jint-browser` command to run this test")

    argv = shlex.split(command, posix=os.name != "nt")
    argv += ["serve", "--port", "0", "--allow-private-network"]

    process = subprocess.Popen(  # noqa: S603
        argv,
        stdout=subprocess.PIPE,
        stderr=subprocess.STDOUT,
        text=True,
        encoding="utf-8",
        errors="replace",
        bufsize=1,
    )

    lines: List[str] = []
    found: List[str] = []
    ready = threading.Event()

    def pump() -> None:
        assert process.stdout is not None
        for line in process.stdout:
            lines.append(line.rstrip("\n"))
            match = _BANNER.search(line)
            if match and not found:
                found.append(match.group(1))
                ready.set()
        ready.set()

    reader = threading.Thread(target=pump, name="jint-browser-output", daemon=True)
    reader.start()

    if not ready.wait(120) or not found:
        process.kill()
        raise AssertionError("jint-browser serve printed no endpoint:\n" + "\n".join(lines))

    try:
        yield ServerProcess(process, found[0], lines)
    finally:
        process.terminate()
        try:
            process.wait(timeout=30)
        except subprocess.TimeoutExpired:
            process.kill()
