using System.Runtime.CompilerServices;
using Jint.HtmlParser;

namespace Jint.Browser.Dom;

/// <summary>
/// HTML's
/// <a href="https://html.spec.whatwg.org/multipage/urls-and-fetching.html#nonce-attributes">[[CryptographicNonce]]</a>
/// internal slot, which is what the <c>nonce</c> IDL attribute of every element including
/// <c>HTMLOrSVGElement</c> answers — and the one member in this package that is deliberately <b>not</b>
/// reflection.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why a slot and not a reflected attribute.</b> HTML §2.5.3 gives <c>nonce</c> a getter that returns the
/// slot and a setter that writes <i>only</i> the slot, leaving the content attribute exactly as it was. That
/// is a security rule rather than an accident: a page whose Content Security Policy was delivered in a header
/// has its <c>nonce</c> content attributes emptied once the element becomes browsing-context connected, so
/// that a CSS selector such as <c>script[nonce]</c> cannot exfiltrate the nonce a script needs. A
/// <see cref="ReflectedAttribute"/> whose only storage is the content attribute cannot express that, and
/// making it do so would put the value back where a selector can read it.
/// </para>
/// <para>
/// <b>Keyed on the native element</b>, because this assembly cannot add a field to one — the arrangement
/// <see cref="AriaElementReferences"/> and <c>Collections/DomTokenListMembers</c> already use — and nothing is
/// allocated for an element whose <c>nonce</c> nobody has touched.
/// </para>
/// </remarks>
internal static class CryptographicNonce
{
    private static readonly ConditionalWeakTable<Element, Slot> _slots = new();

    /// <summary>
    /// The element's <c>[[CryptographicNonce]]</c>: the content attribute for an element no IDL setter has
    /// touched, and the last value that setter was given otherwise.
    /// </summary>
    internal static string Get(Element element, string? attribute)
    {
        if (!_slots.TryGetValue(element, out var slot))
        {
            return attribute ?? "";
        }

        if (!string.Equals(attribute, slot.Attribute, StringComparison.Ordinal))
        {
            slot.Attribute = attribute;
            slot.Value = attribute ?? "";
        }

        return slot.Value;
    }

    /// <summary>
    /// "On setting, set this's <c>[[CryptographicNonce]]</c> to the given value" — and nothing else, which is
    /// the whole point of the member.
    /// </summary>
    internal static void Set(Element element, string value, string? attribute)
    {
        var slot = _slots.GetOrCreateValue(element);
        slot.Attribute = attribute;
        slot.Value = value;
    }

    /// <summary>One element's slot, and the content attribute it was last synchronised against.</summary>
    internal sealed class Slot
    {
        /// <summary>The slot's value; the empty string until something sets it.</summary>
        internal string Value { get; set; } = "";

        /// <summary>
        /// The content attribute as it stood when <see cref="Value"/> was last written, or
        /// <see langword="null"/> when the attribute was absent. A later read that finds anything else runs
        /// HTML's attribute change steps.
        /// </summary>
        internal string? Attribute { get; set; }
    }
}
