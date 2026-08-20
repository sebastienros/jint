using System.Collections.Generic;

namespace Jint.Runtime.Modules;

/// <summary>
/// The kinds a <c>package.json</c> value can have, reduced to what
/// <see href="https://nodejs.org/api/esm.html#resolution-algorithm-specification">PACKAGE_TARGET_RESOLVE</see>
/// distinguishes: a string target, a conditions object, a fallback array, an explicit <c>null</c> and
/// "anything else", which that algorithm's last step rejects as an <em>Invalid Package Target</em>.
/// </summary>
internal enum PackageJsonValueKind
{
    /// <summary>A JSON value that is none of the others - a number or a boolean.</summary>
    Other,
    String,
    Object,
    Array,
    Null,
}

/// <summary>
/// A parsed <c>package.json</c> value. Object members keep their <em>source order</em>, which is not a
/// convenience: <see href="https://nodejs.org/api/packages.html#conditional-exports">conditional exports</see>
/// are resolved "in object insertion order" and "earlier entries have higher priority and take precedence over
/// later entries", so a reader that sorted or hashed the keys away would silently pick the wrong condition.
/// </summary>
internal sealed class PackageJsonValue
{
    internal static readonly PackageJsonValue Null = new(PackageJsonValueKind.Null, stringValue: null, members: null, items: null);
    internal static readonly PackageJsonValue Other = new(PackageJsonValueKind.Other, stringValue: null, members: null, items: null);

    private PackageJsonValue(
        PackageJsonValueKind kind,
        string? stringValue,
        List<KeyValuePair<string, PackageJsonValue>>? members,
        List<PackageJsonValue>? items)
    {
        Kind = kind;
        StringValue = stringValue;
        Members = members ?? (IReadOnlyList<KeyValuePair<string, PackageJsonValue>>) Array.Empty<KeyValuePair<string, PackageJsonValue>>();
        Items = items ?? (IReadOnlyList<PackageJsonValue>) Array.Empty<PackageJsonValue>();
    }

    internal static PackageJsonValue CreateString(string value) => new(PackageJsonValueKind.String, value, members: null, items: null);

    internal static PackageJsonValue CreateObject(List<KeyValuePair<string, PackageJsonValue>> members) => new(PackageJsonValueKind.Object, stringValue: null, members, items: null);

    internal static PackageJsonValue CreateArray(List<PackageJsonValue> items) => new(PackageJsonValueKind.Array, stringValue: null, members: null, items);

    public PackageJsonValueKind Kind { get; }

    /// <summary>The value when <see cref="Kind"/> is <see cref="PackageJsonValueKind.String"/>, else null.</summary>
    public string? StringValue { get; }

    /// <summary>The members in source order; empty unless <see cref="Kind"/> is <see cref="PackageJsonValueKind.Object"/>.</summary>
    public IReadOnlyList<KeyValuePair<string, PackageJsonValue>> Members { get; }

    /// <summary>The items in source order; empty unless <see cref="Kind"/> is <see cref="PackageJsonValueKind.Array"/>.</summary>
    public IReadOnlyList<PackageJsonValue> Items { get; }

    public bool TryGetMember(string key, out PackageJsonValue value)
    {
        var members = Members;
        for (var i = 0; i < members.Count; i++)
        {
            if (string.Equals(members[i].Key, key, StringComparison.Ordinal))
            {
                value = members[i].Value;
                return true;
            }
        }

        value = Null;
        return false;
    }
}

/// <summary>
/// The <c>package.json</c> fields Node's ES module resolution reads, as
/// <see href="https://nodejs.org/api/esm.html#resolution-algorithm-specification">READ_PACKAGE_JSON</see>
/// produces them: absent file returns null, an unparseable file is an <em>Invalid Package Configuration</em>.
/// </summary>
/// <remarks>
/// Jint carries no JSON reader that works without an <see cref="Engine"/> - <c>JsonParser</c> needs a realm to
/// build the resulting objects in - and module resolution is engine-free by contract, so this file carries a
/// small ordered reader of its own. It understands whole JSON but keeps only the three fields the algorithm
/// consults, and it keeps object members in source order because conditional exports are order-sensitive.
/// </remarks>
internal sealed class PackageJson
{
    internal const string FileName = "package.json";

    /// <summary>
    /// Nesting cap for the reader. A hostile <c>package.json</c> is a file the host put on disk itself, so this
    /// is a guard against accident rather than attack, but the reader recurses and a stack overflow is fatal to
    /// the process where a refusal is not.
    /// </summary>
    private const int MaxDepth = 64;

    private PackageJson(string? name, string? main, PackageJsonValue? exports)
    {
        Name = name;
        Main = main;
        Exports = exports;
    }

    /// <summary>The <c>"name"</c> field when it is a string, which is what a self-reference matches against.</summary>
    public string? Name { get; }

    /// <summary>The <c>"main"</c> field when it is a string. Only consulted when <see cref="Exports"/> is null.</summary>
    public string? Main { get; }

    /// <summary>
    /// The <c>"exports"</c> field, or null when it is absent <em>or explicitly JSON <c>null</c></em> - the
    /// algorithm's "not null or undefined" test, which is what makes <c>"exports": null</c> fall through to
    /// <c>"main"</c> rather than encapsulate everything.
    /// </summary>
    public PackageJsonValue? Exports { get; }

    /// <summary>
    /// READ_PACKAGE_JSON. Returns false only for a file that exists and does not parse; a missing file is a
    /// success with a null <paramref name="package"/>.
    /// </summary>
    public static bool TryRead(string packageDirectory, out PackageJson? package)
    {
        package = null;

        var path = Path.Combine(packageDirectory, FileName);
        if (!File.Exists(path))
        {
            return true;
        }

        var text = File.ReadAllText(path);
        var index = 0;
        if (!TryReadValue(text, ref index, depth: 0, out var root))
        {
            return false;
        }

        SkipWhitespace(text, ref index);
        if (index != text.Length || root.Kind != PackageJsonValueKind.Object)
        {
            return false;
        }

        string? name = null;
        string? main = null;
        PackageJsonValue? exports = null;

        var members = root.Members;
        for (var i = 0; i < members.Count; i++)
        {
            var member = members[i];
            if (string.Equals(member.Key, "name", StringComparison.Ordinal))
            {
                name = member.Value.StringValue;
            }
            else if (string.Equals(member.Key, "main", StringComparison.Ordinal))
            {
                main = member.Value.StringValue;
            }
            else if (string.Equals(member.Key, "exports", StringComparison.Ordinal))
            {
                exports = member.Value.Kind == PackageJsonValueKind.Null ? null : member.Value;
            }
        }

        package = new PackageJson(name, main, exports);
        return true;
    }

    private static bool TryReadValue(string text, ref int index, int depth, out PackageJsonValue value)
    {
        value = PackageJsonValue.Other;
        if (depth > MaxDepth)
        {
            return false;
        }

        SkipWhitespace(text, ref index);
        if (index >= text.Length)
        {
            return false;
        }

        switch (text[index])
        {
            case '{':
                return TryReadObject(text, ref index, depth, out value);
            case '[':
                return TryReadArray(text, ref index, depth, out value);
            case '"':
                if (!TryReadString(text, ref index, out var read))
                {
                    return false;
                }

                value = PackageJsonValue.CreateString(read);
                return true;
            case 't':
                return TryReadLiteral(text, ref index, "true", PackageJsonValue.Other, out value);
            case 'f':
                return TryReadLiteral(text, ref index, "false", PackageJsonValue.Other, out value);
            case 'n':
                return TryReadLiteral(text, ref index, "null", PackageJsonValue.Null, out value);
            default:
                return TryReadNumber(text, ref index, out value);
        }
    }

    private static bool TryReadObject(string text, ref int index, int depth, out PackageJsonValue value)
    {
        value = PackageJsonValue.Other;
        index++; // '{'

        var members = new List<KeyValuePair<string, PackageJsonValue>>();
        SkipWhitespace(text, ref index);
        if (index < text.Length && text[index] == '}')
        {
            index++;
            value = PackageJsonValue.CreateObject(members);
            return true;
        }

        while (true)
        {
            SkipWhitespace(text, ref index);
            if (index >= text.Length || text[index] != '"' || !TryReadString(text, ref index, out var key))
            {
                return false;
            }

            SkipWhitespace(text, ref index);
            if (index >= text.Length || text[index] != ':')
            {
                return false;
            }

            index++;
            if (!TryReadValue(text, ref index, depth + 1, out var member))
            {
                return false;
            }

            // JSON.parse keeps a duplicated key at the position of its first occurrence and gives it the last
            // value, and object order is what decides which condition wins - so the reader has to agree.
            var replaced = false;
            for (var i = 0; i < members.Count; i++)
            {
                if (string.Equals(members[i].Key, key, StringComparison.Ordinal))
                {
                    members[i] = new KeyValuePair<string, PackageJsonValue>(key, member);
                    replaced = true;
                    break;
                }
            }

            if (!replaced)
            {
                members.Add(new KeyValuePair<string, PackageJsonValue>(key, member));
            }

            SkipWhitespace(text, ref index);
            if (index >= text.Length)
            {
                return false;
            }

            if (text[index] == ',')
            {
                index++;
                continue;
            }

            if (text[index] == '}')
            {
                index++;
                value = PackageJsonValue.CreateObject(members);
                return true;
            }

            return false;
        }
    }

    private static bool TryReadArray(string text, ref int index, int depth, out PackageJsonValue value)
    {
        value = PackageJsonValue.Other;
        index++; // '['

        var items = new List<PackageJsonValue>();
        SkipWhitespace(text, ref index);
        if (index < text.Length && text[index] == ']')
        {
            index++;
            value = PackageJsonValue.CreateArray(items);
            return true;
        }

        while (true)
        {
            if (!TryReadValue(text, ref index, depth + 1, out var item))
            {
                return false;
            }

            items.Add(item);

            SkipWhitespace(text, ref index);
            if (index >= text.Length)
            {
                return false;
            }

            if (text[index] == ',')
            {
                index++;
                continue;
            }

            if (text[index] == ']')
            {
                index++;
                value = PackageJsonValue.CreateArray(items);
                return true;
            }

            return false;
        }
    }

    private static bool TryReadString(string text, ref int index, out string value)
    {
        value = "";
        index++; // '"'

        var builder = new System.Text.StringBuilder();
        while (index < text.Length)
        {
            var c = text[index++];
            if (c == '"')
            {
                value = builder.ToString();
                return true;
            }

            if (c != '\\')
            {
                if (c < ' ')
                {
                    return false;
                }

                builder.Append(c);
                continue;
            }

            if (index >= text.Length)
            {
                return false;
            }

            var escape = text[index++];
            switch (escape)
            {
                case '"': builder.Append('"'); break;
                case '\\': builder.Append('\\'); break;
                case '/': builder.Append('/'); break;
                case 'b': builder.Append('\b'); break;
                case 'f': builder.Append('\f'); break;
                case 'n': builder.Append('\n'); break;
                case 'r': builder.Append('\r'); break;
                case 't': builder.Append('\t'); break;
                case 'u':
                    if (index + 4 > text.Length || !TryReadHex4(text, index, out var code))
                    {
                        return false;
                    }

                    builder.Append((char) code);
                    index += 4;
                    break;
                default:
                    return false;
            }
        }

        return false;
    }

    private static bool TryReadHex4(string text, int index, out int value)
    {
        value = 0;
        for (var i = 0; i < 4; i++)
        {
            var c = text[index + i];
            int digit;
            if (char.IsAsciiDigit(c))
            {
                digit = c - '0';
            }
            else if (c >= 'a' && c <= 'f')
            {
                digit = c - 'a' + 10;
            }
            else if (c >= 'A' && c <= 'F')
            {
                digit = c - 'A' + 10;
            }
            else
            {
                return false;
            }

            value = (value << 4) + digit;
        }

        return true;
    }

    private static bool TryReadNumber(string text, ref int index, out PackageJsonValue value)
    {
        value = PackageJsonValue.Other;

        if (index < text.Length && text[index] == '-')
        {
            index++;
        }

        if (!TryReadDigits(text, ref index))
        {
            return false;
        }

        if (index < text.Length && text[index] == '.')
        {
            index++;
            if (!TryReadDigits(text, ref index))
            {
                return false;
            }
        }

        if (index < text.Length && (text[index] == 'e' || text[index] == 'E'))
        {
            index++;
            if (index < text.Length && (text[index] == '+' || text[index] == '-'))
            {
                index++;
            }

            if (!TryReadDigits(text, ref index))
            {
                return false;
            }
        }

        return true;
    }

    private static bool TryReadDigits(string text, ref int index)
    {
        var start = index;
        while (index < text.Length && char.IsAsciiDigit(text[index]))
        {
            index++;
        }

        return index > start;
    }

    private static bool TryReadLiteral(string text, ref int index, string literal, PackageJsonValue literalValue, out PackageJsonValue value)
    {
        value = PackageJsonValue.Other;
        if (index + literal.Length > text.Length || string.CompareOrdinal(text, index, literal, 0, literal.Length) != 0)
        {
            return false;
        }

        index += literal.Length;
        value = literalValue;
        return true;
    }

    private static void SkipWhitespace(string text, ref int index)
    {
        while (index < text.Length)
        {
            var c = text[index];
            if (c is ' ' or '\t' or '\n' or '\r')
            {
                index++;
                continue;
            }

            break;
        }
    }
}
