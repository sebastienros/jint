#!/usr/bin/env python3
"""Build a signed parser package, then run a real unsigned consumer on both TFMs."""
from pathlib import Path
import shutil
import subprocess
import tempfile

source = Path(__file__).resolve().parent
repo = source.parents[2]
work = Path(tempfile.mkdtemp(prefix="jint-live-traversal-consumer-"))
packages = work / "packages"
consumer = work / "consumer"
consumer.mkdir()
shutil.copyfile(source / "PackedConsumer.csproj.txt", consumer / "PackedConsumer.csproj")
shutil.copyfile(source / "Program.cs.txt", consumer / "Program.cs")
(consumer / "NuGet.Config").write_text('<configuration><packageSources><clear/><add key="probe" value="' + str(packages) + '"/><add key="nuget.org" value="https://api.nuget.org/v3/index.json"/></packageSources></configuration>')
subprocess.run(["dotnet", "pack", str(repo / "Jint.HtmlParser/Jint.HtmlParser.csproj"), "-c", "Release", "-p:PackageVersion=0.0.0-d6r6-probe", "-p:RestoreSources=https://api.nuget.org/v3/index.json", "-o", str(packages)], check=True, cwd=repo)
for framework in ("net8.0", "net10.0"):
    subprocess.run(["dotnet", "run", "--project", str(consumer / "PackedConsumer.csproj"), "-c", "Release", "-f", framework, "-p:RestorePackagesPath=" + str(work / "restored")], check=True, cwd=consumer)
print("Consumer project and signed package preserved at", work)
