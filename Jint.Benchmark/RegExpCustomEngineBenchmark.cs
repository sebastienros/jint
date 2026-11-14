using BenchmarkDotNet.Attributes;
using Jint.Runtime.RegExp;
using Jint.Runtime.RegExp.GregsEx;

namespace Jint.Benchmark;

/// <summary>
/// Benchmarks of Jint's regex engine called directly, below the RegExp built-ins: compiled once, matched against a
/// ~65K random lowercase subject. The /u flag is kept from when it was what routed a pattern to this engine rather
/// than to .NET Regex; every pattern now runs here. Matches report capture spans into a reused buffer, which is the
/// entry point the built-ins use (the QuickJS port this replaced materialized every group as a string instead).
/// </summary>
[MemoryDiagnoser]
public class RegExpCustomEngineBenchmark
{
    private string _input = "";
    private readonly RegexCapture[] _groups = new RegexCapture[8];

    private bool Find(JintRegExpEngine engine, int start) => engine.TryMatch(_input, start, JintRegExpEngine.NoDeadline, _groups);

    private JintRegExpEngine _literalEngine = null!;
    private JintRegExpEngine _variableLengthEngine = null!;
    private JintRegExpEngine _namedCaptureEngine = null!;
    private JintRegExpEngine _noMatchEngine = null!;
    private JintRegExpEngine _caseInsensitiveEngine = null!;
    private JintRegExpEngine _globalLiteralEngine = null!;
    private JintRegExpEngine _anchoredEngine = null!;
    private JintRegExpEngine _digitScanEngine = null!;
    private JintRegExpEngine _charClassScanEngine = null!;

    [GlobalSetup]
    public void Setup()
    {
        // Generate ~65K random lowercase string matching dromaeo's approach
        var random = new Random(42); // fixed seed for reproducibility
        var chars = new char[16384];
        for (int i = 0; i < chars.Length; i++)
        {
            chars[i] = (char) ('a' + random.Next(26));
        }

        var str = new string(chars);
        _input = str + str + str + str; // ~65K chars

        // Pre-compile engines (isolate execution cost from compilation)
        _literalEngine = JintRegExpEngine.Compile("aaaaaaaaaa", RegExpFlags.Unicode);
        _variableLengthEngine = JintRegExpEngine.Compile("a.*a", RegExpFlags.Unicode);
        _namedCaptureEngine = JintRegExpEngine.Compile("aa(?<cap>b)aa", RegExpFlags.Unicode | RegExpFlags.Global);
        _noMatchEngine = JintRegExpEngine.Compile("zzzzz", RegExpFlags.Unicode);
        _caseInsensitiveEngine = JintRegExpEngine.Compile("aaaaaaaaaa", RegExpFlags.Unicode | RegExpFlags.IgnoreCase);
        _globalLiteralEngine = JintRegExpEngine.Compile("aaaaaaaaaa", RegExpFlags.Unicode | RegExpFlags.Global);
        _anchoredEngine = JintRegExpEngine.Compile("^aaaaaaaaaa", RegExpFlags.Unicode);
        _digitScanEngine = JintRegExpEngine.Compile("[0-9]+", RegExpFlags.Unicode);
        _charClassScanEngine = JintRegExpEngine.Compile("[a-z]+", RegExpFlags.Unicode);
    }

    [Benchmark]
    public bool LiteralScan()
    {
        return Find(_literalEngine, 0);
    }

    [Benchmark]
    public bool VariableLength()
    {
        return Find(_variableLengthEngine, 0);
    }

    [Benchmark]
    public bool NamedCapture()
    {
        return Find(_namedCaptureEngine, 0);
    }

    [Benchmark]
    public bool NoMatchScan()
    {
        return Find(_noMatchEngine, 0);
    }

    [Benchmark]
    public bool IsMatchOnly()
    {
        return _literalEngine.IsMatch(_input, 0);
    }

    [Benchmark]
    public bool CaseInsensitiveScan()
    {
        return Find(_caseInsensitiveEngine, 0);
    }

    [Benchmark]
    public int GlobalMatchAll()
    {
        int count = 0;
        int lastIndex = 0;
        while (lastIndex <= _input.Length)
        {
            if (!Find(_globalLiteralEngine, lastIndex))
            {
                break;
            }

            count++;
            lastIndex = _groups[0].Index + Math.Max(_groups[0].Length, 1);
        }

        return count;
    }

    [Benchmark]
    public bool AnchoredLiteral()
    {
        return Find(_anchoredEngine, 0);
    }

    [Benchmark]
    public bool DigitScan()
    {
        return Find(_digitScanEngine, 0);
    }

    [Benchmark]
    public bool CharClassScan()
    {
        return Find(_charClassScanEngine, 0);
    }
}
