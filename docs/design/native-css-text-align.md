# Native text-align slice


> Historical design: the current [LightPanda-style CSS boundary](https://github.com/sebastienros/jint/blob/main/Jint.HtmlParser/README.md#renderless-css-boundary)
> supersedes typed-value, computation, registration, container and keyframe claims below.
> These are retained design notes, not instructions to restore removed features.

Authority: [CSS Text 4 §7.1](https://drafts.csswg.org/css-text-4/#text-align-property),
[§7.3](https://drafts.csswg.org/css-text-4/#text-align-all-property) and
[§7.4](https://drafts.csswg.org/css-text-4/#text-align-last-property).

Text-align is a shorthand over inherited text-align-all and text-align-last, whose initial values
are start and auto. Common keywords reset the last-line value to auto. Justify-all sets both to
justify; match-parent sets both to match-parent. The all longhand accepts start/end, left/right,
center, justify and match-parent; last also accepts auto. Alignment strings remain explicitly
pending as `text-align:alignment-string`. This slice supplies declaration and computed values,
with no text layout implementation.

Match-parent resolves the corresponding parent's computed longhand. Only retained start/end
values are converted to left/right, using the parent's computed direction. Inheritance retains
logical values; it does not apply the child's direction. Root match-parent computes to start.
Dependencies use the query's iterative parent walk, so deep trees remain cancellation-bounded.

Inverse serialization emits all when last is auto and all is not match-parent, justify-all when both are justify,
match-parent when both are match-parent, and empty for other nonrepresentable pairs. The shared
declaration block preserves mixed-importance and CSS-wide restrictions, pending shorthand identity,
partial overrides, and reset behavior. The TextAlign serializer hook is included in
`CssDeclarationBlock.ShorthandValue`; expansion uses the generic component path. The
nonrepresentable match-parent/auto pair serializes as separate longhands and preserves both
values through a CssText roundtrip.

Fixtures: `CssTextAlignDeclarationTests`, native `NativeCssTextAlignTests`, and Browser
`Views.NativeCssTextAlignTests`. They cover specified reconstruction, importance, resets,
pending substitution, logical versus physical alignment under parent/child direction differences,
root defaults and 8192-element dependencies/cancellation.
