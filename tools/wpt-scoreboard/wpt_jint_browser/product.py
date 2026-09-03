"""The ``jint_browser`` wptrunner product.

``wptrunner`` loads external products from the ``wptrunner.products`` entry-point group, so nothing under
the ``wpt`` checkout is patched to make this work: ``pip install`` this package into the virtualenv
``wpt run`` uses and ``wpt run jint-browser …`` finds it (``wpt run`` maps the hyphen to an underscore
itself).  That mechanism is the reason this is a package beside the repository rather than a fork of wpt.

The browser class is :class:`~wptrunner.browsers.base.NullBrowser`, because this product **connects** to a
server somebody else started rather than launching one.  ``jint-browser serve`` is a .NET process with a
long start-up compared with a page load, and a run that restarted it per test group would spend most of its
budget in the runtime; the workflow starts it once, and the executor waits for the endpoint to answer.
"""

from __future__ import annotations

import argparse
import os
from typing import Any, Mapping

from wptrunner.browsers.base import NullBrowser, get_timeout_multiplier
from wptrunner.executors import executor_kwargs as base_executor_kwargs
from wptrunner.products import Product

from .executor import (
    JintBrowserCrashtestExecutor,
    JintBrowserRefTestExecutor,
    JintBrowserTestharnessExecutor,
)

__all__ = ["load"]

#: Where ``jint-browser serve`` is expected to be listening.  The environment variable is what the workflow
#: sets, so the command line stays the same whichever port the server ended up on.
DEFAULT_ENDPOINT = os.environ.get("JINT_BROWSER_URL", "http://127.0.0.1:9222")


def add_arguments(parser: argparse.ArgumentParser) -> None:
    """Adds this product's options.  Called for every registered product, so the names are prefixed."""
    group = parser.add_argument_group("jint-browser")
    group.add_argument(
        "--jint-browser-url",
        default=DEFAULT_ENDPOINT,
        help="HTTP endpoint of a running `jint-browser serve` (default: %(default)s)",
    )
    group.add_argument(
        "--jint-browser-startup-timeout",
        type=float,
        default=60.0,
        help="How long to wait for that endpoint to answer, in seconds (default: %(default)s)",
    )


def check_args(**kwargs: Any) -> None:
    """Nothing to require: there is no binary to find and no driver to install."""


def browser_kwargs(logger, test_type, run_info_data, config, **kwargs: Any) -> Mapping[str, Any]:
    return {}


def executor_kwargs(logger, test_type, test_environment, run_info_data, subsuite=None, **kwargs: Any):
    executor_kwargs = dict(base_executor_kwargs(test_type, test_environment, run_info_data, subsuite, **kwargs))
    executor_kwargs["endpoint_url"] = kwargs.get("jint_browser_url") or DEFAULT_ENDPOINT
    executor_kwargs["startup_timeout"] = kwargs.get("jint_browser_startup_timeout") or 60.0
    return executor_kwargs


def env_options() -> Mapping[str, Any]:
    """The defaults: ``web-platform.test`` on loopback, which is what ``wpt make-hosts-file`` writes.

    Nothing is overridden here on purpose.  A machine that cannot write its hosts file changes the *server's*
    configuration instead — ``config.json`` in the checkout, which ``TestEnvironment.build_config`` reads —
    and the README says how; a product that quietly moved the host would move it for the nightly run too.
    """
    return {}


def env_extras(**kwargs: Any):
    return []


def load() -> Product:
    """The entry point ``wptrunner.products`` calls; it must return a :class:`Product` named as registered."""
    return Product(
        name="jint_browser",
        browser_classes={None: NullBrowser},
        check_args=check_args,
        get_browser_kwargs=browser_kwargs,
        get_executor_kwargs=executor_kwargs,
        env_options=env_options(),
        get_env_extras=env_extras,
        get_timeout_multiplier=get_timeout_multiplier,
        executor_classes={
            "testharness": JintBrowserTestharnessExecutor,
            "crashtest": JintBrowserCrashtestExecutor,
            "reftest": JintBrowserRefTestExecutor,
        },
        add_arguments=add_arguments,
    )
