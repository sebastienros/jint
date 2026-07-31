using BenchmarkDotNet.Attributes;

namespace Jint.Benchmark;

/// <summary>
/// The SunSpider suite, one row per script.
///
/// <para><b>Engine isolation.</b> The engine is built inside the benchmark method, so each op runs its
/// script on an engine that has never seen anything else — the same choice, and for the same reasons,
/// as <see cref="DromaeoBenchmark"/>. This class used to build one engine in <c>[GlobalSetup]</c> and
/// execute the row's script on it over and over. That is a shared-state measurement even though only
/// one script per row ever touches it: the source is re-parsed on every op, so every op adds a fresh
/// set of AST nodes to the engine-owned handler-tree caches (<c>Engine._functionDefinitions</c>,
/// <c>Engine._scriptStatementLists</c>) that nothing ever evicts, on top of the globals each re-run
/// redeclares. Per-op cost therefore drifted with the invocation index, and the invocation count is
/// picked by a pilot whose answer depends on the very change being measured — so a row could move
/// several percent, in either direction, on a change that could not have touched it.</para>
///
/// <para>Engine construction now counts toward the measurement: roughly 0.1-0.3 ms against ops that run
/// from a few ms to tens of ms. It is constant across revisions, so A/B comparisons stay valid, but it
/// is a larger share of the shortest rows (the <c>bitops-*</c> family) than of the longest.
/// Deliberately <em>not</em> an <c>[IterationSetup]</c>: that forces <c>InvocationCount=1</c>, which
/// leaks tiered-JIT warmup into the measured iterations — see the comment in
/// <see cref="DromaeoBenchmark"/> for the full account. <b>Numbers from this class are not comparable
/// to any published before the harness changed.</b></para>
/// </summary>
[MemoryDiagnoser]
public class SunSpiderBenchmark
{
    private static readonly Dictionary<string, string> files = new()
    {
        {"3d-cube", null},
        {"3d-morph", null},
        {"3d-raytrace", null},
        {"access-binary-trees", null},
        {"access-fannkuch", null},
        {"access-nbody", null},
        {"access-nsieve", null},
        {"bitops-3bit-bits-in-byte", null},
        {"bitops-bits-in-byte", null},
        {"bitops-bitwise-and", null},
        {"bitops-nsieve-bits", null},
        {"controlflow-recursive", null},
        {"crypto-aes", null},
        {"crypto-md5", null},
        {"crypto-sha1", null},
        {"date-format-tofte", null},
        {"date-format-xparb", null},
        {"math-cordic", null},
        {"math-partial-sums", null},
        {"math-spectral-norm", null},
        {"regexp-dna", null},
        {"string-base64", null},
        {"string-fasta", null},
        {"string-tagcloud", null},
        {"string-unpack-code", null},
        {"string-validate-input", null}
    };

    [GlobalSetup]
    public void Setup()
    {
        foreach (var fileName in files.Keys.ToList())
        {
            files[fileName] = File.ReadAllText($"Scripts/{fileName}.js");
        }
    }

    [ParamsSource(nameof(FileNames))]
    public string FileName { get; set; }

    public IEnumerable<string> FileNames()
    {
        foreach (var entry in files)
        {
            yield return entry.Key;
        }
    }

    [Benchmark]
    public void Run()
    {
        var engine = new Engine()
            .SetValue("log", new Action<object>(Console.WriteLine))
            .SetValue("assert", new Action<bool>(b => { }));

        engine.Execute(files[FileName]);
    }
}
