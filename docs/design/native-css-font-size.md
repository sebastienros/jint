# Native font-size slice


> Historical design: the current [LightPanda-style CSS boundary](https://github.com/sebastienros/jint/blob/main/Jint.HtmlParser/README.md#renderless-css-boundary)
> supersedes typed-value, computation, registration, container and keyframe claims below.
> These are retained design notes, not instructions to restore removed features.

Authority: [CSS Fonts 4 §2.5](https://drafts.csswg.org/css-fonts-4/#font-size-prop),
[§2.5.1](https://drafts.csswg.org/css-fonts-4/#absolute-size-mapping), and
[CSS Values 4 §6.1.1](https://drafts.csswg.org/css-values-4/#font-relative-lengths).

Ordinary and keyframe declarations accept absolute/relative keywords and the existing typed
nonnegative length-percentage production. CSS-wide values and deferred substitutions use the
shared parser. `math` retains the explicit `font-size:mathml-scaling` obligation; this slice does
not implement MathML scaling or font shorthand.

Medium uses the host media snapshot's initial font size (default 16px). Absolute keyword factors
are 3/5, 3/4, 8/9, 1, 6/5, 3/2, 2 and 3; larger/smaller use 1.2 and its reciprocal. Font-size
percentages and em units use the computed parent. Rem uses the document root's computed size,
except on the root's font-size where it uses the initial size. Other properties' em units use
their own element's computed size. All completed font-size computations publish absolute px.

Relative font-size and inheritance dependencies use the query's existing iterative walk and
caches. Root computation is demand-driven and terminates at its initial rem basis. Absolute
declarations and unrelated reads do not warm font-size ancestors. Glyph metrics (ch/ex/cap/ic,
including root variants) and line metrics still require explicit host values.

`FontSizePropertyGrammarTests` covers parsing, exact negative rejection and pending boundaries.
`NativeCssFontSizeTests` covers keywords, root rem, parent percentages/em, own em, mixed math,
CSS-wide/deferred values, cache publication counts, demand isolation and cancellation across
8192 dependencies. Browser coverage observes live cascade changes while preserving specified
CSSOM values. Two previous missing-font-size fixtures now test the still-missing zero-advance
metric explicitly because em size is supplied by the completed cascade.

Percentage and keyword scaling separate binary exponents before multiplying/dividing finite
nonzero operands. Only the final scale may overflow or underflow; existing zero/nonfinite math
and CSS serialization policies remain unchanged. Regressions cover large percentages, large
computed parents and keyword bases, and subnormal percentages combined with large finite bases.
