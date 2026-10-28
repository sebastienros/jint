# Native styling boundary

Browser follows the [renderless CSS boundary](../../Jint.HtmlParser/README.md#renderless-css-boundary).
`NativeCssStyleSheets.Install` stores owner identity, source/base URLs, source text and resource
revision. It does not parse inline attributes, run the cascade or perform layout. `NativeCssParsing`
owns demand-driven rule/media caches; inactive bodies stay raw until CSSOM inspection or matching
requires them. Imports load without parsing unrelated rule bodies.

`NativeCssQuery` memoizes matching sources and requested text values for one synchronous query.
It checks document, resource, imported-sheet graph, inline-block and Browser read witnesses.
Matching occurs once per element/query, including native shadow scope and slot/host inheritance.
No state is attached to native nodes. Origin, importance, encapsulation, inline style, layer rank,
specificity and source order select declarations; inherited properties and catalog defaults fill gaps.
Explicit revert/revert-rule/revert-layer values retain cascade rollback.

Successive queries for one document reuse an applicable-rule plan and immutable selector-subject
buckets while the ordered sheets, origins, namespace filters, imported-sheet revisions, media and
owner shadow scopes agree. DOM/text/attribute and focus/control changes still retire element matches,
inline inputs and computed values; they never reuse a previous element's cascade. Candidate buffers
and de-duplication state are query-local. The weak document key retains only the latest plan, with no
engine, callback or network state in that plan. No attached sheet/model or plan is shared across
navigation documents or browser contexts.

Revision verification keeps a saturating process-wide CSS mutation epoch as a negative witness.
An unchanged epoch avoids rescanning sheets and demanded inline declaration blocks, but never skips
the charged host checkpoints, cancellation, DOM/resource/control revisions or Browser read witness.
Unrelated documents can only force another scan; saturation disables the shortcut.

The raw stylesheet boundary scanner has a bounded exact-source FIFO cache (16 sources, at most
2,097,152 retention units: UTF-16 source length plus 64 per raw rule). Oversized sources are not
retained. Only immutable source slices/rule boundaries are reused here, after the normal resource policy
has obtained the current text.
Cache hits still charge work and check cancellation; no host callback runs under the cache lock.

Ordinary style rules also have a bounded exact-text-and-source-offset preparation cache (8,192
entries, 8,388,608 retention units). Units account for source/program size and expanded declarations;
rules longer than 65,536 UTF-16 units bypass it. It shares compiled selector programs and private,
unowned declaration prototypes, never live rules. Every hit creates a fresh rule and declaration
owner; declaration writes replace immutable entry arrays. Nested-parent-dependent rules bypass the
cache. Child source slices always come from the current input. Media/supports decisions, imports,
attachments, matching results and cascade plans remain document-owned and current. This is not a
response/URL cache and changes no fetch, credentials, ETag, origin or content validation policy.

Values are text, not typed grammars. Custom properties inherit declared text. Ordinary values may
use bounded textual `var()` substitution with fallbacks (depth 32; expansion limit 1,000,000 characters).
It is not token substitution: no declaration-scope resolution, deferred shorthand evaluation, typed
functions, URL resolution or unit computation. Colors stay declared text in the cascade; `ResolvedStyle`
serializes color longhands as `rgb()`/`rgba()` only at the CSSOM and DevTools read boundary. A missing/cyclic expansion defaults
the property rather than selecting an earlier declaration. Substitution-produced rollback keywords
remain text. Simple display blockification stays; overflow axes no longer compute together.

The data catalog supplies a small initial-value table and inherited flags; other known names have
empty initial text. Only layout-used shorthands expand by whitespace splitting. `ResolvedStyle`
returns synthetic box width/height where available and text otherwise. Text extraction interprets
white-space keywords locally. DevTools and CSSOM consume the same values and native rule identities.

Style, media, supports, layer, import and font-face rules retain real models and mutation identities.
Other at-rules are plain CSSRule objects preserving cssText and never entering the cascade.
Font-face descriptors remain passive text. CSS.supports tests known/custom names and nonempty values,
with the existing condition/selector parser, not property grammars.

Keep `CssValueWork` and selector work checks in surviving loops. Cancellation and reentrant edits
must not publish obsolete state. Computed declarations stay read-only and live across later reads;
borrowed cross-realm mutator exception branding remains separate binding work.
