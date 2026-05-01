using Jint.Native.Iterator;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.Native.Generator;

/// <summary>
/// https://tc39.es/ecma262/#sec-generator-objects
/// </summary>
[JsObject]
internal sealed partial class GeneratorPrototype : ObjectInstance
{
    private readonly Realm _realm;

    [JsProperty(Name = "constructor", Flags = PropertyFlag.Configurable)]
    private readonly GeneratorFunctionPrototype _constructor;

    [JsSymbol("ToStringTag", Flags = PropertyFlag.Configurable)] private static readonly JsString GeneratorToStringTag = new("Generator");

    internal GeneratorPrototype(
        Engine engine,
        Realm realm,
        GeneratorFunctionPrototype constructor,
        IteratorPrototype iteratorPrototype) : base(engine)
    {
        _realm = realm;
        _constructor = constructor;
        _prototype = iteratorPrototype;
    }

    protected override void Initialize()
    {
        CreateProperties_Generated();
        CreateSymbols_Generated();
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-generator.prototype.next
    /// </summary>
    [JsFunction(Length = 1)]
    private ObjectInstance Next(JsValue thisObject, JsValue value)
    {
        var g = AssertGeneratorInstance(thisObject);
        return g.GeneratorResume(value, null);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-generator.prototype.return
    /// </summary>
    [JsFunction(Length = 1)]
    private JsValue Return(JsValue thisObject, JsValue value)
    {
        var g = AssertGeneratorInstance(thisObject);
        var C = new Completion(CompletionType.Return, value, null!);
        return g.GeneratorResumeAbrupt(C, null);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-generator.prototype.throw
    /// </summary>
    [JsFunction(Length = 1)]
    private JsValue Throw(JsValue thisObject, JsValue exception)
    {
        var g = AssertGeneratorInstance(thisObject);
        var C = new Completion(CompletionType.Throw, exception, null!);
        return g.GeneratorResumeAbrupt(C, null);
    }

    private GeneratorInstance AssertGeneratorInstance(JsValue thisObj)
    {
        var generatorInstance = thisObj as GeneratorInstance;
        if (generatorInstance is null)
        {
            Runtime.Throw.TypeError(_realm, "object must be a Generator instance");
        }

        return generatorInstance;
    }
}
