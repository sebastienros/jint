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
/// </summary>
[ShortRunJob]
[MemoryDiagnoser]
public class FastCoercionBenchmarks
{
    private Engine _warm = null!;
    private Prepared<Script> _stringIndexOf;
    private Prepared<Script> _mapGet;
    private Prepared<Script> _charCodeAt;
    private Prepared<Script> _stringIncludes;

    [GlobalSetup]
    public void GlobalSetup()
    {
        // Each script keeps the call site monomorphic on a JsString needle / key.
        _stringIndexOf  = Engine.PrepareScript("'hello world foo bar baz'.indexOf('foo')");
        _mapGet         = Engine.PrepareScript("var m = new Map(); m.set('a', 1); m.set('b', 2); m.get('b')");
        _charCodeAt     = Engine.PrepareScript("'abcdefg'.charCodeAt(3)");
        _stringIncludes = Engine.PrepareScript("'hello world foo bar baz'.includes('bar')");

        _warm = new Engine();
        _warm.Evaluate(_stringIndexOf);
        _warm.Evaluate(_mapGet);
        _warm.Evaluate(_charCodeAt);
        _warm.Evaluate(_stringIncludes);
    }

    [Benchmark] public JsValue Warm_StringIndexOf() => _warm.Evaluate(_stringIndexOf);
    [Benchmark] public JsValue Warm_MapGet() => _warm.Evaluate(_mapGet);
    [Benchmark] public JsValue Warm_CharCodeAt() => _warm.Evaluate(_charCodeAt);
    [Benchmark] public JsValue Warm_StringIncludes() => _warm.Evaluate(_stringIncludes);
}
