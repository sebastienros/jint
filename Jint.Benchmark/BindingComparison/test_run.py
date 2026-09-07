"""Failures must never turn an empty or contaminated comparison into a successful report."""
import copy
import csv
from pathlib import Path
import tempfile
import unittest
import run


def report():
    return dict(arm="fixture", runtime="runtime", os="os", architecture="arch", workloadSha256="hash",
                assemblies=[dict(name=name, sha256="hash") for name in ("AngleSharp", "BenchmarkDotNet")],
                results=[dict(workload=workload, instance=instance, lane="cold" if call == 0 else "warm", checksum=value)
                         for workload, value in run.EXPECTED.items() for instance in range(2) for call in range(3)])


class ComparisonTests(unittest.TestCase):
    def test_matching_reports(self):
        run.compare(report(), report())

    def test_rejects_missing_wrong_or_different_work(self):
        for mutation in (lambda r: r["results"].pop(),
                         lambda r: r["results"][0].update(checksum=0),
                         lambda r: r.update(workloadSha256="different"),
                         lambda r: r["assemblies"][0].update(sha256="different")):
            candidate = copy.deepcopy(report())
            mutation(candidate)
            with self.assertRaises(ValueError):
                run.compare(report(), candidate)

    def test_csv_requires_every_successful_row_once(self):
        rows = [dict(Method=method, Workload=workload, Mean="1.5 μs")
                for method in ("Cold", "Warm") for workload in run.EXPECTED]
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "report.csv"
            for mode in ("valid", "empty", "missing", "duplicate", "failed"):
                data = copy.deepcopy(rows)
                if mode == "empty": data.clear()
                if mode == "missing": data.pop()
                if mode == "duplicate": data.append(data[0])
                if mode == "failed": data[0]["Mean"] = "NA"
                with path.open("w", newline="") as stream:
                    writer = csv.DictWriter(stream, fieldnames=("Method", "Workload", "Mean"))
                    writer.writeheader()
                    writer.writerows(data)
                if mode == "valid": run.validate_csv(path)
                else:
                    with self.assertRaises(ValueError): run.validate_csv(path)


if __name__ == "__main__":
    unittest.main()
