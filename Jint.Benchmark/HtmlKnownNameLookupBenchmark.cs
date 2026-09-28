#nullable enable
using System.Collections.Frozen;
using System.IO.Hashing;
using System.Runtime.InteropServices;
using BenchmarkDotNet.Attributes;
using Jint.HtmlParser.Html;

namespace Jint.Benchmark;

/// <summary>
/// Isolated recognition of the parser's already ASCII-folded UTF-16 names. Padded source slices
/// give the unchanged substring control its delimiters without timing an extra copy. All methods
/// return canonical literals, verify collisions, and avoid materializing input strings.
/// </summary>
[MemoryDiagnoser]
public class HtmlKnownNameLookupBenchmark
{
    private const int BatchSize = 256;
    private const int MaxNameLength = 64;
    private string[] _inputs = null!;
    private string[][] _byLength = null!;
    private string _source = null!;
    private string?[] _byOffset = null!;
    private (int Start, int Length)[] _ranges = null!;
    private Dictionary<string, string>.AlternateLookup<ReadOnlySpan<char>> _dictionary;
    private FrozenDictionary<string, string>.AlternateLookup<ReadOnlySpan<char>> _frozen;
    private Dictionary<ulong, string[]> _hash = null!;

    [Params("Mixed", "AllKnown", "Misses")]
    public string Distribution { get; set; } = null!;

    [GlobalSetup]
    public void Setup()
    {
        var names = HtmlKnownNames.Values.ToArray();
        _byLength = Enumerable.Range(0, MaxNameLength + 1)
            .Select(length => names.Where(name => name.Length == length).ToArray()).ToArray();
        var dictionary = names.ToDictionary(name => name, name => name, StringComparer.Ordinal);
        _dictionary = dictionary.GetAlternateLookup<ReadOnlySpan<char>>();
        _frozen = dictionary.ToFrozenDictionary(StringComparer.Ordinal).GetAlternateLookup<ReadOnlySpan<char>>();
        _hash = names.GroupBy(name => XxHash3.HashToUInt64(MemoryMarshal.AsBytes(name.AsSpan())))
            .ToDictionary(group => group.Key, group => group.ToArray());
        _source = string.Concat("\0", string.Join('\0', names), "\0");
        _byOffset = new string?[_source.Length - names[^1].Length];
        _ranges = new (int Start, int Length)[MaxNameLength + 1];
        var start = 1;
        foreach (var name in names)
        {
            _byOffset[start] = name;
            ref var range = ref _ranges[name.Length];
            if (range.Length == 0) range.Start = start - 1;
            range.Length = start + name.Length - range.Start + 1;
            start += name.Length + 1;
        }

        string[] common = ["div", "class", "span", "a", "li", "href", "input", "type",
            "id", "body", "script", "table", "data-x", "custom-widget", "aria-label", "style"];
        var misses = names.SelectMany(name => new[]
        {
            "!" + name[1..], name[..^1] + "!", name + "!", "!" + name,
            name[..(name.Length / 2)] + "\u0100" + name[(name.Length / 2 + 1)..]
        }).Concat(["", new string('x', 65)]).ToArray();
        _inputs = Enumerable.Range(0, BatchSize).Select(i =>
        {
            var name = Distribution switch
            {
                "Mixed" => i % 7 == 0 ? names[i % names.Length] : common[i % common.Length],
                "AllKnown" => names[i % names.Length],
                "Misses" => misses[i % misses.Length],
                _ => throw new InvalidOperationException("Unknown input distribution.")
            };
            return "\0" + name + "\0";
        }).ToArray();
        var random = new Random(42);
        random.Shuffle(_inputs);
        foreach (var input in _inputs)
        {
            var span = input.AsSpan(1, input.Length - 2);
            var expected = LookupSequence(span);
            if (!ReferenceEquals(expected, LookupSubstring(input)) ||
                !ReferenceEquals(expected, HtmlKnownNames.Match(span)) ||
                !ReferenceEquals(expected, LookupDictionary(span)) ||
                !ReferenceEquals(expected, LookupFrozen(span)) ||
                !ReferenceEquals(expected, LookupHash(span)))
                throw new InvalidDataException("Known-name lookup results differ.");
        }
    }

    [Benchmark(Baseline = true, OperationsPerInvoke = BatchSize)]
    public int Substring()
    {
        var checksum = 0;
        foreach (var input in _inputs) checksum += LookupSubstring(input)?.Length ?? 0;
        return checksum;
    }

    [Benchmark(OperationsPerInvoke = BatchSize)]
    public int Generated()
    {
        var checksum = 0;
        foreach (var input in _inputs)
            checksum += HtmlKnownNames.Match(input.AsSpan(1, input.Length - 2))?.Length ?? 0;
        return checksum;
    }

    [Benchmark(OperationsPerInvoke = BatchSize)]
    public int SequenceEqual()
    {
        var checksum = 0;
        foreach (var input in _inputs)
            checksum += LookupSequence(input.AsSpan(1, input.Length - 2))?.Length ?? 0;
        return checksum;
    }

    [Benchmark(OperationsPerInvoke = BatchSize)]
    public int Dictionary()
    {
        var checksum = 0;
        foreach (var input in _inputs)
            checksum += LookupDictionary(input.AsSpan(1, input.Length - 2))?.Length ?? 0;
        return checksum;
    }

    [Benchmark(OperationsPerInvoke = BatchSize)]
    public int FrozenDictionary()
    {
        var checksum = 0;
        foreach (var input in _inputs)
            checksum += LookupFrozen(input.AsSpan(1, input.Length - 2))?.Length ?? 0;
        return checksum;
    }

    [Benchmark(OperationsPerInvoke = BatchSize)]
    public int XxHash3Dispatch()
    {
        var checksum = 0;
        foreach (var input in _inputs)
            checksum += LookupHash(input.AsSpan(1, input.Length - 2))?.Length ?? 0;
        return checksum;
    }

    private string? LookupSubstring(ReadOnlySpan<char> padded)
    {
        var length = padded.Length - 2;
        if ((uint) length > MaxNameLength) return null;
        var range = _ranges[length];
        var offset = _source.AsSpan(range.Start, range.Length).IndexOf(padded);
        return offset >= 0 ? _byOffset[range.Start + offset + 1] : null;
    }

    private string? LookupSequence(ReadOnlySpan<char> input)
    {
        if (input.Length > MaxNameLength) return null;
        foreach (var name in _byLength[input.Length])
            if (input.SequenceEqual(name)) return name;
        return null;
    }

    private string? LookupDictionary(ReadOnlySpan<char> input)
        => _dictionary.TryGetValue(input, out var name) ? name : null;

    private string? LookupFrozen(ReadOnlySpan<char> input)
        => _frozen.TryGetValue(input, out var name) ? name : null;

    private string? LookupHash(ReadOnlySpan<char> input)
    {
        if (_hash.TryGetValue(XxHash3.HashToUInt64(MemoryMarshal.AsBytes(input)), out var names))
            foreach (var name in names)
                if (input.SequenceEqual(name)) return name;
        return null;
    }
}
