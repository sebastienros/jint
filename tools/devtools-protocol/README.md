# Vendored Chrome DevTools Protocol

`js_protocol.json` and `browser_protocol.json` are copied verbatim from the `json/` directory of
[ChromeDevTools/devtools-protocol](https://github.com/ChromeDevTools/devtools-protocol), at commit

    ea39a11d80de9a08ce2af03f52125ed2e462cf84

which is npm `devtools-protocol@0.0.1687809`, retrieved 2026-09-01. `pin.json` is that record in
machine-readable form and is what the generator stamps into the code it emits; this file and it must
say the same thing.

`devtools-protocol-LICENSE` is that commit's `LICENSE`. The protocol description is
Copyright The Chromium Authors and is redistributed here under the 3-Clause BSD License it carries.
Neither vendored file is edited — a fetch at the pinned commit has to produce no diff, which is
why `.gitattributes` pins both to LF. The one file here that is *not* upstream's is `jint_protocol.json`,
below.

## The domain that is ours

`jint_protocol.json` sits beside the two vendored files, in the same format, and is **not** part of the
fetch: it is this repository's own, and a re-fetch at the pinned commit still produces no diff in the two
files above. It describes the `Jint` domain — `getMarkdown`, `getText`, `getAccessibilitySnapshot` — the way
Lightpanda describes its `LP` one, and the generator reads all three files alike, so a `Jint` command gets
data transfer objects, a dispatch base and a manifest entry exactly as a Chrome command does. The one place
they differ is the citation: a member of a domain Chrome does not have is cited against this file rather
than against Chrome's documentation, because `ProtocolCitationTests` resolves every `chromedevtools` URL
against the vendored JSON and a `Jint` anchor would name nothing there.

It declares the same protocol version as the vendored files, which the reader checks. Adding a domain to it
is the same three steps as any other: describe it here, add its commands to `manifest.json`, regenerate, and
override the virtuals.

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
  class. A generated command gets a virtual, and its default answers
  `-32601 'Domain.method' wasn't found`. An entry is **either a domain name or an object**:

  ```jsonc
  "Runtime",                                                  // the whole domain
  { "domain": "Audits", "commands": ["disable", "enable"] },   // these, and nothing else
  ```

  The object form generates the commands and events it names and nothing else — including nothing for a
  list it leaves out — and the types it gets are the closure of what those members reach, computed rather
  than listed. A command that is not generated has no virtual and is answered by the dispatch base's
  `default` case, which is the same `-32601` it answered before: the object form changes what is checked
  in, not what a client is told, and `ACommandAPartialDomainDoesNotGenerateIsStillMethodNotFound` is what
  says so. Which form to use is a judgement about the domain — one whose surface is being filled in stays
  whole, so the next command is an override and a manifest line; one that answers a handful of commands out
  of dozens, and whose remainder describes machinery this engine does not have, names them. Nine domains
  do today, and it took **30%** off the generated tree: `Audits` alone went from 143 KB to 4 KB.
- `implementedMethods` — the commands Jint.DevTools answers. Each is checked to exist in the vendored
  protocol, and `ProtocolManifestTests` checks each is overridden on a registered domain and that
  nothing else is.
- `implementedEvents` — the events Jint.DevTools emits, checked the same way.
- `reportedDomains` — what `Schema.getDomains` answers. A domain may only appear here once it has an
  implemented command, so a client feature-detecting through `Schema.getDomains` is never told about a
  domain that answers nothing.

The generator fails rather than emitting broken code when an entry names something the vendored protocol
does not have, when a `$ref` crosses into a domain that is not generated and does not resolve to a
primitive alias, and when `implementedMethods` names a command its own `generatedDomains` entry does not
generate — which would otherwise be a command the manifest and `Schema.getDomains` claim and nothing can
override.

## What a generated file says about itself

Every `.g.cs` carries three lines of provenance:

```
//     source:   tools/devtools-protocol/browser_protocol.json
//     protocol: version 1.3, ChromeDevTools/devtools-protocol@ea39a11… (devtools-protocol@0.0.1687809)
//     manifest: tools/devtools-protocol/manifest.json, Audits entries, sha256:5893b4009485
```

`source` is the description **that file** was read from, which is why it is per file: `Jint.g.cs` comes from
`jint_protocol.json`, ours, and used to cite the Chrome commit its neighbours come from — provenance naming
the wrong document, which is what a reader has instead of a build when they have only a diff in front of
them. A file generated from our own description names no Chrome commit at all, because a bump cannot move
it.

The `manifest` digest is over the *part* of the manifest that shaped the file — that domain's
`generatedDomains` entry, its implemented commands and its implemented events — rather than over the whole
file. A whole-file digest would rewrite all twenty-four headers whenever any domain gained a command, and
say nothing true about the other twenty-three. `ProtocolJsonContext.g.cs` and `ProtocolManifest.g.cs` are
generated from all of it and carry a digest of all of it. Line endings are normalised before hashing, so a
Windows checkout and a Linux one stamp the same digest.

## The map type

The protocol has no map keyword. What it writes instead is a **named type declared as `"type": "object"`
with no `properties`**, and `Network.Headers` — "request / response headers as keys / values of JSON
object" — is the one every generated domain uses. The emitter reads that shape as a map: no record is
emitted for the type, and every `$ref` to it resolves to
`global::System.Collections.Generic.Dictionary<string, string>`. An empty record was what came before, and
it meant a client's headers were not in the request object at all.

Two things about the rule are deliberate:

- **Only a *named* type is a map.** An **inline** `"type": "object"` member stays a `JsonElement`, because
  those carry values of mixed shapes rather than strings: `Debugger.paused`'s `data` holds a remote object,
  an execution context's `auxData` holds a boolean and two strings, and a dictionary of strings would drop
  the lot silently.
- **A repeated header is joined with a newline**, which is what Chrome sends, because the Fetch Standard
  keeps every value of a repeated header apart and the protocol's map cannot. That join is the domain's
  business rather than the generator's; see `Jint.Browser/DevTools/NetworkDomain.Events.cs`.

## The handshake recordings

`handshakes/` is what the described protocol looks like from the *client* side: every CDP method and event
Puppeteer, PuppeteerSharp, Playwright, Playwright for .NET and the Chrome DevTools frontend actually send
and receive while driving one Chrome build through one canonical scenario, one file per client plus
`matrix.md`. `manifest.json` decides what is answered; the matrix says what has to be. It is recorded, not
written, by [`tools/cdp-histogram`](../cdp-histogram/README.md).
