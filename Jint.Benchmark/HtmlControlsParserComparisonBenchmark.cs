#nullable enable
using System.Text;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using AngleHtmlParser = global::AngleSharp.Html.Parser.HtmlParser;
using ParserModel = global::Jint.HtmlParser;

namespace Jint.Benchmark;

/// <summary>
/// Controls-heavy text/url/email/password parsing, parsing plus first value access, and warm value access are distinct pairs.
/// First access includes a fresh parse each invocation; warm access uses a row-owned document and
/// cached control array. This exposes deferred sanitization without an iteration setup or engine.
/// Each AngleSharp row is an untouched control, with identical sources and semantic checksums.
/// </summary>
[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class HtmlControlsParserComparisonBenchmark
{
    private AngleHtmlParser _angle = null!;
    private ParserModel.HtmlParseOptions _native = null!;
    private IHtmlInputElement[] _warmAngle = null!;
    private ParserModel.Element[] _warmNative = null!;
    private static readonly string Source = CreateSource();

    [GlobalSetup(Targets = [nameof(ParseAngleSharp), nameof(ParseAndFirstAccessAngleSharp), nameof(WarmAccessAngleSharp)])]
    public void SetupAngleSharp()
    {
        _angle = new AngleHtmlParser(new HtmlParserOptions { IsScripting = false });
        var document = ParseAngleSharp();
        _warmAngle = Inputs(document);
        _ = Read(_warmAngle);
    }

    [GlobalSetup(Targets = [nameof(ParseNative), nameof(ParseAndFirstAccessNative), nameof(WarmAccessNative)])]
    public void SetupNative()
    {
        _native = new ParserModel.HtmlParseOptions();
        var document = ParseNative();
        var reference = new AngleHtmlParser(new HtmlParserOptions { IsScripting = false }).ParseDocument(Source);
        HtmlCorpusComparer.Compare("controls-heavy", reference, document);
        _warmNative = Inputs(document);
        if (_warmNative.Length != 256 || _warmNative.Any(input => input.ExistingInputValueState is not null))
            throw new InvalidDataException("Parsing eagerly initialized input values or lost controls.");
        var referenceInputs = Inputs(reference);
        for (var i = 0; i < _warmNative.Length; i++)
        {
            var actual = _warmNative[i].GetHtmlState()!.GetInputValueState(default)!.GetValue(default);
            if (actual != referenceInputs[i].Value)
                throw new InvalidDataException($"Control {i} type {_warmNative[i].GetAttribute("type")}: '{actual}' != '{referenceInputs[i].Value}'");
        }
    }

    [Benchmark(Baseline = true), BenchmarkCategory("Parse")]
    public IDocument ParseAngleSharp() => _angle.ParseDocument(Source);
    [Benchmark, BenchmarkCategory("Parse")]
    public ParserModel.Document ParseNative() => ParserModel.MarkupParser.ParseHtml(Source, _native);

    [Benchmark(Baseline = true), BenchmarkCategory("ParseAndFirstAccess")]
    public long ParseAndFirstAccessAngleSharp() => Read(Inputs(ParseAngleSharp()));
    [Benchmark, BenchmarkCategory("ParseAndFirstAccess")]
    public long ParseAndFirstAccessNative() => Read(Inputs(ParseNative()));

    [Benchmark(Baseline = true), BenchmarkCategory("WarmAccess")]
    public long WarmAccessAngleSharp() => Read(_warmAngle);
    [Benchmark, BenchmarkCategory("WarmAccess")]
    public long WarmAccessNative() => Read(_warmNative);

    private static IHtmlInputElement[] Inputs(IDocument document)
    {
        var inputs = new List<IHtmlInputElement>();
        var pending = new Stack<AngleSharp.Dom.INode>(); pending.Push(document);
        while (pending.TryPop(out var node))
        {
            if (node is IHtmlInputElement input) inputs.Add(input);
            for (var child = node.LastChild; child is not null; child = child.PreviousSibling) pending.Push(child);
        }
        return inputs.ToArray();
    }
    private static ParserModel.Element[] Inputs(ParserModel.Document document) => HtmlCorpusComparer.Descendants(document)
        .OfType<ParserModel.Element>().Where(element => element.NamespaceUri == ParserModel.Namespaces.Html && element.LocalName == "input").ToArray();
    private static long Read(IHtmlInputElement[] inputs)
    {
        long checksum = 0;
        foreach (var input in inputs) checksum += input.Value?.Length ?? 0;
        return checksum;
    }
    private static long Read(ParserModel.Element[] inputs)
    {
        long checksum = 0;
        foreach (var input in inputs)
            checksum += input.GetHtmlState()!.GetInputValueState(default)!.GetValue(default).Length;
        return checksum;
    }
    private static string CreateSource()
    {
        var source = new StringBuilder("<!doctype html><form>");
        for (var i = 0; i < 64; i++)
            source.Append("<input type=text value='text &amp; ").Append(i)
                .Append("'><input type=url value='https://example.test/path'><input type=email value='reader@example.test'><input type=password value='password'>");
        return source.Append("</form>").ToString();
    }
    internal static void ValidateAll()
    {
        var benchmark = new HtmlControlsParserComparisonBenchmark();
        benchmark.SetupAngleSharp(); benchmark.SetupNative();
        if (benchmark.ParseAndFirstAccessAngleSharp() != benchmark.ParseAndFirstAccessNative() ||
            benchmark.WarmAccessAngleSharp() != benchmark.WarmAccessNative())
            throw new InvalidDataException("First/warm controls workload differs.");
        Console.WriteLine("256 controls: parse remains lazy; first and warm value workloads agree.");
    }
}
