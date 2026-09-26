using System.Text;
using Jint.HtmlParser;
using Jint.Native;
using Jint.Runtime;
using Jint.WebApi.DomException;

namespace Jint.Browser.Dom.Collections;

/// <summary>DOM §7.1 operations over the actual element-backed ordered token set.</summary>
internal static class DomTokenListMembers
{
    internal static JsValue Project(DomRealm realm, Element element, string attribute)
        => realm.Wrap(DomAttributeTokenList.Of(element, attribute));

    // https://webidl.spec.whatwg.org/#PutForwards
    internal static JsValue PutForwards(DomRealm realm, Element element, string attribute, JsValue[] arguments)
    {
        var value = DomConvert.RequiredText(arguments, 0, Member.Value);
        var work = Work(realm);
        work.Check();
        element.SetAttribute(attribute, value);
        work.Check();
        return JsValue.Undefined;
    }

    // https://dom.spec.whatwg.org/#dom-domtokenlist-item
    internal static JsValue Item(DomRealm realm, DomAttributeTokenList list, JsValue[] arguments)
    {
        var index = DomConvert.RequiredUInt32(arguments, 0, Member.Item);
        return list.ReadItem(index, realm.NativeReadCheckpoint, realm.CancellationToken) is { } item
            ? JsString.Create(item) : JsValue.Null;
    }

    internal static JsValue Contains(DomRealm realm, DomAttributeTokenList list, JsValue[] arguments)
        => JsBoolean.Create(list.ReadContains(DomConvert.RequiredText(arguments, 0, Member.Contains),
            realm.NativeReadCheckpoint, realm.CancellationToken));

    // https://dom.spec.whatwg.org/#dom-domtokenlist-add
    internal static JsValue Add(DomRealm realm, DomAttributeTokenList list, JsValue[] arguments)
    {
        var tokens = DomConvert.TextRest(arguments, 0);
        var work = Work(realm);
        Validate(realm, tokens, Member.Add, work);
        var next = Snapshot(list, work);
        foreach (var token in tokens)
        {
            if (!Contains(next, token, work)) next.Add(token);
        }
        Update(list, next, work);
        return JsValue.Undefined;
    }

    // https://dom.spec.whatwg.org/#dom-domtokenlist-remove
    internal static JsValue Remove(DomRealm realm, DomAttributeTokenList list, JsValue[] arguments)
    {
        var tokens = DomConvert.TextRest(arguments, 0);
        var work = Work(realm);
        Validate(realm, tokens, Member.Remove, work);
        var next = Snapshot(list, work);
        for (var i = next.Count - 1; i >= 0; i--)
        {
            if (Contains(tokens, next[i], work)) next.RemoveAt(i);
        }
        Update(list, next, work);
        return JsValue.Undefined;
    }

    // https://dom.spec.whatwg.org/#dom-domtokenlist-toggle
    internal static JsValue Toggle(DomRealm realm, DomAttributeTokenList list, JsValue[] arguments)
    {
        var token = DomConvert.RequiredText(arguments, 0, Member.Toggle);
        var work = Work(realm);
        Validate(realm, token, Member.Toggle, work);
        var given = arguments.Length > 1 && !arguments[1].IsUndefined();
        var force = given && TypeConverter.ToBoolean(arguments[1]);
        var next = Snapshot(list, work);
        for (var i = 0; i < next.Count; i++)
        {
            if (!work.Equal(next[i], token)) continue;
            if (given && force) { work.Check(); return JsBoolean.True; }
            next.RemoveAt(i);
            Update(list, next, work);
            return JsBoolean.False;
        }
        if (given && !force) { work.Check(); return JsBoolean.False; }
        next.Add(token);
        Update(list, next, work);
        return JsBoolean.True;
    }

    // https://dom.spec.whatwg.org/#dom-domtokenlist-replace
    internal static JsValue Replace(DomRealm realm, DomAttributeTokenList list, JsValue[] arguments)
    {
        var token = DomConvert.RequiredText(arguments, 0, Member.Replace);
        var replacement = DomConvert.RequiredText(arguments, 1, Member.Replace);
        var work = Work(realm);
        work.Check();
        // Both empty checks precede either whitespace check.
        RefuseEmpty(realm, token, Member.Replace);
        RefuseEmpty(realm, replacement, Member.Replace);
        RefuseWhitespace(realm, token, Member.Replace, work);
        RefuseWhitespace(realm, replacement, Member.Replace, work);
        var tokens = Snapshot(list, work);
        if (!Contains(tokens, token, work)) { work.Check(); return JsBoolean.False; }
        var replaced = new List<string>(tokens.Count);
        foreach (var existing in tokens)
        {
            var next = work.Equal(existing, token) ? replacement : existing;
            if (!Contains(replaced, next, work)) replaced.Add(next);
        }
        Write(list, Serialize(replaced, work), work);
        return JsBoolean.True;
    }

    // Attributes with supported-token sets retain the existing unsupported capability.
    internal static JsValue Supports(DomRealm realm, JsValue[] arguments)
    {
        DomConvert.RequiredText(arguments, 0, Member.Supports);
        Throw.TypeError(realm.OwningRealm,
            "Failed to execute '" + Member.Supports + "': the attribute this token list reflects defines no supported tokens.");
        return JsValue.Undefined;
    }

    // https://dom.spec.whatwg.org/#dom-domtokenlist-value
    internal static JsValue Value(DomRealm realm, DomAttributeTokenList list)
        => JsString.Create(list.ReadValue(realm.NativeReadCheckpoint, realm.CancellationToken));

    internal static JsValue SetValue(DomRealm realm, DomAttributeTokenList list, JsValue[] arguments)
    {
        var value = DomConvert.RequiredText(arguments, 0, Member.Value);
        Write(list, value, Work(realm));
        return JsValue.Undefined;
    }

    private static DomReadWork Work(DomRealm realm) => new(realm.NativeReadCheckpoint, realm.CancellationToken);

    private static List<string> Snapshot(DomAttributeTokenList list, DomReadWork work)
    {
        var tokens = new List<string>();
        foreach (var token in list.Read(work)) tokens.Add(token);
        work.Check();
        return tokens;
    }

    private static bool Contains(IReadOnlyList<string> tokens, string token, DomReadWork work)
    {
        foreach (var existing in tokens)
        {
            if (work.Equal(existing, token)) return true;
        }
        return false;
    }

    // https://dom.spec.whatwg.org/#concept-dtl-update
    private static void Update(DomAttributeTokenList list, List<string> tokens, DomReadWork work)
    {
        if (tokens.Count == 0 && work.Attribute(list.Element, list.Attribute) is null)
        {
            work.Check();
            return;
        }
        Write(list, Serialize(tokens, work), work);
    }

    private static string Serialize(List<string> tokens, DomReadWork work)
    {
        var builder = new StringBuilder();
        foreach (var token in tokens)
        {
            work.Step();
            if (builder.Length != 0) builder.Append(' ');
            foreach (var character in token)
            {
                work.Step();
                builder.Append(character);
            }
        }
        work.Check();
        return builder.ToString();
    }

    private static void Write(DomAttributeTokenList list, string value, DomReadWork work)
    {
        work.Check();
        list.Element.SetAttribute(list.Attribute, value);
        work.Check();
    }

    private static void Validate(DomRealm realm, string[] tokens, string member, DomReadWork work)
    {
        work.Check();
        foreach (var token in tokens)
        {
            work.Step();
            Validate(realm, token, member, work);
        }
        work.Check();
    }

    private static void Validate(DomRealm realm, string token, string member, DomReadWork work)
    {
        work.Check();
        RefuseEmpty(realm, token, member);
        RefuseWhitespace(realm, token, member, work);
        work.Check();
    }

    private static void RefuseEmpty(DomRealm realm, string token, string member)
    {
        if (token.Length == 0)
            DomFailures.Refuse(realm, member, DomExceptionNames.Syntax, "The token provided must not be empty.");
    }

    private static void RefuseWhitespace(DomRealm realm, string token, string member, DomReadWork work)
    {
        foreach (var character in token)
        {
            work.Step();
            if (character is '\t' or '\n' or '\f' or '\r' or ' ')
                DomFailures.Refuse(realm, member, DomExceptionNames.InvalidCharacter,
                    "The token provided ('" + token + "') contains HTML space characters, which are not valid in tokens.");
        }
    }

    private static class Member
    {
        internal const string Add = "DOMTokenList.add";
        internal const string Contains = "DOMTokenList.contains";
        internal const string Item = "DOMTokenList.item";
        internal const string Remove = "DOMTokenList.remove";
        internal const string Replace = "DOMTokenList.replace";
        internal const string Supports = "DOMTokenList.supports";
        internal const string Toggle = "DOMTokenList.toggle";
        internal const string Value = "DOMTokenList.value";
    }
}
