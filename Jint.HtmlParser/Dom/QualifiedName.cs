using System.Xml;

namespace Jint.HtmlParser;

internal readonly record struct QualifiedName(string? NamespaceUri, string LocalName, string? Prefix)
{
    internal static void ValidateUnqualified(string name)
    {
        try { XmlConvert.VerifyName(name); }
        catch (XmlException) { throw DomException.InvalidCharacter(); }
    }

    // DOM Standard §1.4: validate and extract a namespace and qualified name.
    internal static QualifiedName Parse(string? namespaceUri, string qualifiedName)
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
            if (namespaceUri is null) throw DomException.Namespace();
        }

        try
        {
            XmlConvert.VerifyNCName(localName);
            if (prefix is not null) XmlConvert.VerifyNCName(prefix);
        }
        catch (XmlException)
        {
            throw DomException.InvalidCharacter();
        }

        if (prefix is "xml" && namespaceUri != Namespaces.Xml ||
            (qualifiedName == "xmlns" || prefix == "xmlns") && namespaceUri != Namespaces.Xmlns ||
            namespaceUri == Namespaces.Xmlns && qualifiedName != "xmlns" && prefix != "xmlns")
        {
            throw DomException.Namespace();
        }

        return new QualifiedName(namespaceUri, localName, prefix);
    }

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
