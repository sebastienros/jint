#nullable enable

using System.Buffers;
using System.Collections.Frozen;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;

namespace Jint.Benchmark;

/// <summary>Only adds reporting columns; the repository runner owns runtime/job/environment settings.</summary>
public sealed class MarkupPrimitiveConfig : ManualConfig
{
    public MarkupPrimitiveConfig() => AddColumn(StatisticColumn.OperationsPerSecond);
}

/// <summary>
/// HTML ASCII whitespace membership, one character per reported operation. Immutable lookup setup is
/// excluded; there is no engine. The table is the ASCII fast-lane idea from Markdig's CharacterMap,
/// specialized to membership. It is not a benchmark of Markdig's generic parser dispatcher.
/// </summary>
[MemoryDiagnoser]
[Config(typeof(MarkupPrimitiveConfig))]
[BenchmarkCategory("MarkupPrimitives")]
public class MarkupCharacterMembershipBenchmark
{
    internal const int CharacterCount = 4096;
    private readonly SearchValues<char> _values = SearchValues.Create("\t\n\f\r ");
    private readonly bool[] _map = MarkupPrimitiveData.CreateWhitespaceMap();
    private string _input = null!;

    [Params("Text", "Whitespace")]
    public string Distribution { get; set; } = "Text";

    [GlobalSetup]
    public void Setup()
    {
        _input = MarkupPrimitiveData.Repeat(Distribution == "Text"
            ? "A text node with café 中文 and 😀.\r\n"
            : " \t\r\n\f \tX\u00a0\u2003\u2028", CharacterCount);
        var expected = _input.Count(static c => "\t\n\f\r ".Contains(c, StringComparison.Ordinal));
        MarkupPrimitiveData.Require(OrPattern() == expected && AsciiMap() == expected && BitMask() == expected &&
            CachedSearchValues() == expected, "whitespace benchmark results");
    }

    [Benchmark(Baseline = true, OperationsPerInvoke = CharacterCount)]
    public int OrPattern()
    {
        var count = 0;
        foreach (var c in _input)
            if (c is '\t' or '\n' or '\f' or '\r' or ' ') count++;
        return count;
    }

    [Benchmark(OperationsPerInvoke = CharacterCount)]
    public int AsciiMap()
    {
        var count = 0;
        var map = _map;
        foreach (var c in _input)
            if (c < map.Length && map[c]) count++;
        return count;
    }

    [Benchmark(OperationsPerInvoke = CharacterCount)]
    public int CachedSearchValues()
    {
        var count = 0;
        var values = _values;
        foreach (var c in _input)
            if (values.Contains(c)) count++;
        return count;
    }

    [Benchmark(OperationsPerInvoke = CharacterCount)]
    public int BitMask()
    {
        var count = 0;
        foreach (var c in _input)
            if (c <= ' ' && ((0x100003600UL >> c) & 1) != 0) count++;
        return count;
    }
}

/// <summary>
/// Locates every HTML data delimiter in a buffer; one whole buffer per reported operation. This kernel
/// does not decode, normalize, validate scalars, construct tokens or run parser cancellation checks.
/// Static search structures and cached inputs are outside timing; each method returns the same checksum.
/// </summary>
[MemoryDiagnoser]
[Config(typeof(MarkupPrimitiveConfig))]
[BenchmarkCategory("MarkupPrimitives")]
public class MarkupDelimiterScanBenchmark
{
    private static readonly SearchValues<char> Delimiters = SearchValues.Create("<&\r\0");
    private string _input = null!;

    [Params("Short", "LongText", "DenseMarkup", "Unicode")]
    public string Input { get; set; } = "LongText";

    [GlobalSetup]
    public void Setup()
    {
        _input = Input switch
        {
            "Short" => "small text &amp; more <b>",
            "LongText" => MarkupPrimitiveData.Repeat("ordinary text without special characters. ", 4095) + "<",
            "DenseMarkup" => MarkupPrimitiveData.Repeat("<a>&amp;\r\n<b>\0&x;", 4096),
            "Unicode" => MarkupPrimitiveData.Repeat("café 中文 😀 Ελληνικά &amp; ", 4096),
            _ => throw new InvalidOperationException(Input)
        };
        var expected = Scalar(_input);
        MarkupPrimitiveData.Require(SpanSearch(_input) == expected && CachedSearch(_input) == expected,
            "delimiter benchmark results");
    }

    [Benchmark(Baseline = true)]
    public int OrPattern() => Scalar(_input);

    [Benchmark]
    public int SpanIndexOfAny() => SpanSearch(_input);

    [Benchmark]
    public int CachedSearchValues() => CachedSearch(_input);

    internal static int Scalar(ReadOnlySpan<char> input)
    {
        var checksum = 0;
        for (var i = 0; i < input.Length; i++)
            if (input[i] is '<' or '&' or '\r' or '\0') checksum += i + 1;
        return checksum;
    }

    internal static int SpanSearch(ReadOnlySpan<char> input)
    {
        var checksum = 0;
        var offset = 0;
        while (offset < input.Length)
        {
            var index = input[offset..].IndexOfAny("<&\r\0");
            if (index < 0) break;
            offset += index + 1;
            checksum += offset;
        }
        return checksum;
    }

    internal static int CachedSearch(ReadOnlySpan<char> input)
    {
        var checksum = 0;
        var offset = 0;
        while (offset < input.Length)
        {
            var index = input[offset..].IndexOfAny(Delimiters);
            if (index < 0) break;
            offset += index + 1;
            checksum += offset;
        }
        return checksum;
    }
}

/// <summary>
/// Exact, case-sensitive membership of already materialized tag names in the native InBody block-start
/// set. One tag per reported operation. Non-interned input strings and lookup construction are untimed.
/// This does not give a span/prefix matcher credit for skipping work already performed by the tokenizer.
/// </summary>
[MemoryDiagnoser]
[Config(typeof(MarkupPrimitiveConfig))]
[BenchmarkCategory("MarkupPrimitives")]
public class MarkupTagMembershipBenchmark
{
    internal const int TagCount = 256;
    private readonly HashSet<string> _hash = new(MarkupPrimitiveData.BlockTags, StringComparer.Ordinal);
    private readonly FrozenSet<string> _frozen = MarkupPrimitiveData.BlockTags.ToFrozenSet(StringComparer.Ordinal);
    private string[] _input = null!;

    [Params("Hits", "Misses", "Mixed")]
    public string Distribution { get; set; } = "Mixed";

    [GlobalSetup]
    public void Setup()
    {
        var misses = new[] { "span", "a", "img", "custom-element", "divx", "articlex", "DIV", "", "figcaptio", "\u0130", "séction" };
        _input = new string[TagCount];
        var expected = 0;
        var random = new Random(1979);
        for (var i = 0; i < _input.Length; i++)
        {
            var hit = Distribution == "Hits" || Distribution == "Mixed" && random.Next(2) == 0;
            var source = hit ? MarkupPrimitiveData.BlockTags : misses;
            // Parser-created names do not have string-literal identity with the lookup keys.
            _input[i] = new string(source[random.Next(source.Length)].AsSpan());
            if (hit) expected++;
        }
        MarkupPrimitiveData.Require(OrPattern() == expected && HashSet() == expected && FrozenSet() == expected,
            "tag benchmark results");
    }

    [Benchmark(Baseline = true, OperationsPerInvoke = TagCount)]
    public int OrPattern()
    {
        var count = 0;
        foreach (var name in _input)
            if (IsBlockStart(name)) count++;
        return count;
    }

    [Benchmark(OperationsPerInvoke = TagCount)]
    public int HashSet()
    {
        var count = 0;
        foreach (var name in _input)
            if (_hash.Contains(name)) count++;
        return count;
    }

    [Benchmark(OperationsPerInvoke = TagCount)]
    public int FrozenSet()
    {
        var count = 0;
        foreach (var name in _input)
            if (_frozen.Contains(name)) count++;
        return count;
    }

    internal static bool IsBlockStart(string name) => name is "address" or "article" or "aside" or
        "blockquote" or "center" or "details" or "dialog" or "dir" or "div" or "dl" or "fieldset" or
        "figcaption" or "figure" or "footer" or "header" or "hgroup" or "main" or "menu" or "nav" or
        "ol" or "p" or "search" or "section" or "summary" or "ul";
}

/// <summary>
/// Reports one-time construction/allocation separately from hot lookup. Compare only rows building
/// the same named set; these are not parser-throughput rows. The or-pattern alternatives need no table.
/// </summary>
[MemoryDiagnoser]
[Config(typeof(MarkupPrimitiveConfig))]
[BenchmarkCategory("MarkupPrimitivesConstruction")]
public class MarkupLookupConstructionBenchmark
{
    [Benchmark]
    public bool[] WhitespaceAsciiMap() => MarkupPrimitiveData.CreateWhitespaceMap();

    [Benchmark]
    public SearchValues<char> WhitespaceSearchValues() => SearchValues.Create("\t\n\f\r ");

    [Benchmark]
    public HashSet<string> BlockTagsHashSet() => new(MarkupPrimitiveData.BlockTags, StringComparer.Ordinal);

    [Benchmark]
    public FrozenSet<string> BlockTagsFrozenSet() => MarkupPrimitiveData.BlockTags.ToFrozenSet(StringComparer.Ordinal);
}

internal static class MarkupPrimitiveData
{
    internal static readonly string[] BlockTags =
    [
        "address", "article", "aside", "blockquote", "center", "details", "dialog", "dir", "div", "dl",
        "fieldset", "figcaption", "figure", "footer", "header", "hgroup", "main", "menu", "nav", "ol", "p",
        "search", "section", "summary", "ul"
    ];

    internal static bool[] CreateWhitespaceMap()
    {
        var result = new bool[128];
        foreach (var c in "\t\n\f\r ") result[c] = true;
        return result;
    }

    internal static string Repeat(string seed, int length)
        => string.Create(length, seed, static (destination, text) =>
        {
            for (var i = 0; i < destination.Length; i++) destination[i] = text[i % text.Length];
        });

    internal static void Require(bool condition, string detail)
    {
        if (!condition) throw new InvalidOperationException("Parser primitive validation failed: " + detail);
    }

    internal static int Validate()
    {
        var whitespace = SearchValues.Create("\t\n\f\r ");
        var map = CreateWhitespaceMap();
        for (var code = 0; code <= char.MaxValue; code++)
        {
            var c = (char) code;
            var expected = c is '\t' or '\n' or '\f' or '\r' or ' ';
            Require(whitespace.Contains(c) == expected && (c < map.Length && map[c]) == expected &&
                (c <= ' ' && ((0x100003600UL >> c) & 1) != 0) == expected,
                $"whitespace U+{code:X4}");
        }

        var random = new Random(2077);
        for (var length = 0; length <= 257; length++)
        {
            var buffer = new char[length + 2];
            for (var i = 0; i < buffer.Length; i++)
                buffer[i] = random.Next(4) == 0 ? "<&\r\0"[random.Next(4)] : (char) random.Next(65536);
            for (var start = 0; start <= 2; start++)
            {
                var slice = buffer.AsSpan(start, length);
                var expected = MarkupDelimiterScanBenchmark.Scalar(slice);
                Require(MarkupDelimiterScanBenchmark.SpanSearch(slice) == expected &&
                    MarkupDelimiterScanBenchmark.CachedSearch(slice) == expected, $"delimiter slice {start}/{length}");
                var delimiters = SearchValues.Create("<&\r\0");
                for (var offset = 0; offset <= slice.Length; offset++)
                {
                    var suffix = slice[offset..];
                    var first = -1;
                    for (var i = 0; i < suffix.Length; i++)
                    {
                        if (suffix[i] is not ('<' or '&' or '\r' or '\0')) continue;
                        first = i;
                        break;
                    }
                    Require(suffix.IndexOfAny(delimiters) == first && suffix.IndexOfAny("<&\r\0") == first,
                        $"first delimiter {start}/{length}/{offset}");
                }
            }
        }

        var frozen = BlockTags.ToFrozenSet(StringComparer.Ordinal);
        foreach (var name in BlockTags.SelectMany(static name => new[] { name, name.ToUpperInvariant(), name + "x", name[..^1] }))
            Require(MarkupTagMembershipBenchmark.IsBlockStart(name) == frozen.Contains(name), "tag " + name);

        foreach (var distribution in new[] { "Text", "Whitespace" })
            new MarkupCharacterMembershipBenchmark { Distribution = distribution }.Setup();
        foreach (var input in new[] { "Short", "LongText", "DenseMarkup", "Unicode" })
            new MarkupDelimiterScanBenchmark { Input = input }.Setup();
        foreach (var distribution in new[] { "Hits", "Misses", "Mixed" })
            new MarkupTagMembershipBenchmark { Distribution = distribution }.Setup();

        Console.WriteLine("Parser primitives: exhaustive UTF-16 membership, sliced delimiter scans, exact tag lookup and all benchmark inputs agree. No timing performed.");
        return 0;
    }
}
