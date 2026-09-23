#nullable enable

using System.Text;
using AngleSharp.Css.Dom;
using AngleSharp.Css.Parser;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;
using AngleSharp.Xml.Parser;
using BenchmarkDotNet.Attributes;
using AngleHtmlParser = global::AngleSharp.Html.Parser.HtmlParser;
using DomNode = AngleSharp.Dom.INode;
using DomNodeType = AngleSharp.Dom.NodeType;

namespace Jint.Benchmark;

/// <summary>
/// AngleSharp control rows for a future Jint.HtmlParser comparison. Each operation consumes a cached source
/// string and produces a complete document, stylesheet or declaration block. Parser construction is outside
/// the measurement; the future candidate must use the same lifetime and output level. No engine is involved.
/// </summary>
[MemoryDiagnoser]
[BenchmarkCategory("HtmlParserCorpus")]
public class HtmlParserCorpusBenchmark
{
    private AngleHtmlParser? _html;
    private XmlParser? _xml;
    private CssParser? _css;
    private ParserCorpusShape _validatedShape;

    [ParamsSource(nameof(Cases))]
    public ParserCorpusCase Case { get; set; } = null!;

    public static IEnumerable<ParserCorpusCase> Cases => ParserCorpus.Cases;

    [GlobalSetup]
    public void Setup()
    {
        _html = new AngleHtmlParser(new HtmlParserOptions { IsScripting = false });
        _xml = new XmlParser();
        _css = new CssParser();
        _validatedShape = ParserCorpus.Validate(Case, ParseAngleSharp());
    }

    /// <summary>Control row: the parser under development cannot execute on this path.</summary>
    [Benchmark(Baseline = true)]
    public object ParseAngleSharp() => Case.Kind switch
    {
        ParserCorpusKind.Html => _html!.ParseDocument(Case.Source),
        ParserCorpusKind.Xml => _xml!.ParseDocument(Case.Source),
        ParserCorpusKind.CssRules => _css!.ParseStyleSheet(Case.Source),
        ParserCorpusKind.CssDeclarations => _css!.ParseDeclaration(Case.Source)
            ?? throw new InvalidDataException($"{Case.Name}: declaration parser returned null"),
        _ => throw new ArgumentOutOfRangeException(),
    };

    /// <summary>Correctness-only entry point. It does not run BenchmarkDotNet or report timings.</summary>
    public static int ValidateAll()
    {
        foreach (var corpusCase in ParserCorpus.Cases)
        {
            var benchmark = new HtmlParserCorpusBenchmark { Case = corpusCase };
            benchmark.Setup();
            Console.WriteLine($"{corpusCase}: {benchmark._validatedShape}");
        }

        ParserCorpus.ValidateCountPreservingMutations();
        Console.WriteLine("Count-preserving HTML, XML and CSS mutations were rejected.");
        return 0;
    }
}

public enum ParserCorpusKind
{
    Html,
    Xml,
    CssRules,
    CssDeclarations,
}

/// <summary>A checked-in UTF-8 input and its structural reference shape.</summary>
public sealed class ParserCorpusCase
{
    internal ParserCorpusCase(string name, ParserCorpusKind kind, string source, int inputBytes, ParserCorpusShape expectedShape)
    {
        Name = name;
        Kind = kind;
        Source = source;
        InputBytes = inputBytes;
        ExpectedShape = expectedShape;
    }

    public string Name { get; }
    public ParserCorpusKind Kind { get; }
    public string Source { get; }
    public int InputBytes { get; }
    public ParserCorpusShape ExpectedShape { get; }

    public override string ToString() => $"{Name} ({InputBytes} B)";
}

/// <summary>
/// Coarse cross-parser shape: it detects missing subtrees and declarations without treating AngleSharp's
/// serialization or HTML error recovery as a normative oracle. A future candidate should inspect this shape
/// and add targeted tree assertions before publishing a performance comparison; CSS shorthand expansion can
/// make raw declaration counts differ even when the resulting styles are equivalent.
/// </summary>
public readonly record struct ParserCorpusShape(
    int Nodes,
    int Elements,
    int Attributes,
    int TextChars,
    int CssRules,
    int CssDeclarations);

internal static class ParserCorpus
{
    private static readonly (string Name, ParserCorpusKind Kind, ParserCorpusShape Shape)[] Manifest =
    [
        ("html-small.html", ParserCorpusKind.Html, new(32, 17, 7, 136, 0, 0)),
        ("html-recovery.html", ParserCorpusKind.Html, new(36, 22, 3, 55, 0, 0)),
        ("html-large.html", ParserCorpusKind.Html, new(2826, 1285, 1280, 12847, 0, 0)),
        ("html-recovery-large.html", ParserCorpusKind.Html, new(1930, 1157, 128, 4696, 0, 0)),
        ("svg-small.svg", ParserCorpusKind.Xml, new(10, 8, 13, 13, 0, 0)),
        ("svg-large.svg", ParserCorpusKind.Xml, new(965, 578, 1154, 663, 0, 0)),
        ("xml-small.xml", ParserCorpusKind.Xml, new(11, 6, 6, 25, 0, 0)),
        ("xml-large.xml", ParserCorpusKind.Xml, new(1539, 769, 770, 4865, 0, 0)),
        ("css-rules-small.css", ParserCorpusKind.CssRules, new(0, 0, 0, 0, 9, 10)),
        ("css-rules-large.css", ParserCorpusKind.CssRules, new(0, 0, 0, 0, 194, 1152)),
        ("css-declarations-small.css", ParserCorpusKind.CssDeclarations, new(0, 0, 0, 0, 0, 23)),
        ("css-declarations-large.css", ParserCorpusKind.CssDeclarations, new(0, 0, 0, 0, 0, 258)),
    ];

    public static IReadOnlyList<ParserCorpusCase> Cases { get; } = Load();

    private static IReadOnlyList<ParserCorpusCase> Load()
    {
        var result = new List<ParserCorpusCase>(Manifest.Length);
        var encoding = new UTF8Encoding(false, true);
        foreach (var (name, kind, shape) in Manifest)
        {
            var bytes = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "ParserCorpus", name));
            result.Add(new ParserCorpusCase(name, kind, encoding.GetString(bytes), bytes.Length, shape));
        }

        return result;
    }

    public static ParserCorpusShape Validate(ParserCorpusCase corpusCase, object result)
    {
        var actual = ShapeOf(result);
        if (actual != corpusCase.ExpectedShape)
        {
            throw new InvalidDataException($"{corpusCase.Name}: expected {corpusCase.ExpectedShape}, got {actual}");
        }

        ValidateFeatures(corpusCase, result);
        return actual;
    }

    public static void ValidateCountPreservingMutations()
    {
        AssertRejected("html-small.html", "A &amp; B", "Z &amp; B",
            source => new AngleHtmlParser(new HtmlParserOptions { IsScripting = false }).ParseDocument(source));
        AssertRejected("xml-small.xml", "<![CDATA[<literal>]]>", "<![CDATA[<Literal>]]>",
            source => new XmlParser().ParseDocument(source));
        AssertRejected("css-rules-small.css", "--brand: #123456", "--brand: #654321",
            source => new CssParser().ParseStyleSheet(source));
    }

    private static void AssertRejected(string name, string before, string after, Func<string, object> parse)
    {
        var original = Cases.Single(corpusCase => corpusCase.Name == name);
        var changed = original.Source.Replace(before, after, StringComparison.Ordinal);
        if (changed == original.Source)
        {
            throw new InvalidDataException($"{name}: negative probe replacement was not found");
        }

        var result = parse(changed);
        if (ShapeOf(result) != original.ExpectedShape)
        {
            throw new InvalidDataException($"{name}: negative probe must preserve the pinned shape");
        }

        try
        {
            Validate(original, result);
        }
        catch (InvalidDataException)
        {
            return;
        }

        throw new InvalidDataException($"{name}: a count-preserving value change passed validation");
    }

    private static void ValidateFeatures(ParserCorpusCase corpusCase, object result)
    {
        if (result is IDocument document)
        {
            var root = document.DocumentElement ?? throw new InvalidDataException($"{corpusCase.Name}: no document element");
            var expectedNamespace = corpusCase.Name.StartsWith("svg-", StringComparison.Ordinal) ? "http://www.w3.org/2000/svg" :
                corpusCase.Kind == ParserCorpusKind.Xml ? "urn:catalog" : "http://www.w3.org/1999/xhtml";
            if (root.NamespaceUri != expectedNamespace)
            {
                throw new InvalidDataException($"{corpusCase.Name}: unexpected root namespace {root.NamespaceUri}");
            }

            var selector = corpusCase.Name switch
            {
                "html-small.html" => "main#root svg",
                "html-recovery.html" => "table td",
                "html-large.html" => "article#item-255",
                "html-recovery-large.html" => "#p127",
                "svg-small.svg" => "circle",
                "svg-large.svg" => "#layer-191",
                "xml-small.xml" => "item[id='b']",
                "xml-large.xml" => "item[id='item-255']",
                _ => null,
            };
            if (selector is not null && document.QuerySelector(selector) is null)
            {
                throw new InvalidDataException($"{corpusCase.Name}: missing {selector}");
            }

            if (corpusCase.Name == "html-small.html")
            {
                var main = document.QuerySelector("main#root")!;
                var template = document.QuerySelector("template") as IHtmlTemplateElement;
                if (main.GetAttribute("data-label") != "A & B"
                    || document.QuerySelector("h1")?.TextContent != "Hello\u00a0world"
                    || document.QuerySelector("script")?.TextContent?.Contains("a < b && c > d", StringComparison.Ordinal) != true
                    || template?.Content.QuerySelector("span")?.TextContent != "inside"
                    || document.QuerySelector("svg")?.NamespaceUri != "http://www.w3.org/2000/svg")
                {
                    throw new InvalidDataException("html-small.html: entity, rawtext, template or foreign-content probe failed");
                }
            }

            if (corpusCase.Name == "html-recovery.html" && document.QuerySelectorAll("table td").Length != 2)
            {
                throw new InvalidDataException("html-recovery.html: table repair probe failed");
            }

            if (corpusCase.Name == "xml-small.xml")
            {
                var first = document.QuerySelector("item[id='a']")!;
                var secondName = document.QuerySelector("item[id='b'] name")!;
                if (first.QuerySelector("name")?.TextContent != "A & B"
                    || first.Attributes.FirstOrDefault(attribute => attribute.Name == "m:rank") is not { NamespaceUri: "urn:meta", Value: "1" }
                    || secondName.FirstChild?.NodeType != DomNodeType.CharacterData
                    || secondName.TextContent != "<literal>")
                {
                    throw new InvalidDataException("xml-small.xml: entity, namespaced attribute or CDATA probe failed");
                }
            }

            if (corpusCase.Name == "xml-large.xml")
            {
                var last = document.QuerySelector("item[id='item-255']")!;
                var name = last.QuerySelector("name")?.TextContent;
                var value = last.QuerySelector("value")?.TextContent;
                var rank = last.Attributes.FirstOrDefault(attribute => attribute.Name == "m:rank");
                if (name != "Entry 255 & Co"
                    || value != "65025"
                    || rank is not { NamespaceUri: "urn:meta", Value: "255" })
                {
                    throw new InvalidDataException($"xml-large.xml: final item probe failed (name={name}, value={value}, rank={rank?.Name}:{rank?.NamespaceUri}:{rank?.Value})");
                }
            }

            if (corpusCase.Name == "svg-small.svg")
            {
                var use = document.QuerySelector("use")!;
                if (document.QuerySelector("title")?.TextContent != "Small & sharp"
                    || use.Attributes.FirstOrDefault(attribute => attribute.Name == "xlink:href") is not
                        { NamespaceUri: "http://www.w3.org/1999/xlink", Value: "#g" })
                {
                    throw new InvalidDataException("svg-small.svg: entity or XLink namespace probe failed");
                }
            }

            if (corpusCase.Name == "svg-large.svg")
            {
                var last = document.QuerySelector("#layer-191")!;
                if (last.GetAttribute("transform") != "translate(15 11)" || last.QuerySelector("text")?.TextContent != "191")
                {
                    throw new InvalidDataException("svg-large.svg: final group probe failed");
                }
            }
        }

        if (result is ICssStyleSheet sheet)
        {
            if (corpusCase.Name == "css-rules-small.css")
            {
                var root = sheet.Rules.OfType<ICssStyleRule>().FirstOrDefault(rule => rule.SelectorText == ":root");
                var media = sheet.Rules.OfType<ICssMediaRule>().FirstOrDefault();
                var supports = sheet.Rules.OfType<ICssSupportsRule>().FirstOrDefault();
                var keyframes = sheet.Rules.OfType<ICssKeyframesRule>().FirstOrDefault();
                if (root?.Style.GetPropertyValue("--brand") != "#123456"
                    || root.Style.GetPropertyValue("color") != "var(--brand)"
                    || media?.Rules.OfType<ICssStyleRule>().FirstOrDefault()?.Style.GetPropertyValue("color") is not { Length: > 0 }
                    || supports?.Rules.OfType<ICssStyleRule>().FirstOrDefault()?.Style.GetPropertyValue("display") != "grid"
                    || keyframes?.Rules.OfType<ICssKeyframeRule>().Count() != 2
                    || keyframes.Rules.OfType<ICssKeyframeRule>().Last().Style.GetPropertyValue("opacity") != "1")
                {
                    throw new InvalidDataException("css-rules-small.css: custom property, conditional rule or keyframe probe failed");
                }
            }

            if (corpusCase.Name == "css-rules-large.css")
            {
                var media = sheet.Rules.OfType<ICssMediaRule>().FirstOrDefault();
                var last = media?.Rules.OfType<ICssStyleRule>().LastOrDefault();
                if (media?.Rules.OfType<ICssStyleRule>().Count() != 192
                    || last?.SelectorText?.Contains("#item-191:hover", StringComparison.Ordinal) != true
                    || last.Style.GetPropertyValue("--index") != "191")
                {
                    throw new InvalidDataException("css-rules-large.css: nested rule or final property probe failed");
                }
            }
        }

        if (result is ICssStyleDeclaration declaration)
        {
            if (corpusCase.Name == "css-declarations-small.css"
                && (declaration.GetPropertyValue("--brand") is not { Length: > 0 }
                    || declaration.GetPropertyValue("background") is not { Length: > 0 }
                    || declaration.GetPropertyValue("font") is not { Length: > 0 }))
            {
                throw new InvalidDataException("css-declarations-small.css: custom property, URL or shorthand probe failed");
            }

            if (corpusCase.Name == "css-declarations-large.css"
                && (declaration.GetPropertyValue("--token-255") != "255px"
                    || declaration.GetPropertyValue("color") is not { Length: > 0 }
                    || declaration.GetPropertyValue("margin-left") is not { Length: > 0 }))
            {
                throw new InvalidDataException("css-declarations-large.css: final declaration probe failed");
            }
        }
    }

    public static ParserCorpusShape ShapeOf(object result)
    {
        if (result is IDocument document)
        {
            var nodes = 0;
            var elements = 0;
            var attributes = 0;
            var textChars = 0;
            var pending = new Stack<DomNode>();
            pending.Push(document);
            while (pending.TryPop(out var node))
            {
                nodes++;
                if (node is IElement element)
                {
                    elements++;
                    attributes += element.Attributes.Length;
                }

                if (node.NodeType is DomNodeType.Text or DomNodeType.CharacterData or DomNodeType.Comment)
                {
                    textChars += node.TextContent?.Length ?? 0;
                }

                foreach (var child in node.ChildNodes)
                {
                    pending.Push(child);
                }

                if (node is IHtmlTemplateElement template)
                {
                    pending.Push(template.Content);
                }
            }

            return new ParserCorpusShape(nodes, elements, attributes, textChars, 0, 0);
        }

        if (result is ICssStyleDeclaration declaration)
        {
            return new ParserCorpusShape(0, 0, 0, 0, 0, declaration.Length);
        }

        if (result is ICssStyleSheet sheet)
        {
            var rules = 0;
            var declarations = 0;
            var pending = new Stack<ICssRule>();
            foreach (var rule in sheet.Rules)
            {
                pending.Push(rule);
            }

            while (pending.TryPop(out var rule))
            {
                rules++;
                if (rule is ICssStyleRule style)
                {
                    declarations += style.Style.Length;
                }

                if (rule is ICssGroupingRule group)
                {
                    foreach (var child in group.Rules)
                    {
                        pending.Push(child);
                    }
                }
            }

            return new ParserCorpusShape(0, 0, 0, 0, rules, declarations);
        }

        throw new InvalidDataException($"Unexpected parser result: {result.GetType().FullName}");
    }
}
