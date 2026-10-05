using Jint.HtmlParser;

namespace Jint.Browser.Runtime;

/// <summary>Resource policy shared by XML navigation, DOMParser and XML fragments.</summary>
internal static class BrowserXmlParsing
{
    internal static XmlParseOptions Options(BrowserOptions? browserOptions)
    {
        var defaults = new XmlParseOptions();
        var memory = browserOptions?.MemoryLimit ?? 0;
        if (memory <= 0 || memory == long.MaxValue) return defaults;

        // Bound UTF-16 input, atomic tokens and entity replacement work by the page's byte budget.
        // This is a parser ceiling, not a substitute for allocation accounting: nodes and buffers
        // can consume more than two bytes per unit, so the allocation constraint can fail first.
        var characters = Math.Max(1, memory / sizeof(char));
        return new XmlParseOptions
        {
            Limits = new ParseLimits
            {
                MaxInputCharacters = characters,
                MaxTokenCharacters = (int) Math.Min(int.MaxValue, characters),
                MaxEntityExpansionCharacters = Math.Min(defaults.Limits.MaxEntityExpansionCharacters, characters)
            }
        };
    }
}
