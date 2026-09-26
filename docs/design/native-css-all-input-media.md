# Native all-input media features

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
