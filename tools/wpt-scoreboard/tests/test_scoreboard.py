"""A report in, a page out.

The generator has no browser and no runner behind it, so these are the tests that can say what the published
page will actually claim.  The two that matter are the grouping rules — a case belongs to exactly one suite
and exactly one kind — and the refusal to publish a run that produced almost nothing, which is the only
thing standing between an infrastructure failure and a scoreboard that silently reads zero.
"""

from __future__ import annotations

import json

import pytest

from wpt_jint_browser import scoreboard as sb


def _report(*results, **extra):
    document = {
        "run_info": {"product": "jint_browser", "os": "linux"},
        "time_start": 1_000_000,
        "time_end": 1_060_000,
        "results": list(results),
    }
    document.update(extra)
    return document


def _result(test, status="OK", subtests=()):
    return {
        "test": test,
        "status": status,
        "message": None,
        "subtests": [{"name": name, "status": state, "message": None} for name, state in subtests],
    }


def _scoreboard(*results):
    board = sb.Scoreboard()
    board.add_report(_report(*results))
    return board


def test_a_case_belongs_to_the_longest_matching_suite():
    assert sb.suite_of("/html/webappapis/timers/type-long-setinterval.any.html") == "html/webappapis"
    assert sb.suite_of("/html/dom/aria-attribute-reflection.html") == "html/dom"
    assert sb.suite_of("/dom/events/Event-constructors.any.html") == "dom"
    assert sb.suite_of("/FileAPI/blob/Blob-constructor.any.html") == "FileAPI"


def test_a_case_outside_the_named_suites_falls_back_to_its_first_segment():
    assert sb.suite_of("/streams/readable-streams/general.any.html") == "streams"


def test_generated_wrappers_are_told_from_documents():
    assert sb.variant_of("/dom/events/Event-constructors.any.html") == "window (generated)"
    assert sb.variant_of("/dom/events/Event-constructors.any.worker.html") == "dedicated worker"
    assert sb.variant_of("/xhr/status.window.html") == "window (generated)"
    assert sb.variant_of("/dom/events/Event-propagation.html") == "document"
    assert sb.variant_of("/url/url-setters.any.html?include=file") == "window (generated)"


def test_counts_are_totalled_over_suites_and_over_kinds():
    board = _scoreboard(
        _result("/dom/a.any.html", subtests=[("one", "PASS"), ("two", "FAIL")]),
        _result("/dom/a.any.worker.html", status="ERROR"),
        _result("/url/b.any.html", subtests=[("three", "PASS")]),
    )

    assert board.total.test_count == 3
    assert board.total.subtest_count == 3
    assert board.total.subtests.of("PASS") == 2
    assert board.suites["dom"].test_count == 2
    assert board.suites["url"].subtests.of("PASS") == 1
    assert board.variants["dedicated worker"].tests.of("ERROR") == 1
    assert board.variants["window (generated)"].subtest_count == 3


def test_a_suite_whose_files_all_error_is_visible_rather_than_absent():
    """Zero subtests and no pass rate, which is a different statement from "everything failed"."""
    board = _scoreboard(_result("/xhr/a.any.html", status="ERROR"))

    assert board.suites["xhr"].subtest_count == 0
    assert board.suites["xhr"].subtest_pass_ratio is None
    assert "—" in sb.render_markdown(board)


def test_the_page_names_both_numbers_and_links_absolutely():
    page = sb.render_markdown(_scoreboard(_result("/dom/a.any.html", subtests=[("one", "PASS")])))

    assert "Jint.Tests.Browser/Wpt/README.md" in page
    assert "](../" not in page, "the page lives on a branch of its own, so no link may be relative"
    assert "## By suite" in page
    assert "## By what the case is" in page


def test_the_page_carries_the_run_it_came_from():
    page = sb.render_markdown(
        _scoreboard(_result("/dom/a.any.html", subtests=[("one", "PASS")])),
        when="2026-09-03 04:21 UTC",
        jint_commit="0123456789abcdef0123456789abcdef01234567",
        wpt_commit="6c7127bdd9f2cc6a3668fd9791757843e09d5a9e",
        run_url="https://github.com/sebastienros/jint/actions/runs/1",
    )

    assert "2026-09-03 04:21 UTC" in page
    assert "0123456789ab" in page
    assert "6c7127bdd9f2" in page
    assert "actions/runs/1" in page


def test_the_badge_reports_the_subtest_pass_rate():
    board = _scoreboard(
        _result("/dom/a.any.html", subtests=[("one", "PASS"), ("two", "PASS"), ("three", "FAIL")])
    )
    badge = json.loads(sb.render_badge(board))

    assert badge["schemaVersion"] == 1
    assert badge["message"].startswith("2 / 3")
    assert badge["color"] == "yellow"


def test_an_empty_run_is_refused_rather_than_published(tmp_path):
    path = tmp_path / "wptreport.json"
    path.write_text(json.dumps(_report()), encoding="utf-8")

    assert sb.main([str(path), "--markdown", str(tmp_path / "out.md")]) == 1
    assert not (tmp_path / "out.md").exists()


def test_a_real_run_is_written_where_it_was_asked_for(tmp_path):
    path = tmp_path / "wptreport.json"
    path.write_text(
        json.dumps(_report(_result("/dom/a.any.html", subtests=[("one", "PASS")]))),
        encoding="utf-8",
    )

    markdown = tmp_path / "docs" / "wpt-scoreboard.md"
    badge = tmp_path / "badge.json"

    assert sb.main([str(path), "--markdown", str(markdown), "--badge", str(badge), "--min-tests", "1"]) == 0
    assert "## By suite" in markdown.read_text(encoding="utf-8")
    assert json.loads(badge.read_text(encoding="utf-8"))["label"] == "wpt subtests"


def test_several_reports_total_together(tmp_path):
    """A sharded run is several reports; the page is one."""
    first, second = tmp_path / "one.json", tmp_path / "two.json"
    first.write_text(json.dumps(_report(_result("/dom/a.any.html", subtests=[("x", "PASS")]))), encoding="utf-8")
    second.write_text(json.dumps(_report(_result("/url/b.any.html", subtests=[("y", "FAIL")]))), encoding="utf-8")

    board = sb.read_reports([str(first), str(second)])

    assert board.total.test_count == 2
    assert board.total.subtests.of("PASS") == 1
    assert board.total.subtests.of("FAIL") == 1


def test_a_run_where_every_document_errored_is_refused(tmp_path):
    """A browser that died has a result per test and no subtest anywhere; that is not a score."""
    path = tmp_path / "wptreport.json"
    path.write_text(
        json.dumps(_report(*[_result(f"/dom/{i}.any.html", status="ERROR") for i in range(50)])),
        encoding="utf-8",
    )

    assert sb.main([str(path), "--markdown", str(tmp_path / "out.md"), "--min-tests", "10"]) == 1
    assert not (tmp_path / "out.md").exists()


@pytest.mark.parametrize(
    ("passed", "failed", "colour"),
    [(95, 5, "brightgreen"), (80, 20, "green"), (60, 40, "yellow"), (10, 90, "orange")],
)
def test_the_badge_colour_follows_the_rate(passed, failed, colour):
    subtests = [(f"p{i}", "PASS") for i in range(passed)] + [(f"f{i}", "FAIL") for i in range(failed)]
    board = _scoreboard(_result("/dom/a.any.html", subtests=subtests))

    assert json.loads(sb.render_badge(board))["color"] == colour
