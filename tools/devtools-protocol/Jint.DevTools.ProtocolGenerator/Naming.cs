using System.Globalization;
using System.Text;

namespace Jint.DevTools.ProtocolGenerator;

/// <summary>
/// Turns the protocol's own spellings into C# ones, and its descriptions into doc comments the
/// repository's <c>docs/xml-doc-style.md</c> would accept.
/// </summary>
internal static class Naming
{
    /// <summary>The longest a generated <c>&lt;summary&gt;</c> may be, in words.</summary>
    private const int SummaryWordCap = 25;

    /// <summary>
    /// <c>getVersion</c> becomes <c>GetVersion</c>. Only the first character moves, because the protocol's
    /// own casing carries information a re-casing would lose (<c>consoleAPICalled</c>, <c>BrowserContextID</c>).
    /// </summary>
    internal static string Pascal(string name)
    {
        if (name.Length == 0)
        {
            return name;
        }

        return char.ToUpperInvariant(name[0]) + name.Substring(1);
    }

    /// <summary>
    /// The identifier for one admissible string of an enumeration: <c>wasm-expression-stack</c> becomes
    /// <c>WasmExpressionStack</c>, and a leading minus becomes <c>Negative</c> so <c>-Infinity</c> and
    /// <c>Infinity</c> stay distinct.
    /// </summary>
    internal static string Constant(string value)
    {
        var builder = new StringBuilder(value.Length);

        var rest = value;
        if (rest.StartsWith('-'))
        {
            builder.Append("Negative");
            rest = rest.Substring(1);
        }

        var start = true;
        foreach (var character in rest)
        {
            if (char.IsLetterOrDigit(character))
            {
                builder.Append(start ? char.ToUpperInvariant(character) : character);
                start = false;
            }
            else
            {
                start = true;
            }
        }

        var name = builder.ToString();
        if (name.Length == 0)
        {
            throw new ProtocolGeneratorException($"The protocol's enumeration value '{value}' has no characters an identifier could be made of.");
        }

        return char.IsDigit(name[0]) ? "Value" + name : name;
    }

    /// <summary>
    /// The one-sentence <c>&lt;summary&gt;</c> a description becomes, or <paramref name="fallback"/> when the
    /// protocol has no description or its first sentence is longer than the style allows.
    /// </summary>
    internal static string Summary(string? description, string fallback)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            return fallback;
        }

        var sentence = FirstSentence(Collapse(description));
        return sentence is null || WordCount(sentence) > SummaryWordCap ? fallback : Escape(sentence);
    }

    /// <summary>Collapses the newlines the protocol wraps its descriptions at into single spaces.</summary>
    private static string Collapse(string text)
    {
        var builder = new StringBuilder(text.Length);
        var space = false;

        foreach (var character in text)
        {
            if (char.IsWhiteSpace(character))
            {
                space = true;
                continue;
            }

            if (space && builder.Length > 0)
            {
                builder.Append(' ');
            }

            space = false;
            builder.Append(character);
        }

        return builder.ToString();
    }

    /// <summary>
    /// The first sentence of a collapsed description, or <see langword="null"/> when it does not end in a
    /// period. A period inside the last word (<c>e.g.</c>, <c>i.e.</c>) does not end a sentence.
    /// </summary>
    private static string? FirstSentence(string text)
    {
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] != '.' || (i + 1 < text.Length && text[i + 1] != ' '))
            {
                continue;
            }

            var start = text.LastIndexOf(' ', i) + 1;
            var word = text.Substring(start, i - start);
            if (word.Length <= 1 || word.Contains('.', StringComparison.Ordinal))
            {
                continue;
            }

            return text.Substring(0, i + 1);
        }

        return null;
    }

    private static int WordCount(string text)
    {
        var words = 0;
        var inWord = false;

        foreach (var character in text)
        {
            if (char.IsWhiteSpace(character))
            {
                inWord = false;
            }
            else if (!inWord)
            {
                inWord = true;
                words++;
            }
        }

        return words;
    }

    /// <summary>Escapes what a doc comment cannot carry literally.</summary>
    internal static string Escape(string text)
    {
        return text
            .Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal);
    }

    /// <summary>
    /// Where one member is documented, which
    /// <c>Jint.Tests.DevTools/Protocol/ProtocolCitationTests.cs</c> resolves against the vendored JSON.
    /// </summary>
    /// <param name="domain">The domain the member belongs to.</param>
    /// <param name="anchor">The member's anchor, or <see langword="null"/> for the domain itself.</param>
    /// <remarks>
    /// <b>A domain Chrome does not have is cited against our own description instead.</b> Pointing a member
    /// of the <c>Jint</c> domain at Chrome's documentation would send a reader to a page that has never
    /// heard of it, and would fail the citation test — which resolves every <c>chromedevtools</c> URL
    /// against the vendored JSON precisely so that a citation cannot drift into naming nothing.
    /// </remarks>
    internal static string Citation(ProtocolDomain domain, string? anchor = null)
    {
        if (!domain.Vendored)
        {
            return "https://github.com/sebastienros/jint/blob/main/tools/devtools-protocol/" + Protocol.OwnFile;
        }

        var url = string.Create(CultureInfo.InvariantCulture, $"https://chromedevtools.github.io/devtools-protocol/tot/{domain.Name}/");
        return anchor is null ? url : url + "#" + anchor;
    }
}
