# Jint.HtmlParser packed consumer

This unsigned application consumes the newly packed `Jint.HtmlParser` NuGet package. It has no project
reference, friend access, signing key, or dependency on AngleSharp. The parser-only mode has no engine
dependency. Its own build configuration stops repository-wide MSBuild inheritance. Package source mapping restricts
`Jint` and `Jint.*` to the local feed and allows third-party dependencies from nuget.org,
so no Jint package can resolve from nuget.org.
The local-feed paths use forward slashes so NuGet retains the source's mapping identity on Unix as
well as Windows; a restored cache must not hide a broken fresh-package resolution.

The explicit `5.0.0-consumer-smoke` prerelease below is preserved by the parser version policy.
Stable `Version` or `PackageVersion` inputs instead acquire `experimental-<BuildNumber>` (default `0`),
even for a Jint GA tag. Use the resulting version in consumer references; do not assume the parser
package version equals the stable Jint/Browser version. This probe supplies acceptance evidence but
cannot by itself close ACC-06 or freeze all public APIs.

From the repository root on macOS or Linux, run:

```sh
rm -rf artifacts/html-parser-package-consumer
dotnet pack Jint.HtmlParser/Jint.HtmlParser.csproj -c Release \
  -o artifacts/html-parser-package-consumer/feed \
  -p:PackageVersion=5.0.0-consumer-smoke \
  -p:RestoreSources=https://api.nuget.org/v3/index.json
dotnet restore tools/html-parser-package-consumer/HtmlParserPackageConsumer.csproj \
  --configfile tools/html-parser-package-consumer/nuget.config
dotnet run --project tools/html-parser-package-consumer/HtmlParserPackageConsumer.csproj -c Release -f net8.0
dotnet run --project tools/html-parser-package-consumer/HtmlParserPackageConsumer.csproj -c Release -f net10.0
```

Each run prints `ALL PARSER PACKAGE PROBES PASSED` only after checking HTML, XML, strict SVG, fragment
ownership, mutation records/drains, whole-sheet CSS syntax, all owned XPath overloads and result kinds,
typed IDs and imports, namespace contexts and stale bindings, detached attribute axes, and all five
serialization routes including shadow acquisition/selection, templates, output limits and cancellation.
It also checks public DTD processing-instruction metadata, immutable snapshot/clone behavior and
original-input offsets without relocating instructions into DOM children. Live-traversal probes cover
node/attribute identity, character-data edits, live and static range endpoints and contents, adoption,
iterators and tree walkers. These checks replace the standalone D6r6 consumer and run in both the
shared packaged-consumer CI job and its parser-only Native AOT leg.
Clearing the disposable feed and package cache before packing prevents a prior package with the
same version from answering the test. Inspect the actual package dependency list and shipped assets:

```sh
dotnet list tools/html-parser-package-consumer/HtmlParserPackageConsumer.csproj package --include-transitive
unzip -p artifacts/html-parser-package-consumer/feed/Jint.HtmlParser.*.nupkg '*.nuspec'
unzip -l artifacts/html-parser-package-consumer/feed/Jint.HtmlParser.*.nupkg
```

The package must contain both `lib/net8.0` and `lib/net10.0` assets, declare `System.IO.Hashing`,
include `licenses/ValueStringBuilder.LICENSE.txt`, and have no `Jint` or `AngleSharp` dependency.
The application also checks that its own assembly is unsigned and that it loaded the
`Jint.HtmlParser` assembly. The package manifest and restored graph are the dependency checks, since
reflection-based assembly-reference enumeration is unsafe in a trimmed Native AOT consumer.

Where a Native AOT toolchain is installed, publish and run one target (replace the RID for the host):

```sh
dotnet publish tools/html-parser-package-consumer/HtmlParserPackageConsumer.csproj \
  -c Release -f net10.0 -r osx-arm64 -p:PublishAot=true \
  -p:RestoreConfigFile=tools/html-parser-package-consumer/nuget-aot.config
./tools/html-parser-package-consumer/bin/Release/net10.0/osx-arm64/publish/HtmlParserPackageConsumer
```

The AOT restore config also permits compiler and runtime packs from nuget.org. Its specific
Jint package mappings take precedence over the dependency wildcard, so the parser package keeps
its unique `5.0.0-consumer-smoke` version and can only restore from the local feed.

## Continuous integration

The shared `html-parser-package-consumer.yml` workflow runs from both PR and main builds. Each Linux
job starts with an empty feed and consumer package cache, then packs Jint, Jint.DevTools, Jint.HtmlParser
and Jint.Browser. Its two version inputs cover a prerelease and a GA Browser whose parser remains
experimental. `verify_packages.py` checks both shipped framework assets, the parser's package policy,
and Browser's exact dependency on the actual parser version before supplying consumer version properties.

The consumer builds and runs on net8.0 and net10.0 with `IncludeBrowser=true`. This adds a packaged
Browser smoke probe that creates a page, parses HTML, runs script and reads DOM text. For example,
after packing all four packages with `Version=5.0.0`, run:

```sh
python3 tools/html-parser-package-consumer/verify_packages.py feed \
  --version 5.0.0 --framework net8.0 --framework net10.0
dotnet build tools/html-parser-package-consumer/HtmlParserPackageConsumer.csproj -c Release \
  -p:IncludeBrowser=true -p:HtmlParserPackageVersion=5.0.0-experimental-0 -p:BrowserPackageVersion=5.0.0
python3 tools/html-parser-package-consumer/verify_packages.py restore \
  --version 5.0.0 --framework net8.0 --framework net10.0 --browser
```

Restore verification checks exact versions, framework assets, the source recorded by NuGet and the
SHA-512 of each restored Jint package against the fresh feed. CI then clears the consumer outputs and
cache again, publishes the parser-only probe as Linux net10.0 Native AOT, checks its restore provenance,
and runs the native binary. Trim/AOT analysis warnings fail this parser leg. Browser's general AOT
compatibility remains unclaimed; its integration probe runs under the regular .NET runtimes.
