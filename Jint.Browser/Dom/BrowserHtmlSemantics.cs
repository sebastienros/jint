using System.Runtime.CompilerServices;
using Jint.HtmlParser;
using Jint.Native;

namespace Jint.Browser.Dom;

internal enum BrowserContentEditableState { Inherit, True, False, PlaintextOnly }

/// <summary>Browser HTML semantics over current native attributes and ordinary parent links.</summary>
internal static class BrowserHtmlSemantics
{
    private static readonly ConditionalWeakTable<Element, MenuAssignment> Menus = new();
    private sealed class MenuAssignment { internal Element? Menu; }

    // HTML §6.8.1: enumerated keywords do not trim whitespace. Shared with editor host lookup.
    internal static BrowserContentEditableState ContentEditableState(string? value, DomReadWork? work)
    {
        if (value is null) return BrowserContentEditableState.Inherit;
        if (value.Length == 0 || Equal(value, "true", work)) return BrowserContentEditableState.True;
        if (Equal(value, "false", work)) return BrowserContentEditableState.False;
        return Equal(value, "plaintext-only", work) ? BrowserContentEditableState.PlaintextOnly : BrowserContentEditableState.Inherit;
    }

    private static bool Equal(string value, string keyword, DomReadWork? work)
    {
        if (work is not null) return work.EqualAsciiIgnoreCase(value, keyword);
        if (value.Length != keyword.Length) return false;
        for (var i = 0; i < value.Length; i++)
        {
            var c = value[i];
            if (c is >= 'A' and <= 'Z') c = (char) (c + ('a' - 'A'));
            if (c != keyword[i]) return false;
        }
        return true;
    }

    internal static string GetContentEditable(DomRealm realm, Element element)
    {
        var work = Work(realm);
        var state = ContentEditableState(work.Attribute(element, "contenteditable"), work);
        work.Check();
        return state switch { BrowserContentEditableState.True => "true", BrowserContentEditableState.False => "false", BrowserContentEditableState.PlaintextOnly => "plaintext-only", _ => "inherit" };
    }

    internal static JsValue SetContentEditable(DomRealm realm, Element element, string value)
    {
        var work = Work(realm);
        string? canonical = null;
        if (!work.EqualAsciiIgnoreCase(value, "inherit"))
        {
            canonical = ContentEditableState(value, work) switch
            {
                BrowserContentEditableState.True when value.Length != 0 => "true",
                BrowserContentEditableState.False => "false",
                BrowserContentEditableState.PlaintextOnly => "plaintext-only",
                _ => null,
            };
            work.Check();
            if (canonical is null) return DomFailures.Refuse(realm, "HTMLElement.contentEditable", "SyntaxError", "Invalid contenteditable state.");
        }
        work.Check();
        if (canonical is null) element.RemoveAttributeNS(null, "contenteditable");
        else element.SetAttributeNS(null, "contenteditable", canonical);
        work.Check();
        return JsValue.Undefined;
    }

    internal static bool IsContentEditable(DomRealm realm, Element element)
        => IsContentEditable(element, Work(realm));

    internal static bool IsContentEditable(Element element, DomReadWork work)
    {
        var result = false;
        for (Node? node = element; node is not null; node = node.ParentNode)
        {
            work.Step();
            if (node is not Element candidate) break;
            if (!IsEditableEligible(candidate)) break;
            if (candidate.NamespaceUri != Namespaces.Html)
            {
                continue;
            }
            // A document child is an editing host in design mode, even with contenteditable=false.
            if (candidate.ParentNode is Document document && DomDocumentState.IsDesignModeEnabled(document))
            {
                result = true;
                break;
            }
            var state = ContentEditableState(work.Attribute(candidate, "contenteditable"), work);
            if (state == BrowserContentEditableState.Inherit) continue;
            result = state != BrowserContentEditableState.False;
            break;
        }
        work.Check();
        return result;
    }

    internal static bool IsEditableEligible(Element element)
        => element.NamespaceUri == Namespaces.Html ||
           element.NamespaceUri == Namespaces.Svg && element.LocalName == "svg" ||
           element.NamespaceUri == Namespaces.MathMl && element.LocalName == "math";

    // HTML §6.8.5 permits a user-agent default. This headless implementation chooses false.
    internal static bool GetSpellcheck(DomRealm realm, Element element)
    {
        var work = Work(realm);
        var value = work.Attribute(element, "spellcheck");
        var result = value is not null && (value.Length == 0 || work.EqualAsciiIgnoreCase(value, "true"));
        work.Check();
        return result;
    }

    // https://html.spec.whatwg.org/multipage/dom.html#the-translate-attribute
    internal static bool GetTranslate(DomRealm realm, Element element)
        => InheritedBoolean(realm, element, "translate", "yes", "no", true);

    private static bool InheritedBoolean(DomRealm realm, Element element, string attribute, string yes, string no, bool fallback)
    {
        var work = Work(realm);
        var result = fallback;
        for (Node? node = element; node is not null; node = node.ParentNode)
        {
            work.Step();
            if (node is not Element { NamespaceUri: Namespaces.Html } candidate) continue;
            var value = work.Attribute(candidate, attribute);
            if (value is null) continue;
            if (value.Length == 0 || work.EqualAsciiIgnoreCase(value, yes)) { result = true; break; }
            if (work.EqualAsciiIgnoreCase(value, no)) { result = false; break; }
        }
        work.Check();
        return result;
    }

    internal static JsValue SetSpellcheck(DomRealm realm, Element element, bool value) => SetBoolean(realm, element, "spellcheck", value ? "true" : "false");
    internal static JsValue SetTranslate(DomRealm realm, Element element, bool value) => SetBoolean(realm, element, "translate", value ? "yes" : "no");
    internal static JsValue SetDraggable(DomRealm realm, Element element, bool value) => SetBoolean(realm, element, "draggable", value ? "true" : "false");
    private static JsValue SetBoolean(DomRealm realm, Element element, string name, string value)
    {
        var work = Work(realm);
        element.SetAttributeNS(null, name, value);
        work.Check();
        return JsValue.Undefined;
    }

    // HTML §6.11.7. Objects currently have no actual image representation in the page resource model.
    internal static bool GetDraggable(DomRealm realm, Element element)
    {
        var work = Work(realm);
        var value = work.Attribute(element, "draggable");
        var result = work.EqualAsciiIgnoreCase(value, "true") ||
            (!work.EqualAsciiIgnoreCase(value, "false") && element.NamespaceUri == Namespaces.Html &&
             (element.LocalName == "img" || element.LocalName == "a" && work.Attribute(element, "href") is not null));
        work.Check();
        return result;
    }

    internal static string AccessKeyLabel(DomRealm realm, Element element)
    {
        Work(realm).Check();
        return "";
    }

    internal static JsValue ForceSpellCheck(DomRealm realm, Element element)
        => DomFailures.Refuse(realm, "HTMLElement.forceSpellCheck", "NotSupportedError", "No spelling provider is available.");

    internal static Element? GetContextMenu(DomRealm realm, Element element)
    {
        var work = Work(realm);
        Element? result = null;
        if (Menus.TryGetValue(element, out var assignment) && assignment.Menu is { } assigned) result = assigned;
        else if (work.Attribute(element, "contextmenu") is { Length: > 0 } id && element.OwnerDocument is { } document)
        {
            foreach (var candidate in NodeTraversal.DescendantElements(document, work.Check, work.Token))
            {
                if (!work.Equal(work.Attribute(candidate, "id"), id)) continue;
                if (candidate.NamespaceUri == Namespaces.Html && candidate.LocalName == "menu") result = candidate;
                break;
            }
        }
        work.Check();
        return result;
    }

    internal static JsValue SetContextMenu(DomRealm realm, Element element, Element? menu)
    {
        var work = Work(realm);
        if (menu is not null) Menus.GetValue(element, static _ => new()).Menu = menu;
        else Menus.Remove(element);
        work.Check();
        return JsValue.Undefined;
    }

    private static DomReadWork Work(DomRealm realm)
    {
        var work = new DomReadWork(realm.NativeReadCheckpoint, realm.CancellationToken);
        work.Check();
        return work;
    }
}
