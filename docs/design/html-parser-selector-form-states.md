# C3a1: built-in disabledness and requiredness selectors

Dispatch contract, independently proposed and reviewed by Astra High, 2026-09-23.
Extends the [selector contract](html-parser-selectors.md), D7a2 native disabledness,
and the complete D7b1a input-type classifier. This internal slice adds exactly
`:enabled`, `:disabled`, `:required`, and `:optional`; it does not publish selectors.

Authority: [HTML pseudo-classes](https://html.spec.whatwg.org/multipage/semantics-other.html#pseudo-classes)
and [Selectors disabled-state delegation](https://drafts.csswg.org/selectors-4/#enableddisabled).
Form-associated custom elements remain an explicit native-state prerequisite before full
selector publication. Ordinary custom names do not acquire disabledness from attributes.

## Ownership and semantics

Own SelectorMatcher.cs, new SelectorMatcher.FormStates.cs, new HtmlRequiredness.cs,
and new SelectorFormStateTests.cs. The only change in HtmlDisabledness.cs is making
the existing GetState(Element, ref HtmlDisabledWork) overload internal. Slots owns
Node/Element/Attr/Document and its hooks; this dispatch touches none of them. Do not
change HtmlDisabledState, HtmlElementState, HtmlSelectAncestry or classifier tables.

Enabled/disabled compare the existing native result to Enabled/Disabled. Inapplicable
matches neither. Reuse native fieldset/first-legend and option/nearest-select answers.
Requiredness applies to exact lowercase HTML-namespace select and textarea, plus
input states whose classifier says RequiredApplies. Presence of the exact lowercase,
no-namespace required attribute distinguishes Required from Optional. Disabledness,
readonly, validation candidacy and another radio's required attribute do not alter it.
Non-applicable inputs match neither. XHTML in XML qualifies; namespace-less/foreign
or uppercase local-name lookalikes do not. Type values retain existing ASCII parsing.

Add internal HtmlRequiredState { Inapplicable, Optional, Required } and
HtmlRequiredness.GetState(Element, ref HtmlDisabledWork). Scan actual attributes once,
charging each visited attribute, collecting type/required using namespace/local-name
checks. Use HtmlInputTypes.Parse/Info; never call tokenless Get or duplicate its table.

Add exactly four predicate kinds to IsImplemented and MatchPredicate. Unsupported
predicates must still reject the whole program, including nested nonmatching branches.
Keep all other predicate completion gates and compiler acceptance unchanged.

## Work and lifetime

SelectorMatcher.Work owns one HtmlDisabledWork, forwarding selector steps and native
calls through that same instance by reference. Adapt the existing test checkpoint once
per invocation; the normal null-checkpoint path adds no callback allocation. Preserve
entry/exit checks, explicit checkpoints and invocation-local table-grid caches. Do not
reset the counter on each native call, losing accumulated sub-256 work.

No compiled-program or element result cache. Calls observe current attrs/ancestry and
reuse only the existing correctly invalidated first-legend cache. Queries must not
modify mutation stamps or cause documents to survive through compiled programs.

## Required validation

- All seven built-in disabledness kinds and all 22 input types with/without required.
  Hidden input is enabled but neither required nor optional. Disabled/readonly required
  controls stay required; a radio with no own required attr stays optional despite peers.
- Namespace/case/type spelling, namespaced lookalikes, detached/XML contexts, template
  and shadow boundaries; assigned-slot ancestry must not transfer disabledness.
- Nested fieldsets/first and later legends, reorder/removal/replacement, current option
  ancestry barriers. Literal expectations plus comparison with native disabledness.
- Reuse compiled programs after attrs, Attr.Value, replacement/removal, moves, adoption,
  and select/fieldset changes. Old result snapshots retain membership and identity.
- Every matcher/query entry, matched-branch specificity, is/where/not/has, filtered nth
  and reverse nth; col || td > input:required, col:has(|| td > input:disabled), and
  table:has(> colgroup > col || td > input:optional). col:enabled || td matches nothing.
- Deterministic cancellation inside long attr scans, deep ancestors and cold legend
  scans. Include fewer than 256 selector steps followed by fewer than 256 native steps
  whose sum crosses 256, so separate counters fail. Cover absent/late attrs, retry,
  and many controls sharing a wide fieldset without repeated prefix scans.
- Fresh Release selector/native and non-corpus tests on net8/net10. No timings, public
  API, Browser cutover, dependency changes, or placeholder answers for other families.
