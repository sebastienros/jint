"""Diagnostic-only full-suite repetitions for #3882; never relax or retry inside a test."""

import argparse
from datetime import datetime
import json
import os
from pathlib import Path
import signal
import shutil
import subprocess
import sys
import tempfile
import threading
import xml.etree.ElementTree as ET


def run(command, log, **kwargs):
    with log.open("w") as stream:
        return subprocess.run(command, stdout=stream, stderr=subprocess.STDOUT, **kwargs).returncode


def settings(directory, framework, port):
    root = ET.Element("RunSettings")
    config = ET.SubElement(root, "RunConfiguration")
    ET.SubElement(config, "ResultsDirectory").text = str(directory)
    env = ET.SubElement(config, "EnvironmentVariables")
    for key, value in {
        # The external writer finalizes the trace even when vstest terminates its testhost.
        # nosuspend also prevents child processes from waiting for another collector.
        "DOTNET_DiagnosticPorts": f"{port},connect,nosuspend",
        "JINT_SCALAR_PROFILE_DIRECTORY": str(directory),
        "JINT_SCALAR_PROFILE_FRAMEWORK": framework,
    }.items():
        ET.SubElement(env, key).text = value
    loggers = ET.SubElement(ET.SubElement(root, "LoggerRunSettings"), "Loggers")
    logger = ET.SubElement(loggers, "Logger", friendlyName="trx", enabled="true")
    ET.SubElement(ET.SubElement(logger, "Configuration"), "LogFileName").text = f"browser-{framework}.trx"
    ET.ElementTree(root).write(directory / f"{framework}.runsettings", encoding="unicode")


def scalar_result(path):
    if not path.exists():
        return {"outcome": "Missing"}
    root = ET.parse(path).getroot()
    for result in root.iter():
        if result.tag.endswith("}UnitTestResult") and result.get("testName") == "ScalarRendersTheCapturedOpenApiOperations":
            return {**result.attrib, "details": " ".join(result.itertext())}
    return {"outcome": "Missing"}


def validate_trace(trace, tool, reader, result):
    metadata = trace.with_suffix(".metadata.json")
    if run(["dotnet", str(reader), str(trace)], metadata) != 0:
        return False
    timing = json.loads(metadata.read_text())
    if timing["EventsLost"] != 0 or "startTime" not in result:
        return False
    if (datetime.fromisoformat(timing["StartUtc"]) > datetime.fromisoformat(result["startTime"])
            or datetime.fromisoformat(timing["EndUtc"]) < datetime.fromisoformat(result["endTime"])):
        return False
    log = trace.with_suffix(".conversion.log")
    if run([tool, "convert", str(trace), "--format", "Speedscope"], log) != 0:
        return False
    if "potentially broken trace" in log.read_text():
        return False
    converted = trace.with_suffix(".speedscope.json")
    if not converted.exists():
        return False
    profile = json.loads(converted.read_text())
    names = [frame["name"] for frame in profile.get("shared", {}).get("frames", [])]
    samples = sum(len(p.get("samples", p.get("events", []))) for p in profile.get("profiles", []))
    valid = samples > 0 and any("Jint.Browser" in name for name in names)
    trace.with_suffix(".validation.json").write_text(json.dumps({"samples_or_events": samples, "browser_frames": sum("Jint.Browser" in n for n in names), "valid": valid}, indent=2))
    report_code = run([tool, "report", str(trace), "topN", "-n", "50", "--inclusive"], trace.with_suffix(".top.txt"))
    return valid and report_code == 0


def finish_capture(collector, directory, framework, stop):
    """Finalize while the testhost is alive, then release its diagnostic teardown."""
    while not stop.wait(0.1):
        if (directory / f"{framework}.complete").exists():
            if collector.poll() is None:
                collector.send_signal(signal.SIGINT)
            try:
                code = collector.wait(timeout=45)
            except subprocess.TimeoutExpired:
                return
            if code == 0:
                (directory / f"{framework}.stopped").touch()
            return


TEARDOWN = """
[NUnit.Framework.SetUpFixture]
public sealed class ScalarTraceFinalization
{
    [NUnit.Framework.OneTimeTearDown]
    public static async System.Threading.Tasks.Task FinalizeTrace()
    {
        var directory = System.Environment.GetEnvironmentVariable("JINT_SCALAR_PROFILE_DIRECTORY")!;
        var framework = System.Environment.GetEnvironmentVariable("JINT_SCALAR_PROFILE_FRAMEWORK")!;
        System.IO.File.WriteAllText(System.IO.Path.Combine(directory, framework + ".complete"), "");
        var deadline = System.Diagnostics.Stopwatch.StartNew();
        while (!System.IO.File.Exists(System.IO.Path.Combine(directory, framework + ".stopped")))
        {
            if (deadline.Elapsed > System.TimeSpan.FromSeconds(60))
                throw new System.TimeoutException("Diagnostic trace finalization did not complete.");
            await System.Threading.Tasks.Task.Delay(100);
        }
    }
}
"""


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--attempts", type=int, default=6)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--smoke", action="store_true", help="Validate capture locally with one isolated net8 Scalar run.")
    parser.add_argument("--verify", action="store_true", help="Capture one full-suite run and require passing Scalar results and complete traces.")
    args = parser.parse_args()
    if args.verify:
        args.attempts = 1
    output = args.output.resolve()
    output.mkdir(parents=True, exist_ok=True)
    tool = os.environ.get("TRACE_TOOL", "dotnet-trace")
    # Build outside the repository so the reader has independent package settings.
    reader_dir = Path(tempfile.mkdtemp(prefix="scalar-trace-reader-"))
    shutil.copyfile(Path(__file__).with_name("TraceMetadata.cs"), reader_dir / "Program.cs")
    (reader_dir / "reader.csproj").write_text('''<Project Sdk="Microsoft.NET.Sdk">
      <PropertyGroup><TargetFramework>net8.0</TargetFramework><OutputType>Exe</OutputType>
      <RestoreSources>https://api.nuget.org/v3/index.json</RestoreSources></PropertyGroup>
      <ItemGroup><PackageReference Include="Microsoft.Diagnostics.Tracing.TraceEvent" Version="3.1.23" /></ItemGroup>
    </Project>''')
    if run(["dotnet", "build", str(reader_dir / "reader.csproj"), "-c", "Release"], output / "reader-build.log") != 0:
        raise RuntimeError("Unable to build the trace integrity reader.")
    reader = reader_dir / "bin/Release/net8.0/reader.dll"
    run(["dotnet", "--info"], output / "dotnet-info.txt")
    run(["git", "rev-parse", "HEAD"], output / "commit.txt")
    (output / "runner.json").write_text(json.dumps({key: os.environ.get(key) for key in
        ("RUNNER_NAME", "RUNNER_ARCH", "RUNNER_OS", "ImageOS", "ImageVersion", "GITHUB_RUN_ID")}, indent=2))
    if sys.platform == "linux":
        shutil.copyfile("/proc/cpuinfo", output / "cpuinfo.txt")
        shutil.copyfile("/proc/meminfo", output / "meminfo.txt")
    # Scope profiling to browser testhosts while leaving the full solution's normal scheduling intact.
    # This hook is removed afterward; existing engine/test sources are unchanged.
    hook = Path("Jint.Tests.Browser/Directory.Build.targets")
    if hook.exists():
        raise RuntimeError(f"Refusing to overwrite {hook}")
    history = []
    try:
        for attempt in range(1, args.attempts + 1):
            directory = output / f"attempt-{attempt:02d}"
            directory.mkdir()
            frameworks = ["net8.0"] if args.smoke else ["net8.0", "net10.0"]
            collectors = []
            stop = threading.Event()
            watchers = []
            for framework in frameworks:
                port = f"/tmp/scalar-{os.getpid()}-{attempt}-{framework}.sock"
                settings(directory, framework, port)
                stream = (directory / f"{framework}.collector.log").open("w")
                collector = subprocess.Popen([tool, "collect", "--diagnostic-port", port,
                    # GC + loader + JIT + IL/native maps. Avoid high-volume exception/type events.
                    "--profile", "cpu-sampling", "--providers", "Microsoft-Windows-DotNETRuntime:0x20019:4",
                    "--buffersize", "256", "--output", str(directory / f"{framework}-browser.nettrace")],
                    stdout=stream, stderr=subprocess.STDOUT)
                collectors.append((collector, stream))
                watcher = threading.Thread(target=finish_capture, args=(collector, directory, framework, stop))
                watcher.start()
                watchers.append(watcher)
            project = ET.Element("Project")
            group = ET.SubElement(project, "PropertyGroup")
            ET.SubElement(group, "RunSettingsFilePath").text = str(directory / "$(TargetFramework).runsettings")
            source = directory / "ScalarTraceFinalization.cs"
            source.write_text(TEARDOWN)
            ET.SubElement(ET.SubElement(project, "ItemGroup"), "Compile", Include=str(source))
            ET.ElementTree(project).write(hook, encoding="unicode")
            command = ["dotnet", "test", "--configuration", "Release", "--logger", "console;verbosity=quiet"]
            if args.smoke:
                command += ["Jint.Tests.Browser/Jint.Tests.Browser.csproj", "--framework", "net8.0", "--filter", "FullyQualifiedName~ScalarFixtureTests"]
            print(f"Attempt {attempt}: {' '.join(command)}", flush=True)
            monitor = None
            with (directory / "vmstat.txt").open("w") as stream:
                try:
                    if sys.platform == "linux":
                        monitor = subprocess.Popen(["vmstat", "-t", "5"], stdout=stream)
                    try:
                        code = run(command, directory / "test.log", timeout=1800)
                    except subprocess.TimeoutExpired:
                        code = 124
                finally:
                    stop.set()
                    for watcher in watchers:
                        watcher.join()
                    if monitor:
                        monitor.terminate()
                        monitor.wait()
                    for collector, collector_stream in collectors:
                        try:
                            collector.wait(timeout=15)
                        except subprocess.TimeoutExpired:
                            collector.send_signal(signal.SIGINT)
                            try:
                                collector.wait(timeout=15)
                            except subprocess.TimeoutExpired:
                                collector.kill()
                                collector.wait()
                        collector_stream.close()
            record = {"attempt": attempt, "exit_code": code, "frameworks": {}}
            captured = False
            for framework in frameworks:
                result = scalar_result(directory / f"browser-{framework}.trx")
                target_failure = result["outcome"] == "Failed" and "Page.Errors" in result["details"] and "The operation's time budget elapsed" in result["details"]
                traces = list(directory.glob(f"{framework}-*.nettrace"))
                # Read every trace in the first pass to prove capture works, then on failures/last pass.
                readable = []
                if attempt == 1 or target_failure or attempt == args.attempts:
                    readable = [str(trace.name) for trace in traces if validate_trace(trace, tool, reader, result)]
                result.update(target_failure=target_failure, traces=[t.name for t in traces], readable=readable)
                record["frameworks"][framework] = result
                captured |= target_failure and bool(readable)
            history.append(record)
            (output / "summary.json").write_text(json.dumps(history, indent=2))
            print(json.dumps(record), flush=True)
            if args.verify:
                if not all(r["readable"] for r in record["frameworks"].values()):
                    raise RuntimeError("Post-fix trace is incomplete.")
                return int(code != 0 or any(r["outcome"] != "Passed" for r in record["frameworks"].values()))
            if captured:
                print("Captured the Scalar budget failure with readable browser samples.", flush=True)
                return 0
            if attempt == 1 and not all(r["readable"] for r in record["frameworks"].values()):
                raise RuntimeError("Profiling validation failed; refusing to repeat without valid capture.")
            if args.smoke:
                return code
            if code != 0 and any(r["outcome"] == "Missing" for r in record["frameworks"].values()):
                raise RuntimeError("Scalar did not run; inspect test.log before repeating.")
            # Retain the first successful baseline and all failures; discard only subsequent passing traces.
            if attempt > 1 and code == 0 and attempt < args.attempts:
                for trace in directory.glob("*.nettrace"):
                    trace.unlink()
        print("Batch exhausted without a valid trace of the target failure; this is NOT evidence of a fix.", flush=True)
        return 2
    finally:
        hook.unlink(missing_ok=True)
        shutil.rmtree(reader_dir)


if __name__ == "__main__":
    sys.exit(main())
