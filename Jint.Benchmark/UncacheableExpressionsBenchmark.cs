using System.Text;
using System.Text.Json;
using BenchmarkDotNet.Attributes;
using Jint.Native;

namespace Jint.Benchmark;

/// <summary>
/// Test case for situation where object is projected via filter and map, Jint deems code as uncacheable.
/// </summary>
[MemoryDiagnoser]
public class UncacheableExpressionsBenchmark
{
    private Document doc;

    private string targetObject;
    private JsValue[] targetJsObject;

    private const string NonArrowFunctionScript = @"
function output(d) {
    var doc = d.SubDocuments.find(function(x){return x.Id==='testing';});
    return { Id : d.Id, Deleted : d.Deleted, SubTestId : (doc!==null&&doc!==undefined)?doc.Id:null, Values : d.SubDocuments.map(function(x){return {TargetId:x.TargetId,TargetValue:x.TargetValue,SubDocuments:x.SubDocuments.filter(function(s){return (s!==null&&s!==undefined);}).map(function(s){return {TargetId:s.TargetId,TargetValue:s.TargetValue};})};}) };
}
";

    private const string ArrowFunctionScript = @"
function output(d) {
    let doc = d.SubDocuments.find(x => x.Id==='testing');
    return { Id : d.Id, Deleted : d.Deleted, SubTestId : (doc!==null&&doc!==undefined)?doc.Id:null, Values : d.SubDocuments.map(x => ({ TargetId:x.TargetId,TargetValue:x.TargetValue,SubDocuments:x.SubDocuments.filter(s => (s!==null&&s!==undefined)).map(s => ({ TargetId: s.TargetId, TargetValue: s.TargetValue}))})) };
}
";

    private Engine engine;

    public class Document
    {
        public string Id { get; set; }
        public string TargetId { get; set; }
        public decimal TargetValue { get; set; }
        public bool Deleted { get; set; }
        public IEnumerable<Document> SubDocuments { get; set; }
    }

    [GlobalSetup]
    public void Setup()
    {
        doc = new Document
        {
            Deleted = false,
            SubDocuments = new List<Document>
            {
                new Document
                {
                    TargetId = "id1",
                    SubDocuments = Enumerable.Range(1, 200).Select(x => new Document()).ToList()
                },
                new Document
                {
                    TargetId = "id2",
                    SubDocuments = Enumerable.Range(1, 200).Select(x => new Document()).ToList()
                }
            }
        };

        using (var stream = new MemoryStream())
        {
            JsonSerializer.Serialize(stream, doc);

            var targetObjectJson = Encoding.UTF8.GetString(stream.ToArray());
            targetObject = $"var d = {targetObjectJson};";
        }

        CreateEngine(Arrow ? ArrowFunctionScript : NonArrowFunctionScript);
    }

    private static void InitializeEngine(Options options)
    {
        options
            .LimitRecursion(64)
            .MaxStatements(int.MaxValue)
            .Strict()
            .LocalTimeZone(TimeZoneInfo.Utc);
    }

    [Params(500)]
    public int N { get; set; }

    [Params(true, false)]
    public bool Arrow { get; set; }

    [Benchmark]
    public void Benchmark()
    {
        var call = engine.GetValue("output").TryCast<ICallable>();
        for (int i = 0; i < N; ++i)
        {
            call.Call(JsValue.Undefined, targetJsObject);
        }
    }

    private void CreateEngine(string script)
    {
        engine = new Engine(InitializeEngine);
        engine.Execute(script);
        engine.Execute(targetObject);
        targetJsObject = new[] { engine.GetValue("d") };
    }
}
