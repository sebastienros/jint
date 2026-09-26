# Parsing and demand-driven DOM behavior

Astra design amendment, September 25, following the user's explicit request to keep HTML parsing
fast and materialize enhanced models only when necessary. This supersedes wording that requires
physical control-value initialization at element creation. Observable behavior remains unchanged.

## Dependency boundary

| Layer | Responsibility | Trigger |
| --- | --- | --- |
| HTML/XML/SVG parser and core DOM | Tokenization, tree construction, decoded attributes/text, identity, mutation hooks and necessary parser metadata | Parsing and structural mutation |
| HTML behavior | Control current/dirty state, selection, reflected and derived facts | Semantic access or a mutation that requires preserving history |
| Pure value helpers | Numeric/date/time parsing, formatting and stepping arithmetic | Explicit control/value operation; never attribute presence alone |
| CSS parser | Syntax, selectors and property grammars, including flex declarations | Explicit CSS parsing or Browser stylesheet activation |
| Browser style and layout | Cascade, computed styles, environment-dependent resolution and geometry | Browser demand; never HTML tree construction |

Keep one authoritative DOM. No eager/lazy configuration switch, second tree, parallel value store,
or replay log is needed. Logical dependency separation comes first; sharing an assembly does not
require executing its code. A physical package split can follow a stable, useful API boundary.
Browser references the parser; the parser must not reference Browser styling or layout.

Parsing `input[type=date]` retains its value attribute as a string. It does not convert a date,
create numeric constraints, or validate the control. Parsing `style` attributes or style-element
text does not create declaration blocks, compute colors, run the cascade, or perform flex layout.
Explicit CSS property parsing validates flex grammar; it does not place or size any element.

## Lazy semantic state

Initial state must be observationally equivalent to the HTML initialization algorithms. Physical
sanitization and derived model allocation may wait until the first semantic observation or before
the first operation that would destroy information needed by those algorithms.

- Fresh parsed inputs leave their value sidecar absent. Preparing a parsed attribute batch refreshes
  an already observed sidecar atomically, but does not create an unobserved one.
- Raw attribute reads, serialization and unrelated attribute writes do not create value state.
- First semantic access uses the caller's cancellation/work budget and publishes only a complete
  state. Subsequent access reuses that state; numeric/temporal structs are derived caches.
- Relevant mutations update existing state. A cold state is materialized before a history-sensitive
  transition when equivalence cannot otherwise be proved. Type changes and email `multiple`
  transitions cannot be reconstructed indiscriminately from final attributes.
- A clone of a cold, pristine input remains cold. A materialized source copies authoritative current
  value and dirty state, preserving the existing clone rules rather than recomputing from attributes.
- Derived select metadata and collection views should be lazy. Selectedness and peer-exclusion
  history require a separate careful reduction to minimal state; they cannot simply be discarded.

Necessary history remains: parser form-pointer association, script parser flags, radio exclusion,
and option selection may differ from what final attributes alone imply. Removing the winning radio
must not resurrect an earlier radio whose `checked` attribute remains present. Actual tree changes
required by parsing still occur; lazy evaluation must not suppress observable DOM effects.

Browser owns form-validation/reset orchestration, events, custom validity messages and stylesheet
lifecycle. Event-free native control operations retain one current-value/dirty/selection store.
Browser computed-style work is demand-driven and cached only with valid freshness information;
requesting one property must not eagerly compute every unrelated property.

## Verification

Deterministic tests inspect instance state and scoped work probes: parsing many text/number/date
inputs must create zero input value sidecars and perform zero numeric/temporal conversions. Raw DOM
inspection stays cold; first semantic access initializes once; cancellation publishes nothing.
Compare initially observed and unobserved controls through type/value/multiple changes, reset,
clone/import/adoption, radio exclusion and parser form association. Verify that ordinary HTML parsing
creates no CSS declaration model or computed style.

Benchmark parse-only work separately from first semantic access and repeated warm access, including
documents with many controls and style attributes. Paired AngleSharp comparisons must perform the
same observable work. Allocation and throughput claims still require the repository's normal
benchmark protocol; correctness counters are not timing measurements.
