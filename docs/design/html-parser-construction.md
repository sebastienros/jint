# Trusted parser construction seam

Immediate native-owner prerequisite for XML core integration, shared with later HTML construction.
This supplements [native follow-ups](html-parser-native-followups.md) and
[D5 mutation tracking](html-parser-mutations.md). Keep public DOM factories/mutation methods unchanged.
No parser feature flag, generic `skipValidation` argument or public unchecked API is added.

## Exact internal surface

Namespace `Jint.HtmlParser`. Native owner edits Document/Element/Node and adds `Dom/ParserAttribute.cs`;
XML/HTML owners consume these members without editing their implementation:

```csharp
// Document: receiver is the actual destination node document.
internal Element CreateParsedElement(string? namespaceUri, string localName, string? prefix);
internal CDataSection CreateParsedCDataSection(string data);

internal readonly struct ParserAttribute
{
    internal ParserAttribute(string? namespaceUri, string localName, string? prefix, string value);
    internal string? NamespaceUri { get; }
    internal string LocalName { get; }
    internal string? Prefix { get; }
    internal string Value { get; }
}

// Element: one initial batch, before publication; receiver has no attributes.
internal void InitializeParsedAttributes(ReadOnlySpan<ParserAttribute> attributes);
// Node: resolved destination; append one newly created node, never a fragment/subtree.
internal void AppendParsedChild(Node child);
```

Names and values are owned immutable strings. Namespace absence is null, never an empty string;
Prefix is null or nonempty. The parser supplies format-validated/resolved name components and decoded
values, including format-specific folding/foreign adjustments already completed. It proves duplicate
expanded attribute names absent and source attribute order final before calling the batch method.
Native methods do not concatenate/split a qualified name, fold it again, rescan a name against the
public factory grammar, resolve namespaces or apply another duplicate policy. Do not manufacture
`QualifiedName.Parse` calls inside this lane.

This distinction is semantic as well as performance-related. XML `<xmlns/>` is a legal no-namespace
element; the validated element factory must preserve it even though the existing public
`CreateElementNS(null, "xmlns")` path rejects it. XML fragments may be parsed with an HTML-owned
context; CreateParsedCDataSection retains that actual owner without applying the public
CreateCDataSection HTML-document prohibition. Scanner well-formedness checks, including CDATA
termination, remain mandatory. Neither case changes the public DOM factory behavior. Include both
regressions in native and XML integration tests.

## Preconditions and effect boundaries

CreateParsedElement uses the same intrinsic element initialization as other native creation paths,
including the correct TemplateContent identity/owner for an HTML-namespace lowercase template. It
returns a fresh detached empty element, with no user/host code invoked. A name containing a prefix is
stored with that prefix unchanged; element and attribute NamespaceUri/LocalName/Prefix remain exact.

InitializeParsedAttributes requires a fresh, unpublished element with no attributes, registrations or
host exposure. Allocate attribute storage once for the batch, create each Attr with the receiver's
OwnerDocument and OwnerElement, and append in supplied order. No lookup for each insertion. Perform
required native per-attribute initialization/semantic updates in order with old value absent; do not
leave id/class/control/style state stale merely because no observer can yet exist. Keep the operation
linear in attributes plus their data/required intrinsic work. Never retain the caller's span/array.

AppendParsedChild requires a fresh, unpublished, detached node with no ordinary children, previous/
next siblings or registration, and the same node document as the receiver. Attributes already
initialized on a fresh Element and its empty intrinsic TemplateContent are allowed. The receiver is
Document, Element or DocumentFragment. Child is neither Document nor DocumentFragment. Parser has
already proved destination kind/document shape, host-inclusive acyclicity by freshness, and limits.
The destination may already be observable (incremental parsing/document.write); freshness applies to
the child, not the entire document. Use constant-time precondition guards/assertions for directly
inspectable links, node kind and owner identity; do not reintroduce ancestor/document scans to verify
the trusted proof. Misuse is an internal programming error, not a repaired/recovered DOM operation.

Link at the end without CollectIncoming, ancestor walks, document-order reconstruction, detach or
adoption. Then execute the same insertion semantic bookkeeping, invalidation and appropriate D5
record production as ordinary insertion. Do not call the clone-only AppendClonedChild lane, suppress
observers, or bypass future intrinsic/range/host bookkeeping. Share the post-validation insertion core
so adding D5 consumers cannot accidentally update only the public method. The no-observer/no-host
structural path is constant work; required observer matching or intrinsic algorithms retain their own
costs. This contract does not promise constant cost for an observed or semantically complex insertion.

Before creation, the tree builder chooses the actual insertion destination (including TemplateContent)
and derives its node document. Create children/attributes with that owner from the outset. Append does
not redirect into template contents or silently adopt a wrong-owner child. Existing public methods
remain necessary for fragment drainage, reparenting, adoption-agency/foster-parent moves, or any child
that was exposed, observed, previously linked or supplied by a host/custom-element constructor. Do
not infer eligibility merely from ParentNode being null. No global freshness registry is required:
this is a narrow internal caller proof, backed by operation-specific tests and call-site review.

## Implementation and verification

Native owner lands this as a separate immediate commit after coordinating current Document edits;
metadata/template work need not finish first, but their implementation must use this same creation
core when it lands. XML owner then replaces fresh-node public append/attribute loops and name factories
with these calls, retaining its own namespace-map, duplicate-name, document-shape and limit checks.
HTML consumes the same seam only where its creation algorithm establishes the preconditions.

Review establishes why public append's repeated ancestor walks and repeated linear attribute lookup
would be quadratic, and confirms those loops are absent here. Test deep fresh construction, wide
attribute batches, links/counts/order, names/namespaces/spelling, both special XML cases above, ownership
precondition failures before mutation, and input-array reuse after initialization. Once D5/templates
land, test observable-parent insertion records, native invalidation and template destinations through
the fast lane too. Reparenting/fragment tests continue through validated public algorithms. Do not add
stopwatch thresholds or claim measured speedups; Release net8.0/net10.0 functional tests and direct
complexity review suffice for this prerequisite.
