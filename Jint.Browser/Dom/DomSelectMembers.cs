using System.Runtime.CompilerServices;
using Jint.Browser.Dom.Collections;
using Jint.HtmlParser;
using Jint.Native;
using Jint.Runtime;

namespace Jint.Browser.Dom;

/// <summary>HTML §4.10.7 conversions around the one native selectedness and option inventory.</summary>
internal static class DomSelectMembers
{
    internal static Jint.HtmlParser.HtmlSelectState State(DomRealm realm, Element element)
    {
        realm.Engine.Constraints.Check();
        return element.GetHtmlState()!.GetSelectState(realm.NativeReadCheckpoint, realm.CancellationToken)!;
    }

    // https://html.spec.whatwg.org/multipage/form-elements.html#dom-select-add
    internal static JsValue Add(DomRealm realm, Element select, JsValue[] arguments, string member)
    {
        if (arguments.Length == 0 || arguments[0] is not DomNodeObject wrapper ||
            wrapper.Node is not Element { NamespaceUri: Namespaces.Html, LocalName: "option" or "optgroup" } added)
        {
            DomBindings.Argument<Element>(arguments, 0, member, "HTMLOptionElement");
            return JsValue.Undefined;
        }
        var options = State(realm, select).Options;
        var before = arguments.Length > 1 ? arguments[1] : JsValue.Undefined;
        if (before is DomNodeObject beforeNode && beforeNode.Implements("HTMLElement"))
            options.Add(added, DomBindings.NullableArgument<Element>(arguments, 1, member, "HTMLElement"), realm.NativeReadCheckpoint, realm.CancellationToken);
        else if (before.IsNullOrUndefined()) options.Add(added, (Element?) null, realm.NativeReadCheckpoint, realm.CancellationToken);
        else options.Add(added, TypeConverter.ToInt32(before), realm.NativeReadCheckpoint, realm.CancellationToken);
        realm.Engine.Constraints.Check();
        realm.CancellationToken.ThrowIfCancellationRequested();
        return JsValue.Undefined;
    }

    internal static JsValue Remove(DomRealm realm, Element select, int index)
    {
        State(realm, select).Options.Remove(index, realm.NativeReadCheckpoint, realm.CancellationToken);
        realm.Engine.Constraints.Check();
        return JsValue.Undefined;
    }
}

/// <summary>Native live options projected through the existing HTMLCollection wrapper class.</summary>
internal class DomSelectCollection(DomRealm realm, Element select, bool selectedOnly) : DomHtmlCollection<Element>
{
    private static readonly ConditionalWeakTable<DomRealm, ConditionalWeakTable<Element, Views>> _views = new();
    private sealed class Views
    {
        internal DomSelectOptionsCollection? Options;
        internal DomSelectCollection? Selected;
    }
    internal Element Select => select;
    private HtmlSelectOptions Native(DomRealm caller) => selectedOnly
        ? DomSelectMembers.State(caller, select).SelectedOptions : DomSelectMembers.State(caller, select).Options;

    internal static DomSelectCollection Of(DomRealm realm, Element select, bool selectedOnly)
    {
        var views = _views.GetValue(realm, static _ => new()).GetValue(select, static _ => new());
        return selectedOnly ? views.Selected ??= new(realm, select, true) : views.Options ??= new(realm, select);
    }

    internal override int Length => GetLength(realm);

    internal override int GetLength(DomRealm caller) => Native(caller).GetCount(caller.NativeReadCheckpoint, caller.CancellationToken);

    internal override Element? GetItem(uint index) => GetItem(realm, index);

    internal override Element? GetItem(DomRealm caller, uint index) => Native(caller).Item(index, caller.NativeReadCheckpoint, caller.CancellationToken);

    public override IEnumerator<Element> GetEnumerator() => Read(realm).GetEnumerator();

    internal override IEnumerable<Element> Read(DomRealm caller)
    {
        var options = Native(caller);
        for (uint index = 0; options.Item(index, caller.NativeReadCheckpoint, caller.CancellationToken) is { } option; index++)
        {
            caller.Engine.Constraints.Check();
            yield return option;
        }
        caller.CancellationToken.ThrowIfCancellationRequested();
    }
}

internal sealed class DomSelectOptionsCollection(DomRealm realm, Element select) : DomSelectCollection(realm, select, false);
