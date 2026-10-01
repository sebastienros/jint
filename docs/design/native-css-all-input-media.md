# Native all-input media features


> Historical design: the current [LightPanda-style CSS boundary](https://github.com/sebastienros/jint/blob/main/Jint.HtmlParser/README.md#renderless-css-boundary)
> supersedes typed-value, computation, registration, container and keyframe claims below.
> These are retained design notes, not instructions to restore removed features.

This finite slice implements `any-pointer`, `any-hover`, and `display-mode` on the existing
component parser and bounded postfix evaluator. The authorities are
[Media Queries 4 §7.3](https://drafts.csswg.org/mediaqueries-4/#any-input) and
[Media Queries 5 §4.9](https://drafts.csswg.org/mediaqueries-5/#display-mode), with boolean and
discrete feature syntax defined by Media Queries 4 §2.4.

The immutable native media snapshot keeps primary and all-input capabilities separate.
`any-pointer` accepts a union of coarse and fine capabilities. None is an explicit distinct value;
unavailable or invalid capability flags stay unknown even under negation. `any-hover` reads its
own none/hover snapshot. Display mode accepts fullscreen, standalone, minimal-ui, browser, and
picture-in-picture. Boolean queries are false for none and true for every display-mode value.
Discrete range syntax, min/max forms and unknown values do not become true under negation.

Browser integration must copy `PageMediaEnvironment.ValueOf` for each of these three features,
independently of primary pointer/hover. Its current emulation surface supplies one pointer keyword;
the native snapshot also supports both coarse and fine when a host has both. An invalid emulated
pointer keyword maps to Unknown, never to None. Existing page defaults remain fine/hover/browser.

`CssAllInputMediaTests` covers unions, independent primary input, invalid snapshots and syntax,
all five modes, and cancellation during a long postfix evaluation. `NativeCssAllInputMediaTests`
covers shared `@media`/`matchMedia` defaults, live emulation and reset. Other previously pending
media features retain their named incomplete-grammar boundaries.

The existing `Runtime.MediaQuery.Discrete` lane also validates both the host and requested
vocabularies for exactly these three features. Invalid values return unknown before boolean or
equality evaluation, so `matchMedia` cannot turn them into a true answer by negation. Browser
parity fixtures exercise invalid host snapshots and invalid requested values through actual
`@media` rules and `matchMedia`, including boolean queries and negation. This does not expand
the legacy evaluator's other syntax or feature vocabularies.
