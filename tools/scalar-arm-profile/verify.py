#!/usr/bin/env python3
"""Repeat the ordinary solution test command without profiling or altered test budgets."""

import argparse
import json
from pathlib import Path
import subprocess
import xml.etree.ElementTree as ET

from run import run, scalar_result


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--attempts", type=int, default=3)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    if args.attempts < 1:
        parser.error("--attempts must be positive")
    output = args.output.resolve()
    output.mkdir(parents=True, exist_ok=True)
    hook = Path("Jint.Tests.Browser/Directory.Build.targets")
    if hook.exists():
        raise RuntimeError(f"Refusing to overwrite {hook}")
    run(["git", "rev-parse", "HEAD"], output / "source-sha.txt")
    run(["dotnet", "--info"], output / "dotnet-info.txt")
    failed = False
    try:
        for attempt in range(1, args.attempts + 1):
            directory = output / f"attempt-{attempt:02d}"
            directory.mkdir()
            for framework in ("net8.0", "net10.0"):
                settings = ET.Element("RunSettings")
                config = ET.SubElement(settings, "RunConfiguration")
                ET.SubElement(config, "ResultsDirectory").text = str(directory)
                loggers = ET.SubElement(ET.SubElement(settings, "LoggerRunSettings"), "Loggers")
                logger = ET.SubElement(loggers, "Logger", friendlyName="trx", enabled="true")
                ET.SubElement(ET.SubElement(logger, "Configuration"), "LogFileName").text = f"browser-{framework}.trx"
                ET.ElementTree(settings).write(directory / f"{framework}.runsettings", encoding="unicode")
            project = ET.Element("Project")
            group = ET.SubElement(project, "PropertyGroup")
            ET.SubElement(group, "RunSettingsFilePath").text = str(directory / "$(TargetFramework).runsettings")
            ET.ElementTree(project).write(hook, encoding="unicode")
            print(f"Unprofiled full-suite attempt {attempt}/{args.attempts}", flush=True)
            try:
                code = run(["dotnet", "test", "--configuration", "Release", "--logger", "console;verbosity=quiet"],
                           directory / "test.log", timeout=1800)
            except subprocess.TimeoutExpired:
                code = 124
            results = {framework: scalar_result(directory / f"browser-{framework}.trx")
                       for framework in ("net8.0", "net10.0")}
            summary = {"suite_exit_code": code, "scalar": results}
            (directory / "verification.json").write_text(json.dumps(summary, indent=2))
            print(json.dumps(summary), flush=True)
            failed |= code != 0 or any(result["outcome"] != "Passed" for result in results.values())
            if code == 124:
                break
    finally:
        hook.unlink(missing_ok=True)
    return int(failed)


if __name__ == "__main__":
    raise SystemExit(main())
