using static Jint.HtmlParser.Css.Selectors.CompiledSelector;

namespace Jint.HtmlParser.Css.Selectors;

internal static partial class SelectorMatcher
{
    // https://drafts.csswg.org/selectors/#the-lang-pseudo
    private static bool MatchLanguage(Predicate predicate, Element element, ref Work work)
    {
        var language = LanguageOf(element, ref work);
        foreach (var range in predicate.TextArguments!)
        {
            work.Step();
            if (LanguageRangeMatches(language, range, ref work)) return true;
        }
        return false;
    }

    private static string LanguageOf(Element element, ref Work work)
    {
        for (Element? current = element; current is not null;
             current = current.ParentNode is ShadowRoot shadow ? shadow.Host : current.ParentNode as Element)
        {
            work.Step();
            string? htmlLanguage = null;
            foreach (var attribute in current.Attributes)
            {
                work.Step();
                if (attribute.LocalName != "lang") continue;
                if (attribute.NamespaceUri == Namespaces.Xml) return attribute.Value;
                if (current.NamespaceUri == Namespaces.Html && attribute.NamespaceUri is null)
                    htmlLanguage = attribute.Value;
            }
            if (htmlLanguage is not null) return htmlLanguage;
        }
        return "";
    }

    // RFC 4647 §3.3.2: wildcards skip range subtags; a singleton in the language
    // tag stops a search for a later non-wildcard subtag.
    private static bool LanguageRangeMatches(string language, string range, ref Work work)
    {
        if (range.Length == 0) return language.Length == 0;
        if (language.Length == 0) return false;
        var languageIndex = 0;
        var rangeIndex = 0;
        var languagePart = LanguageSubtag(language, ref languageIndex, ref work);
        var rangePart = LanguageSubtag(range, ref rangeIndex, ref work);
        if (!rangePart.SequenceEqual("*") && !TextEquals(languagePart, rangePart, true, ref work)) return false;
        while (rangeIndex <= range.Length)
        {
            work.Step();
            rangePart = LanguageSubtag(range, ref rangeIndex, ref work);
            if (rangePart.SequenceEqual("*")) continue;
            var matched = false;
            while (languageIndex <= language.Length)
            {
                work.Step();
                languagePart = LanguageSubtag(language, ref languageIndex, ref work);
                if (TextEquals(languagePart, rangePart, true, ref work))
                {
                    matched = true;
                    break;
                }
                if (languagePart.Length == 1) return false;
            }
            if (!matched) return false;
        }
        return true;
    }

    private static ReadOnlySpan<char> LanguageSubtag(string text, scoped ref int index, scoped ref Work work)
    {
        var start = index;
        while (index < text.Length && text[index] != '-')
        {
            work.Step();
            index++;
        }
        var part = text.AsSpan(start, index - start);
        index++;
        return part;
    }
}
