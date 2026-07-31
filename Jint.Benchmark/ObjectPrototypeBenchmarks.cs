using BenchmarkDotNet.Attributes;
using Jint.Native;

namespace Jint.Benchmark;

/// <summary>
/// Source-gen sentinel for Object.prototype.{toString,hasOwnProperty,valueOf} and the __proto__
/// accessor. Post-source-gen the prototype uses [JsFunction] for the methods and [JsAccessor] for
/// __proto__'s get/set pair. Warm-path numbers should match the pre-source-gen baseline.
///
/// <para><b>Engine isolation.</b> Every row gets its own engine, warmed with its own script and nothing
/// else (see <see cref="IsolatedScript"/>). It used to be one shared <c>_warm</c> engine warmed with all
/// five scripts, so each row was measured on an engine already carrying the other rows' handler-tree and
/// call-site state. The rows still measure warm dispatch, and engine construction and warm-up stay in
/// <c>[GlobalSetup]</c>, outside the measurement. <b>Numbers from this class are not comparable to any
/// published before the harness changed.</b></para>
/// </summary>
[MemoryDiagnoser]
public class ObjectPrototypeBenchmarks
{
    private IsolatedScript _toString;
    private IsolatedScript _hasOwn;
    private IsolatedScript _isPrototypeOf;
    private IsolatedScript _protoGet;
    private IsolatedScript _propertyIsEnumerable;

    [GlobalSetup]
    public void GlobalSetup()
    {
        _toString             = IsolatedScript.Warm("({a:1}).toString()");
        _hasOwn               = IsolatedScript.Warm("({a:1}).hasOwnProperty('a')");
        _isPrototypeOf        = IsolatedScript.Warm("Object.prototype.isPrototypeOf({a:1})");
        _protoGet             = IsolatedScript.Warm("({a:1}).__proto__");
        _propertyIsEnumerable = IsolatedScript.Warm("({a:1}).propertyIsEnumerable('a')");
    }

    [Benchmark]
    public JsValue Warm_ToString() => _toString.Run();

    [Benchmark]
    public JsValue Warm_HasOwnProperty() => _hasOwn.Run();

    [Benchmark]
    public JsValue Warm_IsPrototypeOf() => _isPrototypeOf.Run();

    [Benchmark]
    public JsValue Warm_ProtoGet() => _protoGet.Run();

    [Benchmark]
    public JsValue Warm_PropertyIsEnumerable() => _propertyIsEnumerable.Run();
}
