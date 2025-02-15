using Jint.Collections;
using Jint.Native.Iterator;
using Jint.Native.Object;
using Jint.Native.Symbol;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;

namespace Jint.Native.Generator;

/// <summary>
/// https://tc39.es/ecma262/#sec-asyncgenerator-objects
/// </summary>
internal sealed class AsyncGeneratorPrototype : ObjectInstance
{
    private readonly AsyncGeneratorFunctionPrototype _constructor;

    internal AsyncGeneratorPrototype(
        Engine engine,
        AsyncGeneratorFunctionPrototype constructor,
        IteratorPrototype iteratorPrototype) : base(engine)
    {
        _constructor = constructor;
        _prototype = iteratorPrototype;
    }

    protected override void Initialize()
    {
        const PropertyFlag PropertyFlags = PropertyFlag.Configurable | PropertyFlag.Writable;
        const PropertyFlag LengthFlags = PropertyFlag.Configurable;
        var properties = new PropertyDictionary(4, false)
        {
            ["constructor"] = new(_constructor, PropertyFlag.Configurable),
            ["next"] = new(new ClrFunction(Engine, "next", Next, 1, LengthFlags), PropertyFlags),
            ["return"] = new(new ClrFunction(Engine, "return", Return, 1, LengthFlags), PropertyFlags),
            ["throw"] = new(new ClrFunction(Engine, "throw", Throw, 1, LengthFlags), PropertyFlags)
        };
        SetProperties(properties);

        var symbols = new SymbolDictionary(1)
        {
            [GlobalSymbolRegistry.ToStringTag] = new("Generator", PropertyFlag.Configurable)
        };
        SetSymbols(symbols);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-generator.prototype.next
    /// </summary>
    private ObjectInstance Next(JsValue thisObject, JsCallArguments arguments)
    {
        var g = AssertGeneratorInstance(thisObject);
        var value = arguments.At(0, null!);
        return g.GeneratorResume(value, null);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-generator.prototype.return
    /// </summary>
    private JsValue Return(JsValue thisObject, JsCallArguments arguments)
    {
        var g = AssertGeneratorInstance(thisObject);
        var value = arguments.At(0);
        var C = new Completion(CompletionType.Return, value, null!);
        return g.GeneratorResumeAbrupt(C, null);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-generator.prototype.throw
    /// </summary>
    private JsValue Throw(JsValue thisObject, JsCallArguments arguments)
    {
        var g = AssertGeneratorInstance(thisObject);
        var exception = arguments.At(0);
        var C = new Completion(CompletionType.Throw, exception, null!);
        return g.GeneratorResumeAbrupt(C, null);
    }

    private GeneratorInstance AssertGeneratorInstance(JsValue thisObj)
    {
        var generatorInstance = thisObj as GeneratorInstance;
        if (generatorInstance is null)
        {
            ExceptionHelper.ThrowTypeError(_engine.Realm, "object must be a Generator instance");
        }

        return generatorInstance;
    }
}
