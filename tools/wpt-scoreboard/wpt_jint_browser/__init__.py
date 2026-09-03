"""Running upstream's own web-platform-tests runner against ``Jint.Browser`` over the DevTools Protocol.

Two halves that do not depend on each other:

* the ``wptrunner`` product ``jint_browser`` (:mod:`product`, :mod:`executor`, :mod:`cdp`,
  :mod:`websocket`, ``bridge.js``), which needs ``wptrunner`` importable and is installed into the
  virtualenv ``wpt run`` builds for itself;
* :mod:`scoreboard`, which turns the ``wptreport.json`` that run produces into ``docs/wpt-scoreboard.md``
  and a badge document, and imports nothing but the standard library so it can run anywhere.
"""

__version__ = "0.1.0"
