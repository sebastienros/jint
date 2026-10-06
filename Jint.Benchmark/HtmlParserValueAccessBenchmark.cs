#nullable enable
using AngleSharp.Dom;
using BenchmarkDotNet.Attributes;
using ParserModel = global::Jint.HtmlParser;

namespace Jint.Benchmark;

/// <summary>
/// Fresh large HTML documents followed by reading every text and attribute value.
/// String access exposes deferred materialization; the internal span row exercises
/// the same checksum without requiring strings. AngleSharp is the unchanged control.
/// Each row has its own parser configuration and validates its checksum outside timing.
/// </summary>
[MemoryDiagnoser]
public class HtmlParserValueAccessBenchmark
{
    private HtmlParserComparisonBenchmark _parser = null!;

    [GlobalSetup(Target = nameof(ParseAndReadAngleSharp))]
    public void SetupAngleSharp()
    {
        CreateParser();
        _parser.SetupAngleSharp();
    }

    [GlobalSetup(Targets = [nameof(ParseAndReadNativeStrings), nameof(ParseAndReadNativeSpans)])]
    public void SetupNative()
    {
        CreateParser();
        _parser.SetupNative();
        var reference = ParseAndReadAngleSharp();
        if (ParseAndReadNativeStrings() != reference || ParseAndReadNativeSpans() != reference)
            throw new InvalidDataException("Parser value-access checksums differ.");
    }

    private void CreateParser() => _parser = new HtmlParserComparisonBenchmark
    {
        Case = HtmlParserComparisonBenchmark.Cases.Single(item => item.Name == "html-large.html")
    };

    [Benchmark(Baseline = true)]
    public long ParseAndReadAngleSharp()
    {
        long checksum = 0;
        var pending = new Stack<AngleSharp.Dom.INode>();
        pending.Push(_parser.ParseAngleSharp());
        while (pending.TryPop(out var node))
        {
            if (node is IText text) checksum = Read(text.Data, checksum);
            if (node is IElement element)
                foreach (var attribute in element.Attributes) checksum = Read(attribute.Value, checksum);
            if (node is AngleSharp.Html.Dom.IHtmlTemplateElement template) pending.Push(template.Content);
            for (var child = node.LastChild; child is not null; child = child.PreviousSibling) pending.Push(child);
        }
        return checksum;
    }

    [Benchmark]
    public long ParseAndReadNativeStrings() => ReadNative(_parser.ParseNative(), spans: false);

    [Benchmark]
    public long ParseAndReadNativeSpans() => ReadNative(_parser.ParseNative(), spans: true);

    private static long ReadNative(ParserModel.Document document, bool spans)
    {
        long checksum = 0;
        foreach (var node in HtmlCorpusComparer.Descendants(document))
        {
            if (node is ParserModel.Text text) checksum = Read(spans ? text.DataSpan : text.Data, checksum);
            if (node is ParserModel.Element element)
                foreach (var attribute in element.Attributes)
                    checksum = Read(spans ? attribute.ValueSpan : attribute.Value, checksum);
        }
        return checksum;
    }

    private static long Read(ReadOnlySpan<char> value, long checksum)
    {
        foreach (var character in value) checksum = unchecked(checksum * 31 + character);
        return checksum;
    }
}
