using System.Text;
using BenchmarkDotNet.Attributes;
using Jint.Native;

namespace Jint.Benchmark;

/// <summary>
/// Measures the JS-level <c>JSON.parse</c>/<c>JSON.stringify</c> API over the payload shapes real
/// embeddings feed through scripts: a homogeneous array of records (the dominant API-payload shape)
/// and a heterogeneous nested config object. Unlike <see cref="JsonBenchmark"/> (which drives the
/// internal C# JsonParser/JsonSerializer over fixtures downloaded from the network), the payloads
/// here are generated deterministically offline, so rows are stable gates. One parse or stringify
/// per op — the Allocated column is the per-document allocation cost.
/// </summary>
[MemoryDiagnoser]
[HideColumns("Error", "Gen0", "Gen1", "Gen2")]
public class JsonJsBenchmark
{
    private Engine _engine = null!;
    private Prepared<Script> _parseRecords;
    private Prepared<Script> _parseConfig;
    private Prepared<Script> _parseBigObject;
    private Prepared<Script> _stringifyRecords;
    private Prepared<Script> _stringifyConfig;
    private Prepared<Script> _roundTripRecords;

    internal const string ParseRecordsSource = "JSON.parse(recordsJson);";
    internal const string ParseConfigSource = "JSON.parse(configJson);";
    internal const string ParseBigObjectSource = "JSON.parse(bigObjectJson);";
    internal const string StringifyRecordsSource = "JSON.stringify(records);";
    internal const string StringifyConfigSource = "JSON.stringify(config);";
    internal const string RoundTripRecordsSource = "JSON.parse(JSON.stringify(records));";
    internal const string SetupSource = "var records = JSON.parse(recordsJson); var config = JSON.parse(configJson);";

    /// <summary>1,000 records × 6 mixed-type properties (~90 KB) — the array-of-identical-records shape.</summary>
    internal static string BuildRecordsJson()
    {
        var random = new Random(42);
        string[] tags = ["alpha", "beta", "gamma", "delta", "epsilon", "zeta", "eta", "theta"];
        var sb = new StringBuilder(96 * 1024);
        sb.Append('[');
        for (var i = 0; i < 1_000; i++)
        {
            if (i > 0)
            {
                sb.Append(',');
            }

            sb.Append("{\"id\":").Append(i)
                .Append(",\"name\":\"user").Append(random.Next(10_000))
                .Append("\",\"active\":").Append(random.Next(2) == 0 ? "false" : "true")
                .Append(",\"score\":").Append(random.Next(1_000)).Append('.').Append(random.Next(10))
                .Append(",\"tags\":[\"").Append(tags[random.Next(tags.Length)]).Append("\",\"").Append(tags[random.Next(tags.Length)])
                .Append("\"],\"ts\":").Append(1_700_000_000L + i)
                .Append('}');
        }

        sb.Append(']');
        return sb.ToString();
    }

    /// <summary>Nested config: depth 4, fan-out 4 (340 objects), leaves cycling string/number/bool/null.</summary>
    internal static string BuildConfigJson()
    {
        var random = new Random(42);
        var sb = new StringBuilder(16 * 1024);
        AppendLevel(sb, random, depth: 4);
        return sb.ToString();

        static void AppendLevel(StringBuilder sb, Random random, int depth)
        {
            sb.Append('{');
            for (var i = 0; i < 4; i++)
            {
                if (i > 0)
                {
                    sb.Append(',');
                }

                sb.Append("\"node").Append((char) ('A' + i)).Append(depth).Append("\":");
                if (depth > 0)
                {
                    AppendLevel(sb, random, depth - 1);
                }
                else
                {
                    switch (i % 4)
                    {
                        case 0:
                            sb.Append("\"value").Append(random.Next(100)).Append('"');
                            break;
                        case 1:
                            sb.Append(random.Next(100_000));
                            break;
                        case 2:
                            sb.Append(random.Next(2) == 0 ? "false" : "true");
                            break;
                        default:
                            sb.Append("null");
                            break;
                    }
                }
            }

            sb.Append('}');
        }
    }

    /// <summary>One object with 100 properties — trips the 64-property shape guard mid-build.</summary>
    internal static string BuildBigObjectJson()
    {
        var random = new Random(42);
        var sb = new StringBuilder(4 * 1024);
        sb.Append('{');
        for (var i = 0; i < 100; i++)
        {
            if (i > 0)
            {
                sb.Append(',');
            }

            sb.Append("\"prop").Append(i).Append("\":").Append(random.Next(1_000));
        }

        sb.Append('}');
        return sb.ToString();
    }

    [GlobalSetup]
    public void Setup()
    {
        _engine = new Engine();
        _engine.SetValue("recordsJson", BuildRecordsJson());
        _engine.SetValue("configJson", BuildConfigJson());
        _engine.SetValue("bigObjectJson", BuildBigObjectJson());
        _engine.Execute(SetupSource);

        _parseRecords = Engine.PrepareScript(ParseRecordsSource);
        _parseConfig = Engine.PrepareScript(ParseConfigSource);
        _parseBigObject = Engine.PrepareScript(ParseBigObjectSource);
        _stringifyRecords = Engine.PrepareScript(StringifyRecordsSource);
        _stringifyConfig = Engine.PrepareScript(StringifyConfigSource);
        _roundTripRecords = Engine.PrepareScript(RoundTripRecordsSource);

        _engine.Evaluate(_parseRecords);
        _engine.Evaluate(_parseConfig);
        _engine.Evaluate(_parseBigObject);
        _engine.Evaluate(_stringifyRecords);
        _engine.Evaluate(_stringifyConfig);
        _engine.Evaluate(_roundTripRecords);
    }

    [Benchmark]
    public JsValue ParseRecords() => _engine.Evaluate(_parseRecords);

    [Benchmark]
    public JsValue ParseConfig() => _engine.Evaluate(_parseConfig);

    [Benchmark]
    public JsValue ParseBigObject() => _engine.Evaluate(_parseBigObject);

    [Benchmark]
    public JsValue StringifyRecords() => _engine.Evaluate(_stringifyRecords);

    [Benchmark]
    public JsValue StringifyConfig() => _engine.Evaluate(_stringifyConfig);

    [Benchmark]
    public JsValue RoundTripRecords() => _engine.Evaluate(_roundTripRecords);
}
