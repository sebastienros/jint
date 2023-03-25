using BenchmarkDotNet.Attributes;
using Jint.Native;
using Jint.Native.Json;

namespace Jint.Benchmark;

[MemoryDiagnoser]
public class JsonBenchmark
{
    private Engine _engine;

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
        _engine = new Engine();

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

            var parser = new JsonParser(_engine);
            _parsedInstance[source.Key] = parser.Parse(json);
        }
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
        var parser = new JsonParser(_engine);
        return parser.Parse(_json[FileName]);
    }

    [Benchmark]
    public JsValue Stringify()
    {
        var serializer = new JsonSerializer(_engine);
        return serializer.Serialize(_parsedInstance[FileName]);
    }
}
