namespace Jint.HtmlParser;

internal enum HtmlTextAreaWrapMode
{
    Soft,
    Hard
}

/// <summary>HTML Living Standard §4.10.19 and §4.10.5.3.1 effective text-control attributes.</summary>
internal static class HtmlTextControlAttributes
{
    private const long BeyondNativeStringLength = (long) int.MaxValue + 1;

    internal static long? GetMinimumAllowedLength(Element element, CancellationToken cancellationToken)
        => GetAllowedLength(element, "minlength", null, cancellationToken);

    internal static long? GetMinimumAllowedLength(Element element, Action<int>? checkpoint,
        CancellationToken cancellationToken)
        => GetAllowedLength(element, "minlength", checkpoint, cancellationToken);

    internal static long? GetMaximumAllowedLength(Element element, CancellationToken cancellationToken)
        => GetAllowedLength(element, "maxlength", null, cancellationToken);

    internal static long? GetMaximumAllowedLength(Element element, Action<int>? checkpoint,
        CancellationToken cancellationToken)
        => GetAllowedLength(element, "maxlength", checkpoint, cancellationToken);

    internal static HtmlTextAreaWrapMode GetEffectiveTextAreaWrap(Element element,
        CancellationToken cancellationToken)
    {
        RequireTextArea(element);
        var work = new HtmlTextWork(cancellationToken);
        work.Check();
        var value = FindAttributeValue(element, "wrap", ref work);
        var result = value is not null && EqualsAsciiIgnoreCase(value, "hard", ref work)
            ? HtmlTextAreaWrapMode.Hard : HtmlTextAreaWrapMode.Soft;
        work.Check();
        return result;
    }

    internal static long GetEffectiveTextAreaColumns(Element element, CancellationToken cancellationToken)
    {
        RequireTextArea(element);
        var work = new HtmlTextWork(cancellationToken);
        work.Check();
        var value = FindAttributeValue(element, "cols", ref work);
        var parsed = ParseNonNegativeInteger(value, ref work);
        work.Check();
        return parsed is > 0 ? parsed.Value : 20;
    }

    private static long? GetAllowedLength(Element element, string attributeName, Action<int>? checkpoint,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(element);
        var work = new HtmlTextWork(cancellationToken, checkpoint);
        work.Check();
        if (element.NamespaceUri != Namespaces.Html)
        {
            work.Check();
            return null;
        }

        if (element.LocalName == "input")
        {
            // HtmlInputTypes.Get is tokenless and reserved for synchronous hooks.
            var type = HtmlInputTypes.Parse(FindAttributeValue(element, "type", ref work));
            if (!HtmlInputTypes.Info(type).LengthAndSizeApply)
            {
                work.Check();
                return null;
            }
        }
        else if (element.LocalName != "textarea")
        {
            work.Check();
            return null;
        }

        var value = FindAttributeValue(element, attributeName, ref work);
        var result = ParseNonNegativeInteger(value, ref work);
        work.Check();
        return result;
    }

    private static string? FindAttributeValue(Element element, string name, ref HtmlTextWork work)
    {
        foreach (var attribute in element.Attributes)
        {
            work.Step();
            if (attribute.NamespaceUri is null && attribute.LocalName == name)
            {
                return attribute.Value;
            }
        }

        return null;
    }

    // HTML's nonnegative-integer parser accepts ASCII leading whitespace, optional +,
    // and a digit prefix. The saturation is a semantic limit, not IDL reflection.
    private static long? ParseNonNegativeInteger(string? value, ref HtmlTextWork work)
    {
        if (value is null)
        {
            return null;
        }

        var i = 0;
        while (i < value.Length && HtmlTextSanitizer.IsAsciiWhitespace(value[i]))
        {
            work.Step();
            i++;
        }

        if (i < value.Length && (value[i] is '+' or '-'))
        {
            var negative = value[i] == '-';
            work.Step();
            i++;
            if (negative)
            {
                // A negative numeric result is invalid; -0 parses to zero.
                var digits = 0;
                var nonzero = false;
                while (i < value.Length && value[i] is >= '0' and <= '9')
                {
                    work.Step();
                    nonzero |= value[i++] != '0';
                    digits++;
                }

                return digits > 0 && !nonzero ? 0 : null;
            }
        }

        if (i == value.Length || value[i] is < '0' or > '9')
        {
            return null;
        }

        long result = 0;
        do
        {
            work.Step();
            var digit = value[i++] - '0';
            result = Math.Min(BeyondNativeStringLength, result * 10 + digit);
        } while (i < value.Length && value[i] is >= '0' and <= '9');

        return result;
    }

    private static bool EqualsAsciiIgnoreCase(string value, string keyword, ref HtmlTextWork work)
    {
        if (value.Length != keyword.Length)
        {
            return false;
        }

        for (var i = 0; i < value.Length; i++)
        {
            work.Step();
            var character = value[i];
            if (character is >= 'A' and <= 'Z')
            {
                character = (char) (character + ('a' - 'A'));
            }

            if (character != keyword[i])
            {
                return false;
            }
        }

        return true;
    }

    private static void RequireTextArea(Element element)
    {
        ArgumentNullException.ThrowIfNull(element);
        if (element.NamespaceUri != Namespaces.Html || element.LocalName != "textarea")
        {
            throw new ArgumentException("An HTML textarea element is required.", nameof(element));
        }
    }
}
