using BenchmarkDotNet.Attributes;
using Jint.Native;

namespace Jint.Benchmark;

/// <summary>
/// Source-gen sentinel for Object.prototype.{toString,hasOwnProperty,valueOf} and the __proto__
/// accessor. Post-source-gen the prototype uses [JsFunction] for the methods and [JsAccessor] for
/// __proto__'s get/set pair. Warm-path numbers should match the pre-source-gen baseline.
/// </summary>
[ShortRunJob]
[MemoryDiagnoser]
public class ObjectPrototypeBenchmarks
{
    private Engine _warm = null!;
    private Prepared<Script> _toString;
    private Prepared<Script> _hasOwn;
    private Prepared<Script> _isPrototypeOf;
    private Prepared<Script> _protoGet;
    private Prepared<Script> _propertyIsEnumerable;

    [GlobalSetup]
    public void GlobalSetup()
    {
        _toString             = Engine.PrepareScript("({a:1}).toString()");
        _hasOwn               = Engine.PrepareScript("({a:1}).hasOwnProperty('a')");
        _isPrototypeOf        = Engine.PrepareScript("Object.prototype.isPrototypeOf({a:1})");
        _protoGet             = Engine.PrepareScript("({a:1}).__proto__");
        _propertyIsEnumerable = Engine.PrepareScript("({a:1}).propertyIsEnumerable('a')");

        _warm = new Engine();
        _warm.Evaluate(_toString);
        _warm.Evaluate(_hasOwn);
        _warm.Evaluate(_isPrototypeOf);
        _warm.Evaluate(_protoGet);
        _warm.Evaluate(_propertyIsEnumerable);
    }

    [Benchmark]
    public JsValue Warm_ToString() => _warm.Evaluate(_toString);

    [Benchmark]
    public JsValue Warm_HasOwnProperty() => _warm.Evaluate(_hasOwn);

    [Benchmark]
    public JsValue Warm_IsPrototypeOf() => _warm.Evaluate(_isPrototypeOf);

    [Benchmark]
    public JsValue Warm_ProtoGet() => _warm.Evaluate(_protoGet);

    [Benchmark]
    public JsValue Warm_PropertyIsEnumerable() => _warm.Evaluate(_propertyIsEnumerable);
}
