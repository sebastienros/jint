# HTML template completion and public facade checkpoint

Root Astra approved this finite sequence on 2026-09-25: native PI attributes and template patch
identity slots, bounded H6f template algorithms, then H7c public HTML entry points with packed
external-consumer proof. This checkpoint amends the original options proposal; it does not claim
that the algorithms below have already landed.

`MarkupParser.ParseHtml(string, HtmlParseOptions?, CancellationToken)` returns a native document.
`MarkupParser.ParseHtmlFragment(string, Element, HtmlParseOptions?, CancellationToken)` returns a
detached fragment belonging to the supplied context's owner document. It never replaces that
context's children. Browser's internal fragment factory supplies a different actual target for
template contents or shadow roots when its calling algorithm requires one.

Promoted `HtmlParseOptions` contains `ScriptingEnabled` (false), shared `Limits` (Unbounded), optional
`Diagnostics`, and HTML-specific `MaxCreatedNodes` (nonnegative, zero unbounded). The creation cap
does not belong to generic `ParseLimits`, since other public parsers must not silently ignore it.
The shared exception taxonomy may include `ParseLimitKind.CreatedNodes`.

The cap counts actual parser node allocations before creation, including documents, result and
template fragments, synthetic/implied/explicit/reconstructed elements, text, comments, PIs,
doctypes, attributes and declarative shadow roots. Existing host/context nodes are excluded;
movement/removal does not refund the count. This is separate from Browser's finished-document
`MaxDomNodes` and wrapper bounds. Limit/cancellation failure terminates the parser; it does not
promise rollback of already committed native mutations.

BaseUrl is deliberately omitted from this first promotion. Real document URL metadata and WHATWG
resolution remain owned by the Browser integration; no dormant string option or System.Uri
substitute is introduced. The scripting flag controls grammar and inert metadata only: public
parsers choose Disabled or Inert explicitly, never Normal. Declarative-shadow permission remains
an internal calling-algorithm fact, without a public feature toggle or host callback.

PI attributes use the [DOM algorithm](https://dom.spec.whatwg.org/#interface-processinginstruction)
and [XML pseudo-attribute grammar](https://www.w3.org/TR/xml-stylesheet/#NT-PseudoAtts). Native
identity owns the ordered map; equal-value data replacement invalidates it, while attribute-driven
serialization preserves it. HTML inspection advances the same native parser in charged slices.
Invalid syntax publishes an empty map, including invalid references or duplicate names.

PI clone/import copies target and data only under the current DOM clone algorithm; its map begins
empty, rather than being reconstructed from the copied data on first access. A small known-empty
flag preserves lazy allocation. Subsequent data replacement, including an equal write, invokes the
normal update-from-data semantics. This intentionally corrects the former Browser helper, which
reparsed a clone on first attribute access; no WPT pass is claimed for that correction.

PI name hashing is incremental during the native scan. Dictionary keys store that hash; collision
and duplicate equality poll cancellation and charge every compared character. The dictionary
operation is an atomic native batch and may exceed a requested quantum. Final string copies use
before/after cancellation boundaries; they are not presented as preemptible CLR operations.

Template identity owns insertion target/start/end marker slots. Fresh construction and cloning
leave them null; adoption preserves their references. H6f follows current
[template parsing](https://html.spec.whatwg.org/multipage/parsing.html#parsing-main-inhead),
[content patching](https://html.spec.whatwg.org/multipage/scripting.html#prepare-content-patching)
and [placement](https://html.spec.whatwg.org/multipage/parsing.html#appropriate-place-for-inserting-a-node).
Explicit template closure removes markers and clears slots. EOF recovery does not invent that
closure cleanup. No public facade ships until applicable patch/shadow paths work and the HTML
success census has no MissingFeature results.

Acceptance includes fresh Release tests on both shipped frameworks, reviewed public API snapshots,
and clean external PackageReference consumers built from the actual nupkg. Consumers prove
defaults/recovery/context ownership/foreign fragments/inertness, bounded diagnostics, exact and
over limits, and cancellation without internal access or a project reference.

## Atomic native mutation boundary amendment

Astra explicitly approved this amendment on 2026-09-25 after auditing existing native removal
hooks. Parser-owned searches, snapshots, input scans and stack walks remain charged, resumable
and cancellable. An existing coherent native DOM mutation is an atomic commit boundary: it can
exceed the cooperative Drive quantum, with cancellation observed before and after the commit.
The parser does not poll or yield halfway through range/iterator/slot/form/observer repair, promise
a latency ceiling, or invent a per-descendant cost for opaque native work. Parser WorkCount
counts the commit boundary only; it excludes that opaque internal mutation cost. Input and
creation limits do not make atomic native mutations preemptible.

Any exception escaping that boundary terminates the session and preserves the original exception
identity. A post-commit cancellation or throwing native notification leaves a coherent DOM and
a faulted parser that cannot resume or replay the committed operation. Cancellation before the
commit changes nothing for that operation; previously committed operations are not rolled back.
Browser must check its engine constraints before and after Drive. No general parser transaction
framework or rollback layer is introduced.
