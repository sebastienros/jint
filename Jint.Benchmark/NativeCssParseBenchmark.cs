#nullable enable

using System.Text;
using BenchmarkDotNet.Attributes;
using Jint.HtmlParser;
using Jint.HtmlParser.Css;
using Jint.HtmlParser.Css.Values;

namespace Jint.Benchmark;

/// <summary>
/// The native CSS parser's two demand layers, as Browser drives them: the rule-boundary scan a sheet gets when
/// it is first seen (<c>CssParser.ParseRuleList</c>, what <c>NativeCssSyntaxCache</c> calls), and the explicit
/// per-rule parse that descends into grouping-rule bodies (<c>CssParser.ParseRule</c>, what
/// <c>NativeCssRuleCache</c> calls), plus the full tokenizer alone through the public component-value parse. Sources are built or loaded once in <see cref="Setup"/>; every row parses
/// from the same immutable string. No engine is involved, so no row shares engine state with a sibling.
/// </summary>
/// <remarks>
/// <see cref="ParseXmlControl"/> is the floor row: the XML tree builder never reaches the CSS tokenizer, so any
/// movement on it between two builds of a CSS change is the cross-process offset, not a result.
/// </remarks>
[MemoryDiagnoser]
[BenchmarkCategory("HtmlParser")]
public class NativeCssParseBenchmark
{
    private string _corpusSheet = null!;
    private string _urlHeavySheet = null!;
    private string _nestedSheet = null!;
    private string _xmlControl = null!;
    private readonly CssValueWork _work = new(CancellationToken.None);

    [GlobalSetup]
    public void Setup()
    {
        var encoding = new UTF8Encoding(false, true);
        string Corpus(string name) =>
            encoding.GetString(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "ParserCorpus", name)));
        _corpusSheet = Corpus("css-rules-large.css");
        _xmlControl = Corpus("xml-small.xml");

        // Icon-font and sprite sheets: long data URLs and quoted strings, which a rule-boundary scan only skips.
        var payload = Convert.ToBase64String(Enumerable.Range(0, 450).Select(i => (byte) (i * 31)).ToArray());
        var urls = new StringBuilder();
        for (var i = 0; i < 256; i++)
        {
            urls.Append(".icon-").Append(i).Append("::before{background-image:url(data:image/png;base64,")
                .Append(payload).Append(");content:\"\\201C glyph ").Append(i)
                .Append("\";font-family:\"Helvetica Neue\",Arial,sans-serif}\n");
        }
        _urlHeavySheet = urls.ToString();

        // Three grouping levels: each level's body is handed to the next parse as a slice of the same source.
        var nested = new StringBuilder("@media (min-width: 1px) {\n");
        for (var group = 0; group < 8; group++)
        {
            nested.Append("@supports (display: grid) {\n@media screen and (max-width: ").Append(600 + group).Append("px) {\n");
            for (var i = 0; i < 32; i++)
            {
                nested.Append(".g").Append(group).Append("-r").Append(i)
                    .Append(" > .item:hover{color:rgb(1 2 3);margin:0 auto 4px;background:url(\"img/")
                    .Append(i).Append(".png\") no-repeat;font-family:\"Segoe UI\",sans-serif}\n");
            }
            nested.Append("}\n}\n");
        }
        _nestedSheet = nested.Append("}\n").ToString();

        var counts = (ScanCorpusSheet(), ScanUrlHeavySheet(), ParseNestedSheet());
        if (counts != (2, 256, 1 + 8 * 2 + 8 * 32))
            throw new InvalidDataException($"NativeCssParseBenchmark: unexpected rule counts {counts}");
    }

    [Benchmark]
    public int ScanCorpusSheet() => CssParser.ParseRuleList(_corpusSheet, _work).Length;

    /// <summary>The full tokenizer and component-value parse alone, with no rule scan in front of it.</summary>
    [Benchmark]
    public int TokenizeCorpusSheet() => MarkupParser.ParseCssComponentValues(_corpusSheet).Count;

    [Benchmark]
    public int ScanUrlHeavySheet() => CssParser.ParseRuleList(_urlHeavySheet, _work).Length;

    [Benchmark]
    public int ParseNestedSheet()
    {
        var count = 0;
        var pending = new Stack<CssRawRule>(CssParser.ParseRuleList(_nestedSheet, _work));
        while (pending.TryPop(out var raw))
        {
            var parsed = CssParser.ParseRule(raw, _work);
            if (parsed is null) continue;
            count++;
            if (parsed.Value.Children is { Kind: CssRuleBodyKind.Group } body)
            {
                foreach (var child in CssParser.ParseRuleList(body, _work)) pending.Push(child);
            }
        }
        return count;
    }

    /// <summary>Control row: the XML parser cannot reach the CSS tokenizer.</summary>
    [Benchmark]
    public int ParseXmlControl() => MarkupParser.ParseXml(_xmlControl).ChildNodes.Count();
}
