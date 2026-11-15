using Jint.Browser.Events;
using Jint.HtmlParser;
using Jint.HtmlParser.Css;
using Jint.HtmlParser.Css.Selectors;

namespace Jint.Browser.Dom;

/// <summary>DOM scope matching over the native selector compiler and identity-preserving tree.</summary>
internal static class DomSelectors
{
    internal static Element? QuerySelector(DomRealm realm, Node root, string text)
    {
        var program = Compile(realm, text);
        var work = Work(realm, root);
        var environment = Environment(realm, root);
        return SelectorMatcher.QuerySelector(program, root, environment, ref work);
    }

    internal static Element[] QuerySelectorAll(DomRealm realm, Node root, string text)
        => QuerySelectorAll(realm, root, Compile(realm, text));

    internal static Element[] QuerySelectorAll(DomRealm realm, Node root, CompiledSelector program)
    {
        var work = Work(realm, root);
        var environment = Environment(realm, root);
        return SelectorMatcher.QuerySelectorAllSnapshot(program, root, environment, ref work);
    }

    internal static bool Matches(DomRealm realm, Element element, string text)
    {
        var program = Compile(realm, text);
        var work = Work(realm, element);
        var environment = Environment(realm, element);
        return SelectorMatcher.Matches(program, element, element, environment, ref work);
    }

    internal static Element? Closest(DomRealm realm, Element element, string text)
    {
        var program = Compile(realm, text);
        var work = Work(realm, element);
        var environment = Environment(realm, element);
        return SelectorMatcher.Closest(program, element, environment, ref work);
    }

    /// <summary>How many selector texts one realm remembers before it starts again.</summary>
    /// <remarks>
    /// A page asks a handful of selectors many times — axe-core asks <c>matches</c> and <c>closest</c> the same
    /// few hundred for every element of the page — and a page building selectors out of data would otherwise
    /// grow the table without bound. Starting again costs only recompilation.
    /// </remarks>
    internal const int MaxCompiledSelectors = 512;

    /// <summary>
    /// The compiled form of <paramref name="text"/>, compiled once per realm and reused.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Scope matching compiles with an empty namespace-prefix map, so the program is a function of the text
    /// alone, and a <see cref="CompiledSelector"/> is immutable but for memo fields the matcher fills in to be
    /// reused. Recompiling it for every <c>element.matches(s)</c> was 4% of an axe-core scan (#4619).
    /// </para>
    /// <para>
    /// A selector that does not parse is not remembered, so every call raises its own <c>SyntaxError</c>.
    /// </para>
    /// </remarks>
    internal static CompiledSelector Compile(DomRealm realm, string text)
    {
        realm.Engine.Constraints.Check();

        var cache = realm.CompiledSelectors;
        if (cache is not null && cache.TryGetValue(text, out var cached))
        {
            return cached;
        }

        try
        {
            // DOM §4.2.6 scope matching uses an empty namespace-prefix map. The compiler owns
            // forgiving lists, escapes and CSS Syntax's EOF recovery; no text rewriting is needed.
            var program = SelectorCompiler.Compile(text, null, realm.CancellationToken);
            realm.Engine.Constraints.Check();

            cache ??= realm.CompiledSelectors = new Dictionary<string, CompiledSelector>(StringComparer.Ordinal);
            if (cache.Count >= MaxCompiledSelectors)
            {
                cache.Clear();
            }

            cache[text] = program;
            return program;
        }
        catch (SelectorParseException exception)
        {
            throw new Jint.HtmlParser.DomException("SyntaxError", exception.Message);
        }
    }

    private static SelectorMatchWork Work(DomRealm realm, Node root)
    {
        BrowserSelectorControlFacts.PrepareControlFactsRead(realm, realm.NativeReadCheckpoint, realm.CancellationToken);
        return new(root, realm.CancellationToken, realm.Engine.Constraints.Check);
    }

    private static SelectorEnvironment Environment(DomRealm realm, Node root)
    {
        var document = root as Document ?? root.OwnerDocument!;
        var events = BrowserEventRealm.Of(realm.Engine);
        var focused = events.FocusedElement;
        var pressed = events.MousePressTarget;
        var hovered = events.MouseHoverTarget;
        if (focused?.OwnerDocument != document) focused = null;
        if (pressed?.OwnerDocument != document) pressed = null;
        if (hovered?.OwnerDocument != document) hovered = null;
        var target = DomDocumentState.Of(document).TargetElement;
        if (target?.OwnerDocument != document) target = null;
        return new SelectorEnvironment(document, focused, pressed, target, BrowserSelectorControlFacts.Factory,
            realm, BrowserSelectorSemanticRevision.Read(document), hovered);
    }

}
