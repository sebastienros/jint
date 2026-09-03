"""Turns a ``wptreport.json`` into the published scoreboard.

Standard library only, and no ``wptrunner`` import: the report is a finished artefact, so reading it must
work on a checkout that has never built the runner's virtualenv — which is what makes the page reproducible
from a downloaded artefact rather than only from inside the workflow that made it.

Two numbers come out of one run and they are not interchangeable.  A **test** is a document, and its status
is the harness's verdict on the whole file (``OK`` means the file ran to completion, which is not the same
as everything in it passing).  A **subtest** is one ``test()`` call inside it, and that is the number that
moves when the engine changes.  The page reports both, per suite, because a suite whose files all ``ERROR``
has a subtest count of zero and would otherwise look like a suite with nothing to fix.
"""

from __future__ import annotations

import argparse
import datetime
import json
import os
import sys
from typing import Any, Dict, Iterable, List, Mapping, Optional, Sequence

__all__ = ["Scoreboard", "read_reports", "render_markdown", "render_badge", "main"]

#: The suites the design names, longest first so `html/webappapis/` wins over `html/`.
SUITES: Sequence[str] = (
    "/custom-elements/",
    "/dom/",
    "/fetch/api/",
    "/FileAPI/",
    "/html/browsers/history/",
    "/html/dom/",
    "/html/semantics/scripting-1/",
    "/html/webappapis/",
    "/url/",
    "/xhr/",
)

#: The harness verdicts, in the order the table shows them.
TEST_STATUSES: Sequence[str] = ("OK", "ERROR", "TIMEOUT", "EXTERNAL-TIMEOUT", "CRASH", "PRECONDITION_FAILED", "SKIP")

#: The subtest verdicts, likewise.
SUBTEST_STATUSES: Sequence[str] = ("PASS", "FAIL", "TIMEOUT", "NOTRUN", "PRECONDITION_FAILED")

#: The page lives on a branch of its own, so every link in it is absolute.
_MAIN = "https://github.com/sebastienros/jint/blob/main/"

#: How a generated variant is told from a document somebody wrote.
VARIANTS: Sequence[tuple] = (
    (".any.worker.html", "dedicated worker"),
    (".any.sharedworker.html", "shared worker"),
    (".any.serviceworker.html", "service worker"),
    (".any.shadowrealm-in-window.html", "shadow realm"),
    (".any.html", "window (generated)"),
    (".window.html", "window (generated)"),
)


class Counter(dict):
    """A dict of counts that answers zero for anything it has not seen."""

    def add(self, key: str, amount: int = 1) -> None:
        self[key] = self.get(key, 0) + amount

    def of(self, key: str) -> int:
        return self.get(key, 0)

    def total(self) -> int:
        return sum(self.values())

    def merge(self, other: "Counter") -> None:
        for key, value in other.items():
            self.add(key, value)


class Group:
    """One row: a suite, or a variant kind, or the whole run."""

    def __init__(self, name: str) -> None:
        self.name = name
        self.tests = Counter()
        self.subtests = Counter()

    @property
    def test_count(self) -> int:
        return self.tests.total()

    @property
    def subtest_count(self) -> int:
        return self.subtests.total()

    @property
    def subtest_pass_ratio(self) -> Optional[float]:
        total = self.subtest_count
        return self.subtests.of("PASS") / total if total else None

    def merge(self, other: "Group") -> None:
        self.tests.merge(other.tests)
        self.subtests.merge(other.subtests)


def suite_of(test: str) -> str:
    """The suite a test URL belongs to: a configured prefix, else its first path segment."""
    for suite in sorted(SUITES, key=len, reverse=True):
        if test.startswith(suite):
            return suite.strip("/")

    head = test.lstrip("/").split("/", 1)[0]
    return head or "(root)"


def variant_of(test: str) -> str:
    """Whether a case is a document in the tree or a wrapper the server generates for a ``.js`` file."""
    path = test.split("?", 1)[0]
    for suffix, name in VARIANTS:
        if path.endswith(suffix):
            return name
    return "document"


class Scoreboard:
    """Every count a run produced, grouped the two ways the page shows them."""

    def __init__(self) -> None:
        self.suites: Dict[str, Group] = {}
        self.variants: Dict[str, Group] = {}
        self.total = Group("total")
        self.run_info: Dict[str, Any] = {}
        self.duration_seconds: Optional[float] = None

    def add_report(self, report: Mapping[str, Any]) -> None:
        if not self.run_info:
            self.run_info = dict(report.get("run_info") or {})

        start, end = report.get("time_start"), report.get("time_end")
        if isinstance(start, (int, float)) and isinstance(end, (int, float)):
            seconds = (end - start) / 1000.0
            self.duration_seconds = (self.duration_seconds or 0.0) + seconds

        for result in report.get("results") or []:
            self.add_result(result)

    def add_result(self, result: Mapping[str, Any]) -> None:
        test = result.get("test") or ""
        status = result.get("status") or "MISSING"

        for group in (
            self.suites.setdefault(suite_of(test), Group(suite_of(test))),
            self.variants.setdefault(variant_of(test), Group(variant_of(test))),
            self.total,
        ):
            group.tests.add(status)
            for subtest in result.get("subtests") or []:
                group.subtests.add(subtest.get("status") or "MISSING")


def read_reports(paths: Iterable[str]) -> Scoreboard:
    """Reads one or more ``wptreport.json`` files into a single scoreboard."""
    scoreboard = Scoreboard()
    for path in paths:
        with open(path, encoding="utf-8") as handle:
            scoreboard.add_report(json.load(handle))
    return scoreboard


# -- rendering -----------------------------------------------------------------------------------------


def _percent(ratio: Optional[float]) -> str:
    return "—" if ratio is None else f"{ratio * 100:.1f}%"


def _row(cells: Sequence[str]) -> str:
    return "| " + " | ".join(cells) + " |"


def _table(header: Sequence[str], alignment: Sequence[str], rows: Iterable[Sequence[str]]) -> List[str]:
    lines = [_row(header), _row(alignment)]
    lines.extend(_row(row) for row in rows)
    return lines


def _group_row(group: Group, label: str) -> List[str]:
    return [
        label,
        str(group.test_count),
        str(group.tests.of("OK")),
        str(group.tests.of("ERROR")),
        str(group.tests.of("TIMEOUT") + group.tests.of("EXTERNAL-TIMEOUT")),
        str(group.tests.of("SKIP")),
        str(group.subtest_count),
        str(group.subtests.of("PASS")),
        str(group.subtests.of("FAIL")),
        str(group.subtest_count - group.subtests.of("PASS") - group.subtests.of("FAIL")),
        _percent(group.subtest_pass_ratio),
    ]


_HEADER = ("Suite", "Files", "OK", "Error", "Timeout", "Skip", "Subtests", "Pass", "Fail", "Other", "Pass rate")
_ALIGN = ("---", "---:", "---:", "---:", "---:", "---:", "---:", "---:", "---:", "---:", "---:")


def render_markdown(
    scoreboard: Scoreboard,
    *,
    when: Optional[str] = None,
    jint_commit: Optional[str] = None,
    wpt_commit: Optional[str] = None,
    run_url: Optional[str] = None,
) -> str:
    """Renders the whole page, which is generated in full every night and never edited by hand."""
    when = when or datetime.datetime.now(datetime.timezone.utc).strftime("%Y-%m-%d %H:%M UTC")

    lines: List[str] = [
        "# The web-platform-tests scoreboard",
        "",
        "**Generated — do not edit.** `.github/workflows/wpt-scoreboard.yml` writes this file nightly from",
        "the `wptreport.json` of a run of *upstream's own* `wpt run`, driving `Jint.Browser` over the Chrome",
        "DevTools Protocol through the `jint_browser` product in",
        "[`tools/wpt-scoreboard/`](" + _MAIN + "tools/wpt-scoreboard/README.md).",
        "",
        "This branch holds nothing else: every file on it is generated by that workflow, which is why every",
        "link here points at `main` rather than at a sibling path.",
        "",
        "There are two web-platform-tests numbers in this repository and they measure different things.",
        "",
        "* **This one** is measured by upstream's runner, over upstream's server, on the whole of the suites",
        "  below — every file the manifest generates, including the wrappers a `.any.js` file produces for",
        "  globals this engine has no lane for. It is a scoreboard, not a gate: nothing here fails a build.",
        "* **The census** in [`Jint.Tests.Browser/Wpt/README.md`](" + _MAIN + "Jint.Tests.Browser/Wpt/README.md)",
        "  is ours. It runs a vendored subset in process, its exclusion table names every failure one at a",
        "  time, and it *is* a gate — the not-passing column only ever goes down.",
        "",
        "A number here that the census does not have is a suite nobody has vendored yet, not a disagreement.",
        "",
        "## The run",
        "",
    ]

    facts = [("Taken", when)]
    if jint_commit:
        facts.append(("Jint", f"[`{jint_commit[:12]}`](https://github.com/sebastienros/jint/commit/{jint_commit})"))
    if wpt_commit:
        facts.append(
            ("web-platform-tests", f"[`{wpt_commit[:12]}`](https://github.com/web-platform-tests/wpt/commit/{wpt_commit})")
        )
    if scoreboard.duration_seconds:
        facts.append(("Wall time", f"{scoreboard.duration_seconds / 60:.0f} min"))
    if run_url:
        facts.append(("Workflow run", f"[log and `wptreport.json`]({run_url})"))
    product = scoreboard.run_info.get("product")
    if product:
        facts.append(("Product", f"`{product}`"))

    # A list rather than a table: a two-column table with no headings renders as an empty header row.
    lines.extend(f"* **{name}** — {value}" for name, value in facts)
    lines.append("")

    total = scoreboard.total
    lines.extend(
        [
            f"**{total.subtests.of('PASS')} of {total.subtest_count} subtests pass** "
            f"({_percent(total.subtest_pass_ratio)}), over {total.test_count} files.",
            "",
            "## By suite",
            "",
        ]
    )

    rows = [_group_row(scoreboard.suites[name], name) for name in sorted(scoreboard.suites)]
    rows.append(_group_row(total, "**total**"))
    lines.extend(_table(_HEADER, _ALIGN, rows))

    lines.extend(
        [
            "",
            "## By what the case is",
            "",
            "A `.any.js` file becomes several cases: the document in the tree, and one wrapper per global its",
            "`// META: global=` names. `Jint.Browser` runs no classic dedicated worker, no shared worker and",
            "no service worker, so those wrapper rows are a floor rather than a finding — they are here so",
            "that the totals above cannot be read as a verdict on the engine's DOM.",
            "",
        ]
    )

    variant_header = ("Case", *_HEADER[1:])
    variant_rows = [_group_row(scoreboard.variants[name], name) for name in sorted(scoreboard.variants)]
    variant_rows.append(_group_row(total, "**total**"))
    lines.extend(_table(variant_header, _ALIGN, variant_rows))
    lines.append("")

    return "\n".join(lines) + "\n"


def render_badge(scoreboard: Scoreboard) -> str:
    """A [shields.io endpoint](https://shields.io/badges/endpoint-badge) document for the README's badge."""
    total = scoreboard.total
    ratio = total.subtest_pass_ratio or 0.0

    if ratio >= 0.9:
        colour = "brightgreen"
    elif ratio >= 0.75:
        colour = "green"
    elif ratio >= 0.5:
        colour = "yellow"
    else:
        colour = "orange"

    document = {
        "schemaVersion": 1,
        "label": "wpt subtests",
        "message": f"{total.subtests.of('PASS')} / {total.subtest_count} ({ratio * 100:.1f}%)",
        "color": colour,
    }
    return json.dumps(document, indent=2, sort_keys=True) + "\n"


# -- the command line ----------------------------------------------------------------------------------


def main(argv: Optional[Sequence[str]] = None) -> int:
    parser = argparse.ArgumentParser(description="Turn a wptreport.json into the published scoreboard.")
    parser.add_argument("report", nargs="+", help="wptreport.json files to total together")
    parser.add_argument("--markdown", help="where to write the page")
    parser.add_argument("--badge", help="where to write the shields.io endpoint document")
    parser.add_argument("--date", help="what to print as the run's date (default: now, in UTC)")
    parser.add_argument("--jint-commit", help="the Jint commit the browser was built from")
    parser.add_argument("--wpt-commit", help="the web-platform-tests commit the corpus was read at")
    parser.add_argument("--run-url", help="a link to the workflow run that produced the report")
    parser.add_argument(
        "--min-tests",
        type=int,
        default=1,
        help="fail if the report has fewer results than this, which is how an empty run is caught",
    )
    parser.add_argument(
        "--min-subtests",
        type=int,
        default=1,
        help=(
            "fail if the report has fewer subtests than this. A browser that died mid-run still has a "
            "result per test -- every one an error -- so the count of results alone cannot tell that apart "
            "from a run; a report in which nothing anywhere reported a subtest is not a scoreboard"
        ),
    )
    options = parser.parse_args(argv)

    scoreboard = read_reports(options.report)

    if scoreboard.total.test_count < options.min_tests:
        print(
            f"{scoreboard.total.test_count} results in the report, expected at least {options.min_tests}; "
            "the runner produced nothing worth publishing",
            file=sys.stderr,
        )
        return 1

    if scoreboard.total.subtest_count < options.min_subtests:
        print(
            f"{scoreboard.total.subtest_count} subtests in the report, expected at least "
            f"{options.min_subtests}; every document failed before it could register one, which is a broken "
            "run rather than a score",
            file=sys.stderr,
        )
        return 1

    page = render_markdown(
        scoreboard,
        when=options.date,
        jint_commit=options.jint_commit,
        wpt_commit=options.wpt_commit,
        run_url=options.run_url,
    )

    if options.markdown:
        os.makedirs(os.path.dirname(os.path.abspath(options.markdown)), exist_ok=True)
        with open(options.markdown, "w", encoding="utf-8", newline="\n") as handle:
            handle.write(page)
    else:
        sys.stdout.write(page)

    if options.badge:
        os.makedirs(os.path.dirname(os.path.abspath(options.badge)), exist_ok=True)
        with open(options.badge, "w", encoding="utf-8", newline="\n") as handle:
            handle.write(render_badge(scoreboard))

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
