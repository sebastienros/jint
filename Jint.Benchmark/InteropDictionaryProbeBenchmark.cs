#nullable enable

using BenchmarkDotNet.Attributes;
using Jint.Native;

namespace Jint.Benchmark;

/// <summary>
/// What a script asks a wrapped host document <em>about</em> its keys rather than for their values —
/// <c>k in doc</c>, <c>doc.hasOwnProperty(k)</c>, <c>Object.keys(doc)</c> and <c>for..in</c> — which is
/// the shape of every "does this payload have the field" guard an embedder's script runs before reading
/// it. <see cref="InteropNestedDictionaryBenchmark"/> and <see cref="ImmutableCrossingBenchmark"/> only
/// walk values, so nothing measured the existence side.
///
/// <para>
/// Why it is its own class rather than rows on either of those: a dictionary member is the one thing an
/// <c>ObjectWrapper</c> never caches (the target can change under it), so every one of these questions
/// re-runs the read — <c>TryGetValue</c>, then a conversion of the value, then a
/// <c>PropertyDescriptor</c> around it — and throws both away. The document below deliberately mixes
/// scalar and object values, because an object value is what turns that discarded conversion into a
/// whole nested wrapper: the <c>Allocated</c> column is where that shows.
/// </para>
///
/// <para>
/// <b>Reading the rows.</b> <c>Values</c> and <c>ReadMember</c> are the controls. Both need the value,
/// so neither can be served by an existence answer, and a change on either is environment drift rather
/// than a result. <c>InMiss</c> is the other control worth watching: a name the dictionary does not carry
/// still has to fall through to CLR member resolution, so it can only get slower, and by how much is the
/// cost of asking.
/// </para>
///
/// <para><b>Engine isolation.</b> Every row gets its own engine, built by <c>CreateEngine</c> and warmed
/// with that row's script and nothing else (see <see cref="IsolatedScript"/>), so no row is measured on an
/// engine carrying a sibling's globals, handler-tree entries or call-site caches.</para>
/// </summary>
[MemoryDiagnoser]
[HideColumns("Error", "Gen0", "Gen1", "Gen2")]
public class InteropDictionaryProbeBenchmark
{
    private const int Iterations = 1_000;

    private IsolatedScript _inHit;
    private IsolatedScript _inMiss;
    private IsolatedScript _hasOwnProperty;
    private IsolatedScript _objectKeys;
    private IsolatedScript _forIn;
    private IsolatedScript _objectValues;
    private IsolatedScript _readMember;

    /// <summary>
    /// A JSON-document shape: eight scalar leaves and four object nodes. The object nodes are the point —
    /// converting one to answer a yes/no question builds a wrapper that is then discarded.
    /// </summary>
    private static Dictionary<string, object> BuildDocument()
    {
        var document = new Dictionary<string, object>(StringComparer.Ordinal);
        for (var i = 0; i < 8; i++)
        {
            document["s" + i.ToString(System.Globalization.CultureInfo.InvariantCulture)] = i % 2 == 0 ? i : "v" + i.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        for (var i = 0; i < 4; i++)
        {
            document["o" + i.ToString(System.Globalization.CultureInfo.InvariantCulture)] =
                new Dictionary<string, object>(StringComparer.Ordinal) { ["deep"] = i };
        }

        return document;
    }

    private static Engine CreateEngine()
    {
        var engine = new Engine();
        engine.SetValue("doc", BuildDocument());
        return engine;
    }

    [GlobalSetup]
    public void GlobalSetup()
    {
        _inHit = IsolatedScript.Warm($"(function () {{ var s = 0; for (var i = 0; i < {Iterations}; i++) {{ if ('o2' in doc) s++; }} return s; }})();", CreateEngine);
        _inMiss = IsolatedScript.Warm($"(function () {{ var s = 0; for (var i = 0; i < {Iterations}; i++) {{ if ('nope' in doc) s++; }} return s; }})();", CreateEngine);
        _hasOwnProperty = IsolatedScript.Warm($"(function () {{ var s = 0; for (var i = 0; i < {Iterations}; i++) {{ if (doc.hasOwnProperty('o2')) s++; }} return s; }})();", CreateEngine);
        _objectKeys = IsolatedScript.Warm($"(function () {{ var s = 0; for (var i = 0; i < {Iterations}; i++) {{ s += Object.keys(doc).length; }} return s; }})();", CreateEngine);
        _forIn = IsolatedScript.Warm($"(function () {{ var s = 0; for (var i = 0; i < {Iterations}; i++) {{ for (var k in doc) s++; }} return s; }})();", CreateEngine);
        _objectValues = IsolatedScript.Warm($"(function () {{ var s = 0; for (var i = 0; i < {Iterations}; i++) {{ s += Object.values(doc).length; }} return s; }})();", CreateEngine);
        _readMember = IsolatedScript.Warm($"(function () {{ var s = 0; for (var i = 0; i < {Iterations}; i++) {{ s += doc.o2.deep; }} return s; }})();", CreateEngine);
    }

    [Benchmark]
    public JsValue InHit() => _inHit.Run();

    [Benchmark]
    public JsValue InMiss() => _inMiss.Run();

    [Benchmark]
    public JsValue HasOwnProperty() => _hasOwnProperty.Run();

    [Benchmark]
    public JsValue ObjectKeys() => _objectKeys.Run();

    [Benchmark]
    public JsValue ForIn() => _forIn.Run();

    /// <summary>Control: needs every value, so no existence answer can serve it.</summary>
    [Benchmark]
    public JsValue ObjectValues() => _objectValues.Run();

    /// <summary>Control: a plain member read, untouched by anything the probe does.</summary>
    [Benchmark]
    public JsValue ReadMember() => _readMember.Run();
}
