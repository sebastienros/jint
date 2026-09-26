# XML notation reporting: bounded native and parser dispatch

Design supplement, 2026-09-23, to [XML parsing](html-parser-xml.md) and
[the standalone architecture](html-parser.md). This closes the notation-reporting gap exposed by the
pinned W3C `xmlconf/xmltest/xmltest.xml#valid-sa-076`; it also supplies enough information to check
`valid-sa-069` without discarding its notation output. Design only; no implementation or timing claim.
Integration source was inspected at `37c8a6712`.

## Decision and authoritative boundary

Add an immutable parse-provenance inventory on `Document` of **every notation declaration actually
read**, including unused declarations. [XML 1.0 fifth edition §4.7](https://www.w3.org/TR/xml/#Notations)
requires reporting names and external identifiers for declared notations referred to by an attribute
value, attribute definition or entity declaration. Returning all read declarations satisfies that
requirement without reference tracking, deferred lookup, or a new validator. The additional unused
declarations are an intentional API guarantee, not a claim that XML requires every unused declaration.

The pinned `xmlconf/sun/cxml.html` Second Canonical Form explicitly includes all declared notations,
which is broader than the required referred subset. Thus case `069` tests the additional inventory
guarantee; `076` tests required reporting even though its NOTATION attribute is `#IMPLIED` and absent
from the element. Their original OUTPUT assertions must become real comparisons after this change.

Keep [§5.1 nonvalidating processing](https://www.w3.org/TR/xml/#sec-conformance) and the existing no-fetch
policy. Neither a notation's SYSTEM/PUBLIC identifier nor an NDATA use requests retrieval or execution.
No configurable DTD validator, resolver, callback, registry of notation handlers, or Browser action is
introduced. Required-profile notation reporting cannot be waived as an observation gap.

## Exact public and internal surface

Namespace `Jint.HtmlParser`; shared owner adds `Parsing/XmlNotationDeclaration.cs`, native owner edits
`Dom/Document.cs` and the Document branch of `Dom/NodeCloner.cs`:

```csharp
/// One complete XML notation declaration encountered during parsing.
public readonly struct XmlNotationDeclaration
{
    public string Name { get; }
    public string? PublicId { get; }
    public string? SystemId { get; }
    public long Offset { get; }

    internal XmlNotationDeclaration(string name, string? publicId, string? systemId, long offset);
}

// Document: never null; no setter, live mutation, or user registration.
public IReadOnlyList<XmlNotationDeclaration> XmlNotations { get; }

// Native-owned internal publication/copy; XML owner consumes these.
internal void PublishXmlNotations(List<XmlNotationDeclaration> records,
    CancellationToken cancellationToken);
internal void CopyXmlNotationsFrom(Document source);
```

`Name` is the case-preserved parsed notation name, with no delimiter or namespace prefix. The XML
scanner enforces fifth-edition Name plus the namespace profile's no-colon restriction. Names are not
HTML-folded, interned globally, or interpreted as namespace bindings. A default struct safely returns
empty Name, null identifiers and offset zero, as `XmlSkippedEntity` does; a default struct is not a
declaration produced by a successful parser.

Identifiers have the following precise contract:

| Declaration form | PublicId | SystemId |
| --- | --- | --- |
| `SYSTEM 's'` | null | `s` |
| `PUBLIC 'p'` | normalized `p` | null |
| `PUBLIC 'p' 's'` | normalized `p` | `s` |

An explicitly empty literal remains `""`, distinct from absence. Every emitted declaration has at
least one non-null identifier. Public identifiers use the XML public-literal grammar and whitespace
normalization: trim permitted surrounding whitespace and collapse internal runs to one ASCII space.
Reject characters excluded by PubidChar, including literal tab; do not use general Unicode Trim or
turn an invalid public literal into a valid one. See [XML §4.2.2](https://www.w3.org/TR/xml/#sec-external-ent).

System identifiers retain their parsed literal value, with XML input line-ending treatment and no
attribute whitespace collapse, URI resolution, percent-encoding, case changes or filesystem conversion.
Quotes are delimiters, not part of either identifier. Reference-looking text inside a system literal
is literal text, not an entity reference to expand. Parameter-entity replacement text has already
undergone the prescribed entity-value construction before its declaration is scanned; do not apply
another blanket decoding/normalization pass. Existing syntax/error policy still applies.

`Offset` is an original-input UTF-16 provenance position: the declaration's opening `<` when read
directly from the document, or the outermost original `%` invocation when read through one or more
parameter-entity replacement frames. It follows the existing omission/error anchoring convention,
not the declaration's position in a normalized or expanded buffer. Several declarations from one
replacement can share an offset; list order remains authoritative. Offsets need not be strictly
increasing. There is no invented source URI or negative-offset sentinel for unread material.

Only owned immutable strings and values survive parsing. No node reference, source buffer, declaration
slice, entity frame, parser, resolver or callback is retained. This is a minimal declaration inventory,
not a public DTD object model: no IsReferenced bit, origin enum, validity flag, or effective-binding map.

## Encounter order, duplicates and unread material

Append one record per complete declaration in parser encounter order, including declarations reached
through included internal parameter entities. Do not sort the result. Do not deduplicate repeated
identical declarations or choose an effective declaration by name. Repeating a parameter entity that
contains a notation therefore contributes another record with that invocation's provenance.

Duplicate notation names violate a validity constraint, not by themselves well-formedness. Preserve
the complete ordered records for a successful nonvalidating parse, including conflicting identifiers;
do not introduce a new fatal duplicate-name error. This explicit API choice resolves otherwise
undefined reporting for invalid duplicate declarations without pretending the document is valid.
A malformed duplicate declaration still fails normal syntax checking. No name-index allocation is
needed for this inventory. Corpus cases involving duplicate names need individual OUTPUT decisions;
neither a first-wins dictionary nor sorting away duplicates is justified by the two initial fixtures.

An unread external subset/parameter entity contributes **no** fabricated notation records. Existing
`SkippedXmlEntities` reports actual omissions. A notation merely declaring an external identifier adds
no omission record. The built-in named-character-entity catalog supplies no notation declarations;
do not manufacture notation data from its public identifier. An empty inventory means no notation
declaration was read, not proof that an unread DTD contained none.

Continue collecting syntactically complete notation declarations that are actually read after an
unread parameter entity. Section 5.1's suppression of later entity/attribute-list declarations does
not turn these observed notation declarations into unread material. The list is encounter provenance,
not a claim to know hidden declarations or their precedence. Preserve the existing suppression rules
for entity/default-attribute effects and standalone handling; this change must not reopen those lanes.
Do not create placeholders for referred notation names whose declaration was not read, or validate
that every referenced name has a declaration as part of this work.

## Publication, cloning and DOM mutation

The XML session owns a nullable builder, allocated at the first complete notation. Save the local name,
identifiers and provenance while parsing, but append only after the declaration's closing `>` and all
its grammar checks succeed. There is no success record for an unfinished declaration. Do not scan the
original source again to discover notations after parsing or parse the doctype a second time.

On successful full document parsing, freeze the records through `PublishXmlNotations` before returning
the document. `ParseXml` and `ParseSvg` follow the same contract. Internal fresh-document parsing uses
the same path; it must not reuse a document carrying earlier parse metadata. Publication is once per
parse into a fresh destination; misuse is an internal programming error. Parsing/limit/cancellation
failure returns no public partial document or notation result.

The native publication method copies into independently owned storage and exposes a read-only wrapper
that cannot be cast to a mutable array/list. Later builder mutation must not change the document.
Prepare and cancellation-check the snapshot before assigning its backing field. The no-notation path
uses a shared immutable empty list and no per-document collection allocation. No public setter or
generic parse-metadata dictionary is added.

`Document.CloneNode(false)` and `CloneNode(true)` preserve this immutable provenance, sharing its
snapshot if convenient, just as skipped-entity provenance is preserved. A shallow clone can therefore
have records without a cloned doctype; this is deliberate provenance rather than a live DTD view.
Importing/adopting an element, PI, fragment or doctype does not copy notation metadata to its destination.
Removing/replacing the doctype, editing attributes or moving nodes never rewrites the inventory.
Creating an empty XML/HTML document or a new template contents owner yields an empty list.

XML fragment parsing neither inherits DTD declarations nor overwrites its context document's inventory.
The fragment has no independent inventory API. Metadata publication/copy invokes no script, network,
observer callback or DOM mutation record and does not advance the DOM mutation stamp. Browser may
retain the native result as it does other parse provenance; this dispatch does not add JS bindings.

## Resource and cancellation contract

Use the existing inclusive `MaxInputCharacters`, `MaxTokenCharacters`, `MaxEntityExpansionCharacters`
and nesting rules. No separate notation count cap or hidden fixed limit is added. Declarations reached
through parameter entities consume the existing expansion budget exactly as other replacement input
does. Copying metadata must not charge expansion characters a second time.

Scanning names/literals, public-id normalization, record preparation and snapshot copying are linear
work with bounded cancellation polling. Reuse the XML work/cancellation protocol; no quadratic growing
string concatenation, rescan per earlier declaration or repeated immutable-array append. The builder
uses amortized growth. Result storage is proportional to declarations and retained identifier data,
including duplicate declarations; a deliberately unbounded parse remains unbounded.

Publication requires its CancellationToken. Copy record slots with bounded polling, check before and
after unavoidable runtime allocations/copies and before publication/return. Do not hide a long authored
loop in a single work charge, or use an uncancellable whole-list LINQ pipeline as the freeze step.
Runtime allocation/copy retains the established cooperative interruption limitation. Propagate the
original cancellation/limit exception, never a syntax error. Test cancellation during literal scans,
normalization, a long record sequence and the final snapshot copy using deterministic checkpoints;
do not use elapsed-time assertions or add a new public test hook.

## Finite acceptance and corpus handoff

Native/shared prerequisite tests cover default struct safety; a shared empty read-only result; exact
null/empty identifiers; builder isolation and attempted collection mutation; both Document clone
depths; no metadata transfer on node import/adoption; unchanged provenance after doctype removal; and
cancelled publication leaving the destination without a newly installed snapshot. Update the package
API snapshot and packed external consumer so a consumer can enumerate this result without internals.

XML tests cover SYSTEM, PUBLIC-only, PUBLIC-plus-SYSTEM and explicit empty identifiers; case-preserved
fifth-edition names; valid CR/CRLF public normalization and rejected tab; system-literal spacing and
reference-looking text; malformed/missing delimiters; no declaration, unused declarations, forward
notation references and referenced declarations; distinct and duplicate names; repeated/nested internal
PE inclusion; unread external subset/PE with exact omission records; direct declarations after an unread
PE; fragment owner preservation; original offsets and all relevant limit boundaries/cancellation.
Exercise each required reference route: a NOTATION attribute definition even without an attribute
instance, a notation-valued attribute, and an NDATA entity declaration, including declaration order
reversal. Reporting all read notations must not depend on a later reference being observed.
No notation identifier is retrieved, including absolute HTTP and file-looking strings.

Independently derived pinned fixture expectations, using original CRLF UTF-16 offsets:

| Case in `xmlconf/xmltest/xmltest.xml` | XmlNotations, in order | SkippedXmlEntities |
| --- | --- | --- |
| `valid-sa-069` | `("n", "whatever", null, 43)` | empty |
| `valid-sa-076` | `("n1", null, "http://www.w3.org/", 87)`, `("n2", null, "http://www.w3.org/", 131)` | empty |

Both retain doctype `doc` with empty public/system identifiers, followed by an empty no-namespace `doc`
element with no attributes. In `076`, `a` is #IMPLIED and is not synthesized. Keep those tree assertions.

The corpus owner changes SecondCanonicalForm to consume **actual public XmlNotations**, sorting only
the test serialization according to the pinned canonical form. It must not recover declarations from
the input or expected OUTPUT, or simply remove its observation-gap guard. Emit and compare the original
notation declarations in the pinned OUTPUT for `069` and `076`; only then retire their observation gaps.
Public-id normalization and canonical system-identifier formatting belong at their respective layers:
the public inventory is not a corpus-specific serialization. Do not quietly normalize unrelated future
OUTPUT mismatches or change any upstream bytes/pins.

Required negative probes preserve record count but change a name, identifier, null-versus-empty state
or provenance offset; metadata assertions must reject each. A wrong actual notation name/identifier
must also fail the real OUTPUT comparison. Cover an unexpected extra/missing declaration and order
corruption independently of canonical sorting. Ordinary DOM-only projection checks are insufficient.

Dispatch in order: native/shared value and snapshot seam with its tests/API consumer; XML population
with unit regressions; corpus serialization and both exact OUTPUT comparisons. Run freshly compiled
Release tests on both project TFMs and the complete pinned 2,585-case census. New failures remain
classified obligations. No notation subset, binary acceptance alone or reviewed waiver completes that
gate, and this change does not replace the architecture's full standalone/Browser/benchmark gates.
