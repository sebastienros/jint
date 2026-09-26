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
        element.SetAttributeNS(null, attribute, value);
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
        while (true)
        {
            var next = list.ReadSnapshot(work, out var proof);
            var present = Index(next, work);
            foreach (var token in tokens)
            {
                if (present.Add(token)) next.Add(token);
            }
            if (TryUpdate(list, next, work, proof)) return JsValue.Undefined;
        }
    }

    // https://dom.spec.whatwg.org/#dom-domtokenlist-remove
    internal static JsValue Remove(DomRealm realm, DomAttributeTokenList list, JsValue[] arguments)
    {
        var tokens = DomConvert.TextRest(arguments, 0);
        var work = Work(realm);
        Validate(realm, tokens, Member.Remove, work);
        var removed = Index(tokens, work);
        while (true)
        {
            var next = list.ReadSnapshot(work, out var proof);
            var retained = new List<string>(next.Count);
            foreach (var existing in next)
            {
                work.Step();
                if (!removed.Contains(existing)) retained.Add(existing);
            }
            if (TryUpdate(list, retained, work, proof)) return JsValue.Undefined;
        }
    }

    // https://dom.spec.whatwg.org/#dom-domtokenlist-toggle
    internal static JsValue Toggle(DomRealm realm, DomAttributeTokenList list, JsValue[] arguments)
    {
        var token = DomConvert.RequiredText(arguments, 0, Member.Toggle);
        var work = Work(realm);
        Validate(realm, token, Member.Toggle, work);
        var given = arguments.Length > 1 && !arguments[1].IsUndefined();
        var force = given && TypeConverter.ToBoolean(arguments[1]);
        while (true)
        {
            var next = list.ReadSnapshot(work, out var proof);
            var index = -1;
            for (var i = 0; i < next.Count; i++)
            {
                if (work.Equal(next[i], token)) { index = i; break; }
            }
            if (index >= 0 && given && force || index < 0 && given && !force)
            {
                work.Check();
                if (!proof.IsCurrent) continue;
                return index >= 0 ? JsBoolean.True : JsBoolean.False;
            }
            if (index >= 0) next.RemoveAt(index);
            else next.Add(token);
            if (TryUpdate(list, next, work, proof)) return index >= 0 ? JsBoolean.False : JsBoolean.True;
        }
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
        while (true)
        {
            var tokens = list.ReadSnapshot(work, out var proof);
            var oldIndex = -1;
            var newIndex = -1;
            for (var i = 0; i < tokens.Count; i++)
            {
                if (work.Equal(tokens[i], token)) oldIndex = i;
                if (work.Equal(tokens[i], replacement)) newIndex = i;
            }
            if (oldIndex < 0)
            {
                work.Check();
                if (!proof.IsCurrent) continue;
                return JsBoolean.False;
            }
            // The earlier of the old/replacement entries keeps the ordered-set position.
            if (newIndex >= 0 && newIndex != oldIndex)
            {
                tokens[Math.Min(oldIndex, newIndex)] = replacement;
                tokens.RemoveAt(Math.Max(oldIndex, newIndex));
            }
            else tokens[oldIndex] = replacement;
            if (TryWrite(list, Serialize(tokens, work), work, proof)) return JsBoolean.True;
        }
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

    private static HashSet<string> Index(IReadOnlyList<string> tokens, DomReadWork work)
    {
        var index = new HashSet<string>(new TokenComparer(work));
        foreach (var token in tokens)
        {
            work.Step();
            index.Add(token);
        }
        work.Check();
        return index;
    }

    // Invocation-local indexing charges the actual hash and collision character reads.
    private sealed class TokenComparer(DomReadWork work) : IEqualityComparer<string>
    {
        public bool Equals(string? x, string? y)
        {
            work.Step();
            return ReferenceEquals(x, y) || y is not null && work.Equal(x, y);
        }
        public int GetHashCode(string value)
        {
            var hash = 2166136261u;
            foreach (var character in value)
            {
                work.Step();
                hash = unchecked((hash ^ character) * 16777619u);
            }
            return unchecked((int) hash);
        }
    }

    // https://dom.spec.whatwg.org/#concept-dtl-update
    private static bool TryUpdate(DomAttributeTokenList list, List<string> tokens, DomReadWork work,
        DomAttributeTokenList.SourceProof proof)
    {
        if (tokens.Count == 0 && proof.Value is null)
        {
            work.Check();
            return proof.IsCurrent;
        }
        return TryWrite(list, Serialize(tokens, work), work, proof);
    }

    private static bool TryWrite(DomAttributeTokenList list, string value, DomReadWork work,
        DomAttributeTokenList.SourceProof proof)
    {
        work.Check();
        if (!proof.IsCurrent) return false;
        // No callback between the constant source proof and the one null-namespace publication.
        list.Element.SetAttributeNS(null, list.Attribute, value);
        return true;
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
        list.Element.SetAttributeNS(null, list.Attribute, value);
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
