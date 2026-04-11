using BenchmarkDotNet.Attributes;
using Jint.Runtime.RegExp;

namespace Jint.Benchmark;

/// <summary>
/// Benchmarks targeting the custom regex engine (QuickJS libregexp port).
/// All patterns use the /u flag to force routing through the custom engine
/// instead of .NET Regex.
/// </summary>
[MemoryDiagnoser]
public class RegExpCustomEngineBenchmark
{
    private string _input = "";

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
        return _literalEngine.Execute(_input, 0).Success;
    }

    [Benchmark]
    public bool VariableLength()
    {
        return _variableLengthEngine.Execute(_input, 0).Success;
    }

    [Benchmark]
    public bool NamedCapture()
    {
        return _namedCaptureEngine.Execute(_input, 0).Success;
    }

    [Benchmark]
    public bool NoMatchScan()
    {
        return _noMatchEngine.Execute(_input, 0).Success;
    }

    [Benchmark]
    public bool IsMatchOnly()
    {
        return _literalEngine.IsMatch(_input, 0);
    }

    [Benchmark]
    public bool CaseInsensitiveScan()
    {
        return _caseInsensitiveEngine.Execute(_input, 0).Success;
    }

    [Benchmark]
    public int GlobalMatchAll()
    {
        int count = 0;
        int lastIndex = 0;
        while (lastIndex <= _input.Length)
        {
            var result = _globalLiteralEngine.Execute(_input, lastIndex);
            if (!result.Success)
            {
                break;
            }

            count++;
            lastIndex = result.Index + Math.Max(result.Length, 1);
        }

        return count;
    }

    [Benchmark]
    public bool AnchoredLiteral()
    {
        return _anchoredEngine.Execute(_input, 0).Success;
    }

    [Benchmark]
    public bool DigitScan()
    {
        return _digitScanEngine.Execute(_input, 0).Success;
    }

    [Benchmark]
    public bool CharClassScan()
    {
        return _charClassScanEngine.Execute(_input, 0).Success;
    }
}
