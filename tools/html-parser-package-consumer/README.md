# Jint.HtmlParser packed consumer

This unsigned application consumes the newly packed `Jint.HtmlParser` NuGet package. It has no project
reference, friend access, signing key, or dependency on the Jint engine or AngleSharp. Its own build
configuration stops repository-wide MSBuild inheritance. The consumer's restore has only the local
feed as a package source, so it cannot resolve `Jint.HtmlParser` from nuget.org.

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

Each run prints `ALL PARSER PACKAGE PROBES PASSED` only after checking XML, strict SVG, fragment
ownership, mutation records and drains. Clearing the disposable feed and package cache before packing prevents a prior package with the
same version from answering the test. Inspect the actual package dependency list and shipped assets:

```sh
dotnet list tools/html-parser-package-consumer/HtmlParserPackageConsumer.csproj package --include-transitive
unzip -p artifacts/html-parser-package-consumer/feed/Jint.HtmlParser.*.nupkg '*.nuspec'
unzip -l artifacts/html-parser-package-consumer/feed/Jint.HtmlParser.*.nupkg
```

The package must contain both `lib/net8.0` and `lib/net10.0` assets and have no `Jint` or `AngleSharp`
dependency. The application also checks that its own assembly is unsigned and that it loaded the
`Jint.HtmlParser` assembly. The package manifest and restored graph are the dependency checks, since
reflection-based assembly-reference enumeration is unsafe in a trimmed Native AOT consumer.

Where a Native AOT toolchain is installed, publish and run one target (replace the RID for the host):

```sh
dotnet publish tools/html-parser-package-consumer/HtmlParserPackageConsumer.csproj \
  -c Release -f net10.0 -r osx-arm64 -p:PublishAot=true \
  -p:RestoreConfigFile=tools/html-parser-package-consumer/nuget-aot.config
./tools/html-parser-package-consumer/bin/Release/net10.0/osx-arm64/publish/HtmlParserPackageConsumer
```

The AOT restore config adds nuget.org only for compiler and runtime packs. The parser package keeps
its unique `5.0.0-consumer-smoke` version and is first restored from the local-only feed above.
