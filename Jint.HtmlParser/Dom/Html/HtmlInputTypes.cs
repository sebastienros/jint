namespace Jint.HtmlParser;

// HTML Living Standard §4.10.5.1, states of the type attribute.
internal enum HtmlInputType
{
    Hidden,
    Text,
    Search,
    Tel,
    Url,
    Email,
    Password,
    Date,
    Month,
    Week,
    Time,
    DateTimeLocal,
    Number,
    Range,
    Color,
    Checkbox,
    Radio,
    File,
    Submit,
    Image,
    Reset,
    Button
}

internal enum HtmlInputValueMode
{
    Value,
    Default,
    DefaultOn,
    Filename
}

internal readonly struct HtmlInputTypeInfo
{
    internal HtmlInputTypeInfo(string keyword, HtmlInputValueMode valueMode, bool hasSelectionApi,
        bool readOnlyApplies, bool requiredApplies, bool lengthAndSizeApply, bool placeholderApplies,
        bool selectApplies)
    {
        Keyword = keyword;
        ValueMode = valueMode;
        HasSelectionApi = hasSelectionApi;
        ReadOnlyApplies = readOnlyApplies;
        RequiredApplies = requiredApplies;
        LengthAndSizeApply = lengthAndSizeApply;
        PlaceholderApplies = placeholderApplies;
        SelectApplies = selectApplies;
    }

    internal string Keyword { get; }
    internal HtmlInputValueMode ValueMode { get; }
    internal bool HasSelectionApi { get; }
    internal bool ReadOnlyApplies { get; }
    internal bool RequiredApplies { get; }
    internal bool LengthAndSizeApply { get; }
    internal bool PlaceholderApplies { get; }
    internal bool SelectApplies { get; }
}

/// <summary>HTML Living Standard §4.10.5.1, input type keywords and state applicability.</summary>
internal static class HtmlInputTypes
{
    internal static HtmlInputType Parse(string? value)
    {
        if (value is null)
        {
            return HtmlInputType.Text;
        }

        // The type attribute is an ASCII case-insensitive keyword. Its value is not trimmed.
        return HtmlInputTypeLookup.Match(value);
    }

    internal static HtmlInputType Get(Element element)
    {
        ArgumentNullException.ThrowIfNull(element);
        if (element.NamespaceUri != Namespaces.Html || element.LocalName != "input")
        {
            throw new ArgumentException("An HTML input element is required.", nameof(element));
        }

        return Parse(element.GetAttributeNodeNS(null, "type")?.Value);
    }

    internal static HtmlInputTypeInfo Info(HtmlInputType type) => type switch
    {
        HtmlInputType.Hidden => new("hidden", HtmlInputValueMode.Default, false, false, false, false, false, false),
        HtmlInputType.Text => new("text", HtmlInputValueMode.Value, true, true, true, true, true, true),
        HtmlInputType.Search => new("search", HtmlInputValueMode.Value, true, true, true, true, true, true),
        HtmlInputType.Tel => new("tel", HtmlInputValueMode.Value, true, true, true, true, true, true),
        HtmlInputType.Url => new("url", HtmlInputValueMode.Value, true, true, true, true, true, true),
        HtmlInputType.Email => new("email", HtmlInputValueMode.Value, false, true, true, true, true, true),
        HtmlInputType.Password => new("password", HtmlInputValueMode.Value, true, true, true, true, true, true),
        HtmlInputType.Date => new("date", HtmlInputValueMode.Value, false, true, true, false, false, true),
        HtmlInputType.Month => new("month", HtmlInputValueMode.Value, false, true, true, false, false, true),
        HtmlInputType.Week => new("week", HtmlInputValueMode.Value, false, true, true, false, false, true),
        HtmlInputType.Time => new("time", HtmlInputValueMode.Value, false, true, true, false, false, true),
        HtmlInputType.DateTimeLocal => new("datetime-local", HtmlInputValueMode.Value, false, true, true, false, false, true),
        HtmlInputType.Number => new("number", HtmlInputValueMode.Value, false, true, true, false, true, true),
        HtmlInputType.Range => new("range", HtmlInputValueMode.Value, false, false, false, false, false, false),
        HtmlInputType.Color => new("color", HtmlInputValueMode.Value, false, false, false, false, false, true),
        HtmlInputType.Checkbox => new("checkbox", HtmlInputValueMode.DefaultOn, false, false, true, false, false, false),
        HtmlInputType.Radio => new("radio", HtmlInputValueMode.DefaultOn, false, false, true, false, false, false),
        HtmlInputType.File => new("file", HtmlInputValueMode.Filename, false, false, true, false, false, true),
        HtmlInputType.Submit => new("submit", HtmlInputValueMode.Default, false, false, false, false, false, false),
        HtmlInputType.Image => new("image", HtmlInputValueMode.Default, false, false, false, false, false, false),
        HtmlInputType.Reset => new("reset", HtmlInputValueMode.Default, false, false, false, false, false, false),
        HtmlInputType.Button => new("button", HtmlInputValueMode.Default, false, false, false, false, false, false),
        _ => throw new ArgumentOutOfRangeException(nameof(type))
    };

    private static bool EqualsAsciiIgnoreCase(string value, string keyword)
    {
        for (var i = 0; i < keyword.Length; i++)
        {
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
}
