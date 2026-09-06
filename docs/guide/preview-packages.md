# Using Jint 5 Preview Packages

Jint 5 is under active development. Its packages are published to the Jint
preview feed so applications can test new APIs before the stable release.

## Add the preview feed

Register the feed once:

```bash
dotnet nuget add source \
  https://f.feedz.io/sebastienros/jint/nuget/index.json \
  --name Jint-preview
```

Then install the package you need with prerelease versions enabled:

```bash
dotnet add package Jint --prerelease
dotnet add package Jint.Browser --prerelease
```

For the command-line browser:

```bash
dotnet tool install --global Jint.Browser.Tool --prerelease
```

Replace the package name with any package listed in the
[package matrix](../reference/package-matrix.md). Preview builds can introduce
breaking changes, so pin the selected version in applications and automation
that require repeatable restores.

Browse all available builds on the
[Jint Feedz repository](https://feedz.io/org/sebastienros/repository/jint/packages).

## Move from Jint 4

Applications upgrading from the stable `4.x` line should also review
[Migrating to Jint 5](./migrating-to-v5.md).
