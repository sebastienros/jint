using BenchmarkDotNet.Attributes;

namespace Jint.Benchmark;

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

    private Engine engine;

    [GlobalSetup]
    public void Setup()
    {
        foreach (var fileName in files.Keys.ToList())
        {
            files[fileName] = File.ReadAllText($"Scripts/{fileName}.js");
        }

        engine = new Engine()
            .SetValue("log", new Action<object>(Console.WriteLine))
            .SetValue("assert", new Action<bool>(b => { }));
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
        engine.Execute(files[FileName]);
    }
}
