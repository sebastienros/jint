# Browser native DOM cutover checkpoint

## Status

This is an unfinished, unmerged checkpoint, not a working Browser cutover. Work
stopped at the user's request to finalize the remaining chats. Do not integrate
the production migration until its remaining callers and native dependencies
are completed and verified. No compatibility fallback or stub was added to make
the build pass.

- Branch: `codex/browser-native-dom-cutover`.
- Starting source: `3c0f0ee7541cd977b458a3d46e5f47b56006ebfd`.
- Independently coherent generator commit: `632db0093` (Make DOM emitter type
  selection explicit in binding contract).
- The following checkpoint commit preserves the production edits, generated
  output, contract changes, and this note together.
- No PR was created. No active build or generator process needs this checkout.

## Verified separable change

The generator commit adds explicit type-map candidacy and configurable
collection type names, with defaults preserving the existing contract.
Its Release build succeeded. Generating the original contract produced all
12 files byte-for-byte identical to the original generated files.
That commit can be considered independently of the unfinished production work.

## Preserved production work

The existing `DomNodeObject`, `DomRealm`, wrapper cache, and creation-realm
association were moved toward native `Jint.HtmlParser` nodes and attributes.
There is no parallel native wrapper identity. Attribute wrappers retain the
JavaScript Node contract even though native Attr is not a Node. Normal wrapping
does not scan a subtree; association and adoption boundaries still do.

The checkpoint also contains a native namespace/local-name classifier,
Browser-owned browsing-context and document-state associations, native Node
helpers and equality, live child-node-list views, native XML parsing and
serialization, and an inert HTML parser-session driver. Event reconciliation
and activation overrides remain present, but their old dependencies have not
all been ported.

The contract was regenerated through the generator, preserving 163 interfaces
and producing 12 files with zero generator diagnostics. Broad receiver/type
replacement is an intermediate migration step: many member helpers and APIs
still need deliberate native implementations. Generated files were not edited
by hand. AngleSharp package references remain because production consumers
still depend on them.

Native parser runtime source was not changed here. The only native-project
edit is the signed Browser `InternalsVisibleTo` grant in
`Jint.HtmlParser/Parsing/AssemblyInfo.cs`.

## Build evidence and gaps

The last fresh build was:

```sh
dotnet build -c Release -f net8.0 Jint.Browser/Jint.Browser.csproj -p:RestoreSources=https://api.nuget.org/v3/index.json
```

It failed with zero warnings and four declaration errors:

- `DomProcessingInstructionAttributes.cs`: missing native `DomRange`.
- `DomRealm.cs`: unresolved `IHtmlCollection<>` and `IStringMap`.
- `DomRealm.cs`: sealed native Element cannot be a generic constraint.

These errors are the first declaration gate and mask further errors. They are
not the complete remaining work list. Native HtmlParser, engine, and DevTools
dependencies compiled in this run. Browser tests were not run, and the Browser
net10 target was not built. There is no successful Browser compilation or
behavioral verification for this checkpoint.

Known remaining work includes:

- Port Runtime, ParserDriver, FrameWindows, constructors, activation helpers,
  reflected/token/string-map helpers, and all other AngleSharp consumers.
- Connect Runtime URL writers to the new document-state association.
- Complete and audit native HTML/SVG interface classification and immutable
  WebIDL brand checks, including TryBind and argument error behavior.
- Correct iterator references left by broad contract replacement; the pending
  native class is `DomNodeIterator`, not `Jint.HtmlParser.NodeIterator`.
- Integrate native Range, NodeIterator, TreeWalker, and CharacterData operations
  from their owning work. The checkpoint references pending
  `NativeCharacterData.Normalize(Node)` and the proposed DomRange boundary API.
- Integrate HTML foreign-content/session and script insertion-marker work from
  their owners. A parser MissingFeature remains an integration failure.
- Integrate the native CSS executor and the Browser per-query live-state seam
  for selectors, including selectors nested in is/not/has.
- Implement native XPath navigation with Browser namespace adaptation.
- Audit mutation bookkeeping, budgets, document-position ordering, live views,
  Attr semantics, events, and all 163 interface bindings against the baseline.
- Remove stale AngleSharp documentation and package references only after all
  consumers have been migrated and the required test/build legs pass.

The native traversal/CharacterData, HTML parser-session, insertion-marker, and
CSS executor work belonged to separate active chats at the time of checkpoint.
Their completion and exact integration commits must be established before
resuming; this note does not assert those dependencies are ready.
