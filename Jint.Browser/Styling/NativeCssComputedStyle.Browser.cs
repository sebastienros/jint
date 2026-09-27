using Jint.HtmlParser;
using Jint.HtmlParser.Css.Selectors;

namespace Jint.Browser.Styling;

// The query-only view is also linked into native parser tests. Geometry ownership stays in
// the Browser assembly, while every related Browser view retains the same invocation context.
internal sealed partial class NativeCssComputedStyle
{
    internal NativeCssComputedStyle(NativeCssQuery query, Element element, SelectorMatchWork matching,
        Action? witness, NativeCssReadContext? readContext) : this(query, element, matching, witness)
        => ReadContext = readContext;

    internal NativeCssReadContext? ReadContext { get; private set; }

    partial void CopyBrowserReadContext(NativeCssComputedStyle view) => view.ReadContext = ReadContext;
}
