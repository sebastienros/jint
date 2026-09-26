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

    internal static IReadOnlyList<Element> QuerySelectorAll(DomRealm realm, Node root, string text)
    {
        var program = Compile(realm, text);
        var work = Work(realm, root);
        var environment = Environment(realm, root);
        return SelectorMatcher.QuerySelectorAll(program, root, environment, ref work);
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

    private static CompiledSelector Compile(DomRealm realm, string text)
    {
        realm.Engine.Constraints.Check();
        try
        {
            // DOM §4.2.6 scope matching uses an empty namespace-prefix map. The compiler owns
            // forgiving lists, escapes and CSS Syntax's EOF recovery; no text rewriting is needed.
            var program = SelectorCompiler.Compile(text, null, realm.CancellationToken);
            realm.Engine.Constraints.Check();
            return program;
        }
        catch (SelectorParseException exception)
        {
            throw new Jint.HtmlParser.DomException("SyntaxError", exception.Message);
        }
    }

    private static SelectorMatchWork Work(DomRealm realm, Node root)
        => new(root, realm.CancellationToken, realm.Engine.Constraints.Check);

    private static SelectorEnvironment Environment(DomRealm realm, Node root)
    {
        var document = root as Document ?? root.OwnerDocument!;
        var events = BrowserEventRealm.Of(realm.Engine);
        var focused = events.FocusedElement;
        var pressed = events.MousePressTarget;
        if (focused?.OwnerDocument != document) focused = null;
        if (pressed?.OwnerDocument != document) pressed = null;
        var target = DomDocumentState.Of(document).TargetElement;
        if (target?.OwnerDocument != document) target = null;
        return new SelectorEnvironment(document, focused, pressed, target);
    }

}
