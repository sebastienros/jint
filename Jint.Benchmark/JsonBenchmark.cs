using BenchmarkDotNet.Attributes;
using Jint.Native;
using Jint.Native.Json;

namespace Jint.Benchmark;

/// <summary>
/// Drives the internal C# <see cref="JsonParser"/> / <see cref="JsonSerializer"/> over two real-world
/// documents downloaded once into the temp directory.
///
/// <para><b>Engine isolation.</b> Each row gets its own engine, and only the current
/// <see cref="FileName"/>'s document is parsed onto one. It used to be a single engine on which
/// <c>[GlobalSetup]</c> parsed <em>both</em> documents — the exact work <see cref="Parse"/> measures —
/// and retained both graphs for the lifetime of the run, so the <c>Parse</c> row was measured against
/// the <c>Stringify</c> row's fixture (and against the other parameter's fixture as well), with the
/// resulting heap pressure folded into its number. <c>Stringify</c> still gets the parsed document it
/// needs, on its own engine; <c>Parse</c> gets an engine warmed with one parse of its own document,
/// which is then discarded rather than retained.</para>
/// </summary>
[MemoryDiagnoser]
public class JsonBenchmark
{
    private Engine _parseEngine;
    private Engine _stringifyEngine;

    private readonly Dictionary<string, string> _sources = new()
    {
        { "twitter.json", "https://raw.githubusercontent.com/miloyip/nativejson-benchmark/master/data/twitter.json" },
        { "bestbuy_dataset.json", "https://github.com/algolia/examples/raw/master/instant-search/instantsearch.js/dataset_import/bestbuy_dataset.json" },
    };

    private readonly Dictionary<string, JsValue> _parsedInstance = new();
    private readonly Dictionary<string, string> _json = new();

    [GlobalSetup]
    public async Task GlobalSetup()
    {
        _parseEngine = new Engine();
        _stringifyEngine = new Engine();

        foreach (var source in _sources)
        {
            var filePath = Path.Combine(Path.GetTempPath(), source.Key);
            if (!File.Exists(filePath))
            {
                using var client = new HttpClient();
                using var response = await client.GetAsync(source.Value);
                await using var streamToReadFrom = await response.Content.ReadAsStreamAsync();
                await using var streamToWriteTo = File.OpenWrite(filePath);
                await streamToReadFrom.CopyToAsync(streamToWriteTo);
            }

            var json = await File.ReadAllTextAsync(filePath);
            _json[source.Key] = json;
        }

        // The Stringify row's fixture, on the Stringify row's engine — only for the document this case
        // is parameterized on, so the other document's graph is never resident.
        _parsedInstance[FileName] = new JsonParser(_stringifyEngine).Parse(_json[FileName]);

        // Warm the Parse row's own engine with its own work; the result is discarded rather than
        // retained, so the row is not measured against a graph it did not build.
        new JsonParser(_parseEngine).Parse(_json[FileName]);
    }

    public IEnumerable<string> FileNames()
    {
        foreach (var entry in _sources)
        {
            yield return entry.Key;
        }
    }

    [ParamsSource(nameof(FileNames))]
    public string FileName { get; set; }

    [Benchmark]
    public JsValue Parse()
    {
        var parser = new JsonParser(_parseEngine);
        return parser.Parse(_json[FileName]);
    }

    [Benchmark]
    public JsValue Stringify()
    {
        var serializer = new JsonSerializer(_stringifyEngine);
        return serializer.Serialize(_parsedInstance[FileName]);
    }
}
