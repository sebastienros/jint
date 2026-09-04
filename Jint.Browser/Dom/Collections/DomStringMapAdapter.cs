using System.Collections;
using AngleSharp.Dom;

namespace Jint.Browser.Dom.Collections;

/// <summary>
/// Adapts an element's <c>data-*</c> attributes to HTML's <c>DOMStringMap</c> naming algorithms.
/// </summary>
internal sealed class DomStringMapAdapter : IStringMap
{
    private const string Prefix = "data-";
    private const string Member = "DOMStringMap";

    private readonly DomRealm _realm;
    private readonly IElement _element;

    internal DomStringMapAdapter(DomRealm realm, IElement element)
    {
        _realm = realm;
        _element = element;
    }

    public string? this[string name]
    {
        get
        {
            if (ContainsDashLowercase(name))
            {
                return null;
            }

            var attributeName = ToAttributeName(name);
            return DomNames.IsValidAttributeLocalName(attributeName)
                ? _element.GetAttribute(attributeName)
                : null;
        }
        set
        {
            // https://html.spec.whatwg.org/multipage/dom.html#dom-domstringmap-setitem
            if (ContainsDashLowercase(name))
            {
                DomFailures.Refuse(
                    _realm.Engine,
                    Member,
                    Jint.WebApi.DomException.DomExceptionNames.Syntax,
                    "the property name '" + name + "' contains a hyphen followed by a lowercase ASCII letter.");
            }

            var attributeName = ToAttributeName(name);
            if (!DomNames.IsValidAttributeLocalName(attributeName))
            {
                DomFailures.Refuse(
                    _realm.Engine,
                    Member,
                    Jint.WebApi.DomException.DomExceptionNames.InvalidCharacter,
                    "the property name '" + name + "' does not produce a valid attribute name.");
            }

            _element.SetAttribute(attributeName, value ?? "");
        }
    }

    public void Remove(string name)
    {
        // https://html.spec.whatwg.org/multipage/dom.html#dom-domstringmap-removeitem
        _element.RemoveAttribute(ToAttributeName(name));
    }

    public IEnumerator<KeyValuePair<string, string>> GetEnumerator()
    {
        // https://html.spec.whatwg.org/multipage/dom.html#concept-domstringmap-pairs
        foreach (var attribute in _element.Attributes)
        {
            if (attribute.NamespaceUri is not null
                || !attribute.Name.StartsWith(Prefix, StringComparison.Ordinal))
            {
                continue;
            }

            var suffix = attribute.Name.AsSpan(Prefix.Length);
            if (ContainsAsciiUppercase(suffix))
            {
                continue;
            }

            yield return new KeyValuePair<string, string>(ToPropertyName(suffix), attribute.Value);
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    private static string ToAttributeName(string name)
    {
        var uppercaseCount = 0;
        foreach (var character in name)
        {
            if (IsAsciiUppercase(character))
            {
                uppercaseCount++;
            }
        }

        return string.Create(Prefix.Length + name.Length + uppercaseCount, name, static (result, source) =>
        {
            Prefix.AsSpan().CopyTo(result);
            var write = Prefix.Length;
            foreach (var character in source)
            {
                if (IsAsciiUppercase(character))
                {
                    result[write++] = '-';
                    result[write++] = (char) (character + ('a' - 'A'));
                }
                else
                {
                    result[write++] = character;
                }
            }
        });
    }

    private static string ToPropertyName(ReadOnlySpan<char> name)
    {
        var removedHyphens = 0;
        for (var i = 0; i + 1 < name.Length; i++)
        {
            if (name[i] == '-' && IsAsciiLowercase(name[i + 1]))
            {
                removedHyphens++;
                i++;
            }
        }

        return string.Create(name.Length - removedHyphens, name.ToString(), static (result, source) =>
        {
            var write = 0;
            for (var read = 0; read < source.Length; read++)
            {
                var character = source[read];
                if (character == '-' && read + 1 < source.Length && IsAsciiLowercase(source[read + 1]))
                {
                    result[write++] = (char) (source[++read] - ('a' - 'A'));
                }
                else
                {
                    result[write++] = character;
                }
            }
        });
    }

    private static bool ContainsDashLowercase(string name)
    {
        for (var i = 0; i + 1 < name.Length; i++)
        {
            if (name[i] == '-' && IsAsciiLowercase(name[i + 1]))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ContainsAsciiUppercase(ReadOnlySpan<char> value)
    {
        foreach (var character in value)
        {
            if (IsAsciiUppercase(character))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsAsciiUppercase(char value) => value is >= 'A' and <= 'Z';

    private static bool IsAsciiLowercase(char value) => value is >= 'a' and <= 'z';
}
