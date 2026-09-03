using AngleSharp.Html.Dom;
using Jint.Native;

namespace Jint.Browser.Dom;

/// <summary>
/// The members whose IDL parameter is a <a href="https://webidl.spec.whatwg.org/#idl-union">union of two
/// interfaces</a>, which AngleSharp models as two overloads sharing one <c>[DomName]</c>.
/// </summary>
/// <remarks>
/// <para>
/// The generator binds one <c>[DomName]</c> to one member, so the second overload was reported as a collision
/// and lost: <c>select.add(optgroup)</c> was a <c>TypeError</c> where a browser accepts it, and so was
/// <c>select.options.add(optgroup)</c>. There is nothing in AngleSharp's metadata that says the two methods
/// are one IDL operation, so the union is written here and the two members are <c>skip</c> + <c>additions</c>
/// entries in the override table — which is also what makes the collision a decision rather than a
/// diagnostic.
/// </para>
/// <para>
/// The dispatch is on the argument's <em>interface</em> and in the order Web IDL's overload resolution takes:
/// the most derived first. An argument that is neither half is the ordinary conversion failure, raised by
/// <see cref="DomBindings.Argument{T}"/> against the option half so that the message names one expected type
/// rather than none.
/// </para>
/// </remarks>
internal static class DomUnionMembers
{
    /// <summary>
    /// https://html.spec.whatwg.org/multipage/form-elements.html#dom-select-add —
    /// <c>add((HTMLOptionElement or HTMLOptGroupElement) element, optional (HTMLElement or long)? before = null)</c>.
    /// </summary>
    internal static JsValue SelectAdd(DomBinding<IHtmlSelectElement> self, JsValue[] arguments)
    {
        var before = DomBindings.NullableArgument<IHtmlElement>(arguments, 1, "HTMLSelectElement.add");

        if (Target<IHtmlOptionsGroupElement>(arguments) is { } group)
        {
            self.Target.AddOption(group, before);
            return JsValue.Undefined;
        }

        self.Target.AddOption(DomBindings.Argument<IHtmlOptionElement>(arguments, 0, "HTMLSelectElement.add"), before);
        return JsValue.Undefined;
    }

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/common-dom-interfaces.html#dom-htmloptionscollection-add —
    /// the same union, on the collection.
    /// </summary>
    internal static JsValue OptionsAdd(DomBinding<IHtmlOptionsCollection> self, JsValue[] arguments)
    {
        var before = DomBindings.NullableArgument<IHtmlElement>(arguments, 1, "HTMLOptionsCollection.add");

        if (Target<IHtmlOptionsGroupElement>(arguments) is { } group)
        {
            self.Target.Add(group, before);
            return JsValue.Undefined;
        }

        self.Target.Add(DomBindings.Argument<IHtmlOptionElement>(arguments, 0, "HTMLOptionsCollection.add"), before);
        return JsValue.Undefined;
    }

    /// <summary>The first argument's AngleSharp object when it is a <typeparamref name="T"/>, else null.</summary>
    private static T? Target<T>(JsValue[] arguments) where T : class
        => arguments.Length > 0 && arguments[0] is IDomWrapper wrapper ? wrapper.DomTarget as T : null;
}
