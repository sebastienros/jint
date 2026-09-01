# Vendored Chrome DevTools Protocol

`js_protocol.json` and `browser_protocol.json` are copied verbatim from the `json/` directory of
[ChromeDevTools/devtools-protocol](https://github.com/ChromeDevTools/devtools-protocol), at commit

    ea39a11d80de9a08ce2af03f52125ed2e462cf84

which is npm `devtools-protocol@0.0.1687809`, retrieved 2026-09-01. `pin.json` is that record in
machine-readable form and is what the generator stamps into the code it emits; this file and it must
say the same thing.

`devtools-protocol-LICENSE` is that commit's `LICENSE`. The protocol description is
Copyright The Chromium Authors and is redistributed here under the 3-Clause BSD License it carries.
Nothing in this directory is edited — a fetch at the pinned commit has to produce no diff, which is
why `.gitattributes` pins both files to LF.

## A bump is a code change

Re-pointing the pin is not a configuration edit that lands on its own. Upstream renames methods, moves
them between domains, changes a parameter from optional to required and retires deprecated commands, and
every one of those changes the generated dispatch surface. So a bump is:

1. Fetch the two files at the new commit, update `pin.json` and the commit above.
2. Regenerate (below) and **read the diff of `Jint.DevTools/Protocol/Generated/`**. That diff is the
   upstream change, stated in the vocabulary this repository compiles.
3. Fix whatever the diff broke, in the same pull request.

The same discipline `Jint.Tests.Test262/Test262Harness.settings.json`'s `SuiteGitSha` carries, and for
the same reason: a pin that moves without anybody reading what moved is an upstream normative change
landing unread.

## Regenerating

```bash
dotnet run --project tools/devtools-protocol/Jint.DevTools.ProtocolGenerator -c Release -- \
    --protocol tools/devtools-protocol \
    --manifest tools/devtools-protocol/manifest.json \
    --output Jint.DevTools/Protocol/Generated
```

The output is **checked in**, and `Jint.Tests.DevTools/Protocol/GeneratedProtocolIsCurrentTests.cs` runs
the same emitter in memory and compares it byte for byte with what is on disk. So an edit to
`manifest.json` without a regeneration fails the build, and a hand edit of a `.g.cs` file fails it too.

A Roslyn source generator was considered and rejected: the `System.Text.Json` context has to be generated
*over* the DTOs and generators do not chain, `Jint.SourceGenerators` is `netstandard2.0` without
`System.Text.Json`, and a protocol surface is exactly the kind of thing whose diff a reviewer wants to
read.

## The manifest

`manifest.json` is the boundary between what is *described* and what is *answered*.

- `generatedDomains` — the domains that get DTOs, a `<Domain>DomainBase` and a `<Domain>Events` factory
  class. Every command of a generated domain gets a virtual, and its default answers
  `-32601 'Domain.method' wasn't found`.
- `implementedMethods` — the commands Jint.DevTools answers. Each is checked to exist in the vendored
  protocol, and `ProtocolManifestTests` checks each is overridden on a registered domain and that
  nothing else is.
- `implementedEvents` — the events Jint.DevTools emits, checked the same way.
- `reportedDomains` — what `Schema.getDomains` answers. A domain may only appear here once it has an
  implemented command, so a client feature-detecting through `Schema.getDomains` is never told about a
  domain that answers nothing.

The generator fails rather than emitting broken code when an entry names something the vendored protocol
does not have, and when a `$ref` crosses into a domain that is not generated and does not resolve to a
primitive alias.
