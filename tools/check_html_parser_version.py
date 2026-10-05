#!/usr/bin/env python3
"""Verify the provisional parser package policy without compiling other target frameworks."""

import json
from pathlib import Path
import subprocess


ROOT = Path(__file__).resolve().parents[1]
CASES = (
    (("VersionSuffix=preview-123",), "5.0.0-preview-123"),
    (("VersionSuffix=",), "5.0.0-experimental-0"),
    (("Version=5.0.0",), "5.0.0-experimental-0"),
    (("Version=5.1.0", "BuildNumber=42"), "5.1.0-experimental-42"),
    (("Version=5.0.0-rc.1",), "5.0.0-rc.1"),
    (("PackageVersion=5.0.0",), "5.0.0-experimental-0"),
    (("PackageVersion=5.0.0-consumer-smoke",), "5.0.0-consumer-smoke"),
    (("PackageVersion=5.0.0+metadata",), "5.0.0-experimental-0+metadata"),
)


def main():
    for properties, expected in CASES:
        result = subprocess.run(
            [
                "dotnet", "msbuild", "Jint.HtmlParser/Jint.HtmlParser.csproj",
                "-p:Configuration=Release", "-p:TargetFrameworks=net10.0",
                "-p:TargetFramework=net10.0", "-t:_GetProjectVersion",
                "-getProperty:PackageVersion", "-getTargetResult:_GetProjectVersion",
                *[f"-p:{prop}" for prop in properties],
            ],
            cwd=ROOT, text=True, capture_output=True, check=True,
        )
        data = json.loads(result.stdout)
        actual = data["Properties"]["PackageVersion"]
        referenced = data["TargetResults"]["_GetProjectVersion"]["Items"][0]["ProjectVersion"]
        if actual != expected or referenced != expected:
            raise AssertionError(f"{properties}: package={actual}, reference={referenced}, expected={expected}")
        browser = subprocess.run(
            [
                "dotnet", "msbuild", "Jint.Browser/Jint.Browser.csproj",
                "-p:Configuration=Release", "-p:TargetFrameworks=net10.0",
                "-p:TargetFramework=net10.0", "-t:_GetProjectReferenceVersions",
                "-getItem:_ProjectReferencesWithVersions",
                *[f"-p:{prop}" for prop in properties],
            ],
            cwd=ROOT, text=True, capture_output=True, check=True,
        )
        references = json.loads(browser.stdout)["Items"]["_ProjectReferencesWithVersions"]
        parser = next(item for item in references if item["Filename"] == "Jint.HtmlParser")
        if parser["ProjectVersion"] != f"[{expected}]":
            raise AssertionError(f"{properties}: Browser parser dependency={parser['ProjectVersion']}")
        print(f"{properties}: {actual}, Browser dependency [{expected}]")
    print("PARSER PRERELEASE VERSION CHECKS PASSED")


if __name__ == "__main__":
    main()
