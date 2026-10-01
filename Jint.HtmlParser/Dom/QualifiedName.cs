namespace Jint.HtmlParser;

internal readonly record struct QualifiedName(string? NamespaceUri, string LocalName, string? Prefix)
{
    internal static void ValidateAttributeLocalName(string name)
    {
        if (name.Length == 0)
        {
            throw DomException.InvalidCharacter();
        }

        foreach (var character in name)
        {
            if (IsForbiddenNameCharacter(character) || character == '=')
            {
                throw DomException.InvalidCharacter();
            }
        }
    }

    internal static void ValidateElementLocalName(string name)
    {
        if (name.Length == 0)
        {
            throw DomException.InvalidCharacter();
        }

        var first = name[0];
        if (IsAsciiAlpha(first))
        {
            foreach (var character in name)
            {
                if (IsForbiddenNameCharacter(character))
                {
                    throw DomException.InvalidCharacter();
                }
            }

            return;
        }

        if (first is not ':' and not '_' && first < 0x80)
        {
            throw DomException.InvalidCharacter();
        }

        for (var i = 1; i < name.Length; i++)
        {
            var character = name[i];
            if (!IsAsciiAlpha(character) && character is not (>= '0' and <= '9') and not '-' and not '.' and not ':' and not '_' && character < 0x80)
            {
                throw DomException.InvalidCharacter();
            }
        }
    }

    // DOM Standard §1.4: validate and extract a namespace and qualified name.
    internal static QualifiedName Parse(string? namespaceUri, string qualifiedName, bool attribute)
    {
        ArgumentNullException.ThrowIfNull(qualifiedName);
        namespaceUri = namespaceUri is "" ? null : namespaceUri;
        var colon = qualifiedName.IndexOf(':');
        string? prefix = null;
        var localName = qualifiedName;
        if (colon >= 0)
        {
            prefix = qualifiedName[..colon];
            localName = qualifiedName[(colon + 1)..];
            ValidatePrefix(prefix);
        }

        if (attribute) ValidateAttributeLocalName(localName);
        else ValidateElementLocalName(localName);

        if (prefix is not null && namespaceUri is null) throw DomException.Namespace();

        if (prefix is "xml" && namespaceUri != Namespaces.Xml ||
            (qualifiedName == "xmlns" || prefix == "xmlns") && namespaceUri != Namespaces.Xmlns ||
            namespaceUri == Namespaces.Xmlns && qualifiedName != "xmlns" && prefix != "xmlns")
        {
            throw DomException.Namespace();
        }

        return new QualifiedName(namespaceUri, localName, prefix);
    }

    private static void ValidatePrefix(string prefix)
    {
        if (prefix.Length == 0)
        {
            throw DomException.InvalidCharacter();
        }

        foreach (var character in prefix)
        {
            if (IsForbiddenNameCharacter(character))
            {
                throw DomException.InvalidCharacter();
            }
        }
    }

    private static bool IsForbiddenNameCharacter(char character)
        => character is '\t' or '\n' or '\f' or '\r' or ' ' or '\0' or '/' or '>';

    private static bool IsAsciiAlpha(char character)
        => character is >= 'A' and <= 'Z' or >= 'a' and <= 'z';

    internal static string AsciiLower(string value)
    {
        var firstUpper = -1;
        for (var i = 0; i < value.Length; i++)
        {
            if (value[i] is >= 'A' and <= 'Z')
            {
                firstUpper = i;
                break;
            }
        }

        if (firstUpper < 0)
        {
            return value;
        }

        return string.Create(value.Length, value, static (destination, source) =>
        {
            for (var i = 0; i < source.Length; i++)
            {
                var character = source[i];
                destination[i] = character is >= 'A' and <= 'Z' ? (char) (character + ('a' - 'A')) : character;
            }
        });
    }
}
