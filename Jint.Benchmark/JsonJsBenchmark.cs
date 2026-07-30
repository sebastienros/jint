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
///
/// <para><b>Engine isolation.</b> Every row gets its own engine — built by <see cref="CreateEngine"/>,
/// which re-runs <see cref="SetupSource"/> so each engine owns its own <c>records</c>/<c>config</c>
/// graphs — and warmed with its own script and nothing else (see <see cref="IsolatedScript"/>). It used
/// to be one engine warmed with all six row scripts, so each row was measured on an engine carrying the
/// other five rows' handler-tree, call-site and object-shape state, plus the retained result graphs their
/// warm-up left behind. The rows still measure warm parse/stringify, and engine construction and warm-up
/// stay in <c>[GlobalSetup]</c>, outside the measurement. <b>Numbers from this class are not comparable
/// to any published before the harness changed.</b></para>
/// </summary>
[MemoryDiagnoser]
[HideColumns("Error", "Gen0", "Gen1", "Gen2")]
public class JsonJsBenchmark
{
    private string _recordsJson = null!;
    private string _configJson = null!;
    private string _bigObjectJson = null!;
    private IsolatedScript _parseRecords;
    private IsolatedScript _parseConfig;
    private IsolatedScript _parseBigObject;
    private IsolatedScript _stringifyRecords;
    private IsolatedScript _stringifyConfig;
    private IsolatedScript _roundTripRecords;

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

    /// <summary>
    /// Builds a fresh engine carrying the fixture every row needs, and nothing else. The three payloads
    /// are generated once in <see cref="Setup"/> and handed to every engine, so the deterministic
    /// builders stay a one-off setup cost rather than running once per row.
    /// </summary>
    private Engine CreateEngine()
    {
        var engine = new Engine();
        engine.SetValue("recordsJson", _recordsJson);
        engine.SetValue("configJson", _configJson);
        engine.SetValue("bigObjectJson", _bigObjectJson);
        engine.Execute(SetupSource);
        return engine;
    }

    [GlobalSetup]
    public void Setup()
    {
        _recordsJson = BuildRecordsJson();
        _configJson = BuildConfigJson();
        _bigObjectJson = BuildBigObjectJson();

        _parseRecords = IsolatedScript.Warm(Engine.PrepareScript(ParseRecordsSource), CreateEngine);
        _parseConfig = IsolatedScript.Warm(Engine.PrepareScript(ParseConfigSource), CreateEngine);
        _parseBigObject = IsolatedScript.Warm(Engine.PrepareScript(ParseBigObjectSource), CreateEngine);
        _stringifyRecords = IsolatedScript.Warm(Engine.PrepareScript(StringifyRecordsSource), CreateEngine);
        _stringifyConfig = IsolatedScript.Warm(Engine.PrepareScript(StringifyConfigSource), CreateEngine);
        _roundTripRecords = IsolatedScript.Warm(Engine.PrepareScript(RoundTripRecordsSource), CreateEngine);
    }

    [Benchmark]
    public JsValue ParseRecords() => _parseRecords.Run();

    [Benchmark]
    public JsValue ParseConfig() => _parseConfig.Run();

    [Benchmark]
    public JsValue ParseBigObject() => _parseBigObject.Run();

    [Benchmark]
    public JsValue StringifyRecords() => _stringifyRecords.Run();

    [Benchmark]
    public JsValue StringifyConfig() => _stringifyConfig.Run();

    [Benchmark]
    public JsValue RoundTripRecords() => _roundTripRecords.Run();
}
