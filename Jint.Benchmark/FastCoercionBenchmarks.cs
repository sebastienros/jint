using BenchmarkDotNet.Attributes;
using Jint.Native;

namespace Jint.Benchmark;

/// <summary>
/// Bench for built-in methods whose arguments are statically the right JsValue subtype already.
/// Gates [FastJsValue] (Phase 2c of the source-gen plan), which checks the InternalTypes flag
/// and skips TypeConverter.ToJsString/ToObject/ToNumber when the value is already that subtype.
///
/// Scenarios target the most common hits:
///   - String.prototype.indexOf with a string-literal needle (already JsString → no coercion needed)
///   - Map.prototype.get with a string key (already JsString)
///   - String.prototype.charCodeAt with a number (already JsNumber → ToInteger fast-path)
/// All three currently pay a TypeConverter call inside the dispatcher; [FastJsValue] would emit a
/// type-flag check + direct cast so the JsString-already case bypasses ToJsString entirely.
///
/// <para><b>Engine isolation.</b> Every row gets its own engine, warmed with its own script and nothing
/// else (see <see cref="IsolatedScript"/>). It used to be one engine warmed with all four scripts, so a
/// row was measured on an engine carrying the other three rows' handler-tree entries and call-site caches
/// (plus <c>Warm_MapGet</c>'s global <c>m</c>) — and since the class is precisely about what a call site
/// has learned about its argument types, sibling state on those sites is the wrong thing to carry. The
/// rows still measure warm dispatch, and engine construction and warm-up stay in <c>[GlobalSetup]</c>,
/// outside the measurement. <b>Numbers from this class are not comparable to any published before the
/// harness changed.</b></para>
/// </summary>
[MemoryDiagnoser]
public class FastCoercionBenchmarks
{
    private IsolatedScript _stringIndexOf;
    private IsolatedScript _mapGet;
    private IsolatedScript _charCodeAt;
    private IsolatedScript _stringIncludes;

    [GlobalSetup]
    public void GlobalSetup()
    {
        // Each script keeps the call site monomorphic on a JsString needle / key.
        _stringIndexOf  = IsolatedScript.Warm("'hello world foo bar baz'.indexOf('foo')");
        _mapGet         = IsolatedScript.Warm("var m = new Map(); m.set('a', 1); m.set('b', 2); m.get('b')");
        _charCodeAt     = IsolatedScript.Warm("'abcdefg'.charCodeAt(3)");
        _stringIncludes = IsolatedScript.Warm("'hello world foo bar baz'.includes('bar')");
    }

    [Benchmark] public JsValue Warm_StringIndexOf() => _stringIndexOf.Run();
    [Benchmark] public JsValue Warm_MapGet() => _mapGet.Run();
    [Benchmark] public JsValue Warm_CharCodeAt() => _charCodeAt.Run();
    [Benchmark] public JsValue Warm_StringIncludes() => _stringIncludes.Run();
}
