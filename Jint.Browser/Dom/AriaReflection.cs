using Jint.HtmlParser;
using Jint.Native;
using Jint.Runtime;

namespace Jint.Browser.Dom;

/// <summary>
/// <a href="https://w3c.github.io/aria/#ARIAMixin">ARIA §10.1's <c>ARIAMixin</c></a>, spliced onto
/// <c>Element</c> by the binding generator's <c>additions</c> extend form: <c>role</c> and the
/// <c>aria-*</c> IDL attributes, each reflecting its content attribute.
/// </summary>
/// <remarks>
/// <para>
/// <b>The type is <c>DOMString?</c>, so absence is <c>null</c> and not <c>""</c>.</b> That is the opposite of
/// the conversion table's default for a projected string member and deliberate: <c>aria-checked</c> being
/// absent and <c>aria-checked=""</c> are different states to an accessibility tree, and
/// <c>el.ariaChecked = null</c> is how a page returns to the first. Setting <c>undefined</c> does the same,
/// which is WebIDL's conversion of <c>undefined</c> to a nullable type.
/// </para>
/// <para>
/// <b>The element-reflection half is <see cref="AriaElementReflection"/>.</b> <c>ariaActiveDescendantElement</c>,
/// <c>ariaControlsElements</c>, <c>ariaDescribedByElements</c> and their five siblings reflect an element or a
/// frozen array of elements and carry an explicitly-set value that outlives the target leaving the tree, which
/// is bookkeeping rather than a view — so it is a file of its own, declared onto this same shape.
/// </para>
/// </remarks>
internal static class AriaReflection
{
    /// <summary>
    /// The mixin's string half, IDL attribute to content attribute, in the standard's own order.
    /// <c>role</c> is the one whose <c>[Reflect]</c> carries no name, because the two are spelled the same.
    /// </summary>
    private static readonly (string Member, string Attribute)[] _reflected =
    [
        ("role", "role"),
        ("ariaAtomic", "aria-atomic"),
        ("ariaAutoComplete", "aria-autocomplete"),
        ("ariaBrailleLabel", "aria-braillelabel"),
        ("ariaBrailleRoleDescription", "aria-brailleroledescription"),
        ("ariaBusy", "aria-busy"),
        ("ariaChecked", "aria-checked"),
        ("ariaColCount", "aria-colcount"),
        ("ariaColIndex", "aria-colindex"),
        ("ariaColIndexText", "aria-colindextext"),
        ("ariaColSpan", "aria-colspan"),
        ("ariaCurrent", "aria-current"),
        ("ariaDescription", "aria-description"),
        ("ariaDisabled", "aria-disabled"),
        ("ariaExpanded", "aria-expanded"),
        ("ariaHasPopup", "aria-haspopup"),
        ("ariaHidden", "aria-hidden"),
        ("ariaInvalid", "aria-invalid"),
        ("ariaKeyShortcuts", "aria-keyshortcuts"),
        ("ariaLabel", "aria-label"),
        ("ariaLevel", "aria-level"),
        ("ariaLive", "aria-live"),
        ("ariaModal", "aria-modal"),
        ("ariaMultiLine", "aria-multiline"),
        ("ariaMultiSelectable", "aria-multiselectable"),
        ("ariaOrientation", "aria-orientation"),
        ("ariaPlaceholder", "aria-placeholder"),
        ("ariaPosInSet", "aria-posinset"),
        ("ariaPressed", "aria-pressed"),
        ("ariaReadOnly", "aria-readonly"),
        ("ariaRelevant", "aria-relevant"),
        ("ariaRequired", "aria-required"),
        ("ariaRoleDescription", "aria-roledescription"),
        ("ariaRowCount", "aria-rowcount"),
        ("ariaRowIndex", "aria-rowindex"),
        ("ariaRowIndexText", "aria-rowindextext"),
        ("ariaRowSpan", "aria-rowspan"),
        ("ariaSelected", "aria-selected"),
        ("ariaSetSize", "aria-setsize"),
        ("ariaSort", "aria-sort"),
        ("ariaValueMax", "aria-valuemax"),
        ("ariaValueMin", "aria-valuemin"),
        ("ariaValueNow", "aria-valuenow"),
        ("ariaValueText", "aria-valuetext"),
    ];

    /// <summary>Declares one accessor pair per reflected attribute on the <c>Element</c> shape.</summary>
    internal static void ElementAria(JsObjectShape.Builder builder)
    {
        foreach (var (member, attribute) in _reflected)
        {
            var accessor = new ReflectedAttribute(member, attribute);

            builder.Accessor(
                member,
                DomFailures.Guard(accessor.Member, accessor.Get),
                DomFailures.GuardMutation(accessor.Member, accessor.Set));
        }

        AriaElementReflection.Declare(builder);
    }

    /// <summary>
    /// One reflected attribute's get/set pair. It captures two strings and nothing else, so it is
    /// engine-independent — the requirement a shape member body carries, because one shape is instantiated
    /// for every engine.
    /// </summary>
    private sealed class ReflectedAttribute(string member, string attribute)
    {
        private readonly string _attribute = attribute;

        internal string Member { get; } = "Element." + member;

        internal JsValue Get(JsValue thisObject, JsValue[] arguments)
        {
            var self = DomBindings.Bind<Element>(thisObject, Member);

            return DomConvert.NullableText(self.Target.GetAttribute(_attribute));
        }

        internal JsValue Set(JsValue thisObject, JsValue[] arguments)
        {
            var self = DomBindings.Bind<Element>(thisObject, Member);
            using var mutation = self.Realm.MutateLayout();
            var value = arguments.Length > 0 ? arguments[0] : JsValue.Undefined;

            // https://html.spec.whatwg.org/multipage/common-dom-interfaces.html#reflecting-content-attributes-in-idl-attributes:
            // a nullable reflected attribute is removed when the IDL attribute is set to null, and undefined
            // reaches a `DOMString?` parameter as null.
            if (value.IsNullOrUndefined())
            {
                self.Target.RemoveAttribute(_attribute);
            }
            else
            {
                self.Target.SetAttribute(_attribute, TypeConverter.ToString(value));
            }

            return JsValue.Undefined;
        }
    }
}
