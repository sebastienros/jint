using System.Runtime.CompilerServices;
using AngleSharp.Dom;
using Jint.Native;
using Jint.Runtime;
using Jint.WebApi.DomException;

namespace Jint.Browser.Dom.Collections;

/// <summary>
/// <a href="https://dom.spec.whatwg.org/#interface-domtokenlist">DOM §7.1</a>'s <c>DOMTokenList</c>, over the
/// token set AngleSharp's <c>ITokenList</c> keeps.
/// </summary>
/// <remarks>
/// <para>
/// <b>What AngleSharp owns is still the token set and the attribute it reflects</b> — the list is live, it is
/// bound to the element's attribute in both directions, and nothing here parses or stores markup. What is
/// here is the half of §7.1 the interface has no room for: the <i>validation steps</i>, which
/// <c>ITokenList</c> runs for none of its members; <c>toggle</c>'s distinction between a <c>force</c> that
/// was given as <see langword="false"/> and one that was not given at all, which a CLR
/// <c>bool force = false</c> parameter cannot express; <c>replace</c> and <c>supports</c>, which
/// <c>ITokenList</c> does not have; <c>item</c>, which WebIDL's indexed getter answers <c>null</c> for rather
/// than throwing; and the <i>update steps</i>, which rewrite the attribute in serialized form even when the
/// token set did not change.
/// </para>
/// <para>
/// <b>The associated element is recorded on the way through the accessor that projects the list</b>, because
/// <c>ITokenList</c> names neither the element nor the attribute it reflects and §7.1's <c>value</c>,
/// stringifier and update steps are all defined in terms of both. Seven accessors project one — <c>classList</c>,
/// the three <c>relList</c>s, <c>sandbox</c>, <c>sizes</c> and <c>htmlFor</c> — and each is a <c>skip</c> plus an
/// <c>additions</c> entry that calls <see cref="Project"/>. A list reached any other way keeps every member
/// but answers its <c>value</c> from the serialized token set, which differs from the attribute only when the
/// attribute has repeated tokens or irregular whitespace.
/// </para>
/// <para>
/// The attribute is written with <c>IElement.SetAttribute</c> rather than through
/// <see cref="DomHostHooks"/>'s: that hook exists to reconcile a <em>handler content attribute</em> with the
/// element's listener list, and no attribute a <c>DOMTokenList</c> reflects begins with <c>on</c>.
/// </para>
/// </remarks>
internal static class DomTokenListMembers
{
    /// <summary>
    /// The element and attribute each projected token list reflects, keyed on the AngleSharp list.
    /// </summary>
    /// <remarks>
    /// A <see cref="ConditionalWeakTable{TKey,TValue}"/> rather than a field, for the reason
    /// <c>DomViewMembers</c>' filter table gives: the list is AngleSharp's object and this assembly cannot
    /// add a field to it. The value holds the element, so the entry lives exactly as long as the list does
    /// and the element it names cannot outlive its own tree.
    /// </remarks>
    private static readonly ConditionalWeakTable<ITokenList, Owner> _owners = new();

    /// <summary>Projects <paramref name="list"/>, recording the attribute of <paramref name="element"/> it reflects.</summary>
    internal static JsValue Project(DomRealm realm, IElement element, string attribute, ITokenList list)
    {
        _owners.AddOrUpdate(list, new Owner(element, attribute));
        return realm.Wrap(list);
    }

    /// <summary>
    /// WebIDL's <a href="https://webidl.spec.whatwg.org/#PutForwards">[PutForwards=value]</a>, which every
    /// accessor <see cref="Project"/> serves carries: <c>el.classList = "a b"</c> is
    /// <c>el.classList.value = "a b"</c>, and therefore a verbatim write of the attribute.
    /// </summary>
    /// <remarks>
    /// Without it the member is a read-only accessor and the assignment is a <c>TypeError</c> in strict
    /// mode, which is what <c>dom/nodes/Element-classlist.html</c>'s "Assigning to classList" rows say.
    /// </remarks>
    internal static JsValue PutForwards(IElement element, string attribute, JsValue[] arguments)
    {
        element.SetAttribute(attribute, DomConvert.RequiredText(arguments, 0, Member.Value));
        return JsValue.Undefined;
    }

    /// <summary>https://dom.spec.whatwg.org/#dom-domtokenlist-item — <c>null</c> out of range, never a throw.</summary>
    internal static JsValue Item(ITokenList list, JsValue[] arguments)
    {
        // WebIDL's unsigned long: -1 is 4294967295, which is out of range rather than an error, and that is
        // the whole of what an indexed getter promises.
        var index = DomConvert.RequiredUInt32(arguments, 0, Member.Item);
        return index >= (uint) list.Length ? JsValue.Null : JsString.Create(list[(int) index]);
    }

    /// <summary>https://dom.spec.whatwg.org/#dom-domtokenlist-add.</summary>
    internal static JsValue Add(DomRealm realm, ITokenList list, JsValue[] arguments)
    {
        var tokens = DomConvert.TextRest(arguments, 0);
        Validate(realm, tokens, Member.Add);

        foreach (var token in tokens)
        {
            if (!list.Contains(token))
            {
                list.Add(token);
            }
        }

        Update(realm, list);
        return JsValue.Undefined;
    }

    /// <summary>https://dom.spec.whatwg.org/#dom-domtokenlist-remove.</summary>
    internal static JsValue Remove(DomRealm realm, ITokenList list, JsValue[] arguments)
    {
        var tokens = DomConvert.TextRest(arguments, 0);
        Validate(realm, tokens, Member.Remove);

        list.Remove(tokens);
        Update(realm, list);
        return JsValue.Undefined;
    }

    /// <summary>https://dom.spec.whatwg.org/#dom-domtokenlist-toggle.</summary>
    internal static JsValue Toggle(DomRealm realm, ITokenList list, JsValue[] arguments)
    {
        var token = DomConvert.RequiredText(arguments, 0, Member.Toggle);
        Validate(realm, token, Member.Toggle);

        // "force is either not given or …" — the distinction AngleSharp's `bool force = false` erases, and
        // the whole of why toggle is here: `toggle("a", false)` on an element without the token must answer
        // false and add nothing, where a defaulted parameter reads it as an ordinary toggle.
        var given = arguments.Length > 1 && !arguments[1].IsUndefined();
        var force = given && TypeConverter.ToBoolean(arguments[1]);

        if (list.Contains(token))
        {
            if (given && force)
            {
                return JsBoolean.True;
            }

            list.Remove(token);
            Update(realm, list);
            return JsBoolean.False;
        }

        if (given && !force)
        {
            return JsBoolean.False;
        }

        list.Add(token);
        Update(realm, list);
        return JsBoolean.True;
    }

    /// <summary>https://dom.spec.whatwg.org/#dom-domtokenlist-replace.</summary>
    internal static JsValue Replace(DomRealm realm, ITokenList list, JsValue[] arguments)
    {
        var token = DomConvert.RequiredText(arguments, 0, Member.Replace);
        var newToken = DomConvert.RequiredText(arguments, 1, Member.Replace);

        // Its validation is the one that is not per-token: DOM §7.1 asks whether *either* argument is empty
        // before it asks whether either contains whitespace, so `replace(" ", "")` is a SyntaxError for the
        // empty replacement rather than an InvalidCharacterError for the space.
        RefuseEmpty(realm, token, Member.Replace);
        RefuseEmpty(realm, newToken, Member.Replace);
        RefuseWhitespace(realm, token, Member.Replace);
        RefuseWhitespace(realm, newToken, Member.Replace);

        if (!list.Contains(token))
        {
            return JsBoolean.False;
        }

        // "Replace token in this's token set with newToken" is an in-place replacement, so the order of the
        // rest is kept and a newToken that is already present collapses onto the replaced position. That is
        // not something Add and Remove can express — they append and delete — so the new set is computed
        // here and written once.
        var replaced = new List<string>(list.Length);

        foreach (var existing in list)
        {
            var next = string.Equals(existing, token, StringComparison.Ordinal) ? newToken : existing;

            if (!replaced.Contains(next, StringComparer.Ordinal))
            {
                replaced.Add(next);
            }
        }

        Write(realm, list, string.Join(" ", replaced));
        return JsBoolean.True;
    }

    /// <summary>https://dom.spec.whatwg.org/#dom-domtokenlist-supports.</summary>
    /// <remarks>
    /// Its first step asks the associated attribute for its supported tokens, and a `TypeError` is what a
    /// browser answers when the attribute defines none — `class` among them, which is what
    /// `dom/nodes/Element-classlist.html` asserts. HTML *does* define a supported-token set for `rel` and
    /// `sandbox`, and this answers a `TypeError` for those too; it is recorded in `Dom/AGENTS.md`.
    /// </remarks>
    internal static JsValue Supports(DomRealm realm, JsValue[] arguments)
    {
        DomConvert.RequiredText(arguments, 0, Member.Supports);

        Throw.TypeError(
            realm.PrincipalRealm,
            "Failed to execute '" + Member.Supports + "': the attribute this token list reflects defines no supported tokens.");
        return JsValue.Undefined;
    }

    /// <summary>
    /// https://dom.spec.whatwg.org/#dom-domtokenlist-value, and the interface's stringifier, which DOM
    /// defines as the same steps.
    /// </summary>
    internal static JsValue Value(ITokenList list)
        => JsString.Create(_owners.TryGetValue(list, out var owner)
            ? owner.Element.GetAttribute(owner.Attribute) ?? ""
            : string.Join(" ", list));

    /// <summary>The <c>value</c> setter: set an attribute value, verbatim.</summary>
    internal static JsValue SetValue(DomRealm realm, ITokenList list, JsValue[] arguments)
    {
        Write(realm, list, DomConvert.RequiredText(arguments, 0, Member.Value));
        return JsValue.Undefined;
    }

    /// <summary>https://dom.spec.whatwg.org/#concept-dtl-update — always a write, even when nothing changed.</summary>
    /// <remarks>
    /// It is the step that makes <c>classList.add("a")</c> on <c>class="a a"</c> leave <c>class="a"</c>
    /// behind: the token set is unchanged, and the attribute is still rewritten in serialized form.
    /// AngleSharp raises its own change notification only when the set moved, so without this the attribute
    /// keeps whatever the page wrote.
    /// </remarks>
    private static void Update(DomRealm realm, ITokenList list)
    {
        if (!_owners.TryGetValue(list, out var owner))
        {
            return;
        }

        // Step 1: an absent attribute and an empty token set is the one case that writes nothing, so that
        // reading `classList` never gives an element a `class` attribute it did not have.
        if (list.Length == 0 && owner.Element.GetAttribute(owner.Attribute) is null)
        {
            return;
        }

        Write(realm, list, string.Join(" ", list));
    }

    private static void Write(DomRealm realm, ITokenList list, string value)
    {
        _ = realm;

        if (_owners.TryGetValue(list, out var owner))
        {
            owner.Element.SetAttribute(owner.Attribute, value);
            return;
        }

        // No element to write to, so the token set is the only place the value can live. Reached only by a
        // token list this package did not project through an accessor, which today is none.
        list.Remove([.. list]);
        list.Add(value.Split((char[]?) null, StringSplitOptions.RemoveEmptyEntries));
    }

    /// <summary>https://dom.spec.whatwg.org/#concept-domtokenlist-validation, for each of several tokens.</summary>
    private static void Validate(DomRealm realm, string[] tokens, string member)
    {
        // Every token is validated before any is appended, which is what makes `add("a", "")` leave the list
        // untouched rather than half-updated.
        foreach (var token in tokens)
        {
            Validate(realm, token, member);
        }
    }

    private static void Validate(DomRealm realm, string token, string member)
    {
        RefuseEmpty(realm, token, member);
        RefuseWhitespace(realm, token, member);
    }

    private static void RefuseEmpty(DomRealm realm, string token, string member)
    {
        if (token.Length == 0)
        {
            DomFailures.Refuse(realm.Engine, member, DomExceptionNames.Syntax, "The token provided must not be empty.");
        }
    }

    private static void RefuseWhitespace(DomRealm realm, string token, string member)
    {
        foreach (var character in token)
        {
            // https://infra.spec.whatwg.org/#ascii-whitespace — tab, newline, form feed, carriage return and
            // space, and deliberately not every Unicode space separator.
            if (character is '\t' or '\n' or '\f' or '\r' or ' ')
            {
                DomFailures.Refuse(
                    realm.Engine,
                    member,
                    DomExceptionNames.InvalidCharacter,
                    "The token provided ('" + token + "') contains HTML space characters, which are not valid in tokens.");
            }
        }
    }

    /// <summary>The element and attribute a projected token list reflects.</summary>
    private sealed record class Owner(IElement Element, string Attribute);

    /// <summary>The qualified member names the refusals wear, spelled once.</summary>
    private static class Member
    {
        internal const string Add = "DOMTokenList.add";

        internal const string Item = "DOMTokenList.item";

        internal const string Remove = "DOMTokenList.remove";

        internal const string Replace = "DOMTokenList.replace";

        internal const string Supports = "DOMTokenList.supports";

        internal const string Toggle = "DOMTokenList.toggle";

        internal const string Value = "DOMTokenList.value";
    }
}
