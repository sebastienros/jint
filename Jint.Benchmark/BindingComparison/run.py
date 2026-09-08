#!/usr/bin/env python3
"""Verify both dependency graphs, optionally collect alternating BDN rounds (no ratio claims)."""
import argparse
import csv
import json
import os
from pathlib import Path
import subprocess
import sys

ROOT = Path(__file__).resolve().parent
ARMS = ("Baseline", "Candidate")
EXPECTED = {"NodeRead": 13000, "ElementReadWrite": 8500, "DocumentLookup": 5000, "InterpreterControl": 3500}


def compare(baseline, candidate):
    for key in ("runtime", "os", "architecture", "workloadSha256"):
        if baseline[key] != candidate[key]:
            raise ValueError(f"Different {key}: comparison is invalid")
    for report in (baseline, candidate):
        expected = [dict(workload=workload, instance=instance,
                         lane="cold" if call == 0 else "warm", checksum=checksum)
                    for workload, checksum in EXPECTED.items()
                    for instance in range(2) for call in range(3)]
        if report["results"] != expected:
            raise ValueError(f"Incomplete or incorrect checksums: {report['arm']}")
    for name in ("AngleSharp", "BenchmarkDotNet"):
        def assembly(report):
            return next(a for a in report["assemblies"] if a["name"] == name)
        if assembly(baseline) != assembly(candidate):
            raise ValueError(f"Different {name} assemblies: comparison is invalid")


def validate_csv(path):
    with path.open(encoding="utf-8-sig", newline="") as stream:
        sample = stream.read(4096)
        stream.seek(0)
        dialect = csv.Sniffer().sniff(sample, delimiters=",;") if sample else csv.excel
        rows = list(csv.DictReader(stream, dialect=dialect))
    expected = {(method, workload) for method in ("Cold", "Warm") for workload in EXPECTED}
    actual = {(row.get("Method"), row.get("Workload")) for row in rows}
    if actual != expected or len(rows) != len(expected):
        raise ValueError(f"Missing, duplicate or unexpected benchmark rows: {path}")
    if any(row.get("Mean", "").strip() in ("", "NA", "N/A", "?") for row in rows):
        raise ValueError(f"Failed benchmark measurement: {path}")


def run(dotnet, arm, args, log, environment):
    command = [dotnet, "run", "-c", "Release", "--project", str(ROOT / arm), "--", *args]
    with log.open("w", encoding="utf-8") as stream:
        subprocess.run(command, cwd=ROOT / arm, stdout=stream, stderr=subprocess.STDOUT,
                       env=environment, check=True)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--dotnet", default="dotnet", help="SDK executable (also used by BDN child builds)")
    parser.add_argument("--output", type=Path, required=True, help="New directory for logs and provenance")
    parser.add_argument("--measure", action="store_true", help="Collect gate-mode measurements on an idle machine")
    parser.add_argument("--rounds", type=int, default=6, help="Alternating paired rounds (minimum 6)")
    args = parser.parse_args()
    if args.measure and args.rounds < 6:
        parser.error("measurements require at least six paired rounds")
    environment = os.environ.copy()
    if args.measure:
        if environment.get("JINT_BENCH_SKIP_IDLE_CHECK") in ("1", "true"):
            parser.error("remove JINT_BENCH_SKIP_IDLE_CHECK for measurements")
        environment["JINT_BENCH_MODE"] = "gate"
    if Path(args.dotnet).is_absolute():
        environment["PATH"] = str(Path(args.dotnet).parent) + os.pathsep + environment.get("PATH", "")
        environment["DOTNET_ROOT"] = str(Path(args.dotnet).parent)
    output = args.output.resolve()
    output.mkdir(parents=True, exist_ok=False)
    provenance = {}
    for arm in ARMS:
        log = output / f"{arm}.verify.log"
        run(args.dotnet, arm, ["--verify"], log, environment)
        reports = [json.loads(line) for line in log.read_text().splitlines() if line.startswith('{"arm":')]
        if len(reports) != 1:
            raise ValueError(f"Expected exactly one verification report: {log}")
        provenance[arm] = reports[0]
    compare(provenance["Baseline"], provenance["Candidate"])
    provenance["gitHead"] = subprocess.check_output(["git", "rev-parse", "HEAD"], cwd=ROOT, text=True).strip()
    provenance["gitStatus"] = subprocess.check_output(["git", "status", "--porcelain"], cwd=ROOT, text=True)
    provenance["sdk"] = subprocess.check_output([args.dotnet, "--version"], env=environment, text=True).strip()
    (output / "verification.json").write_text(json.dumps(provenance, indent=2) + "\n")
    print(f"Verified both arms: 24 checksums each; provenance in {output}", flush=True)
    if args.measure:
        for round_index in range(args.rounds):
            order = ARMS if round_index % 2 == 0 else tuple(reversed(ARMS))
            for arm in order:
                directory = output / f"round-{round_index + 1}" / arm
                directory.mkdir(parents=True)
                print(f"Collecting round {round_index + 1}: {arm}", flush=True)
                run(args.dotnet, arm, ["--filter", "*BindingBenchmark*", "--artifacts", str(directory)],
                    directory / "run.log", environment)
                csv_files = list((directory / "results").glob("*-report.csv"))
                if len(csv_files) != 1:
                    raise ValueError(f"Expected one BDN report: {directory}")
                validate_csv(csv_files[0])
        print("Complete paired raw artifacts saved. Analyze paired differences and confidence intervals before making claims.")


if __name__ == "__main__":
    try:
        main()
    except (ValueError, OSError, csv.Error, subprocess.CalledProcessError) as error:
        print(f"Comparison failed: {error}", file=sys.stderr)
        sys.exit(1)
