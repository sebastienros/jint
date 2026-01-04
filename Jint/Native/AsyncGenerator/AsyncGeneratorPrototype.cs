using Jint.Native.Iterator;
using Jint.Native.Object;
using Jint.Native.Promise;
using Jint.Native.Symbol;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;

namespace Jint.Native.AsyncGenerator;

/// <summary>
/// https://tc39.es/ecma262/#sec-asyncgenerator-prototype-object
/// </summary>
internal sealed class AsyncGeneratorPrototype : ObjectInstance
{
    private readonly AsyncGeneratorFunctionPrototype _constructor;

    internal AsyncGeneratorPrototype(
        Engine engine,
        AsyncGeneratorFunctionPrototype constructor,
        AsyncIteratorPrototype asyncIteratorPrototype) : base(engine)
    {
        _constructor = constructor;
        _prototype = asyncIteratorPrototype;
    }

    protected override void Initialize()
    {
        const PropertyFlag PropertyFlags = PropertyFlag.Configurable | PropertyFlag.Writable;
        const PropertyFlag LengthFlags = PropertyFlag.Configurable;
        var properties = new PropertyDictionary(4, checkExistingKeys: false)
        {
            [KnownKeys.Constructor] = new(_constructor, PropertyFlag.Configurable),
            [KnownKeys.Next] = new(new ClrFunction(Engine, "next", Next, 1, LengthFlags), PropertyFlags),
            [KnownKeys.Return] = new(new ClrFunction(Engine, "return", Return, 1, LengthFlags), PropertyFlags),
            [KnownKeys.Throw] = new(new ClrFunction(Engine, "throw", Throw, 1, LengthFlags), PropertyFlags)
        };
        SetProperties(properties);

        var symbols = new SymbolDictionary(1)
        {
            [GlobalSymbolRegistry.ToStringTag] = new("AsyncGenerator", PropertyFlag.Configurable)
        };
        SetSymbols(symbols);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-asyncgenerator-prototype-next
    /// </summary>
    private JsValue Next(JsValue thisObject, JsCallArguments arguments)
    {
        // Per spec: If generator is not valid, return a rejected promise
        if (!TryGetAsyncGeneratorInstance(thisObject, out var g))
        {
            return CreateRejectedPromiseWithTypeError("object must be an AsyncGenerator instance");
        }

        var value = arguments.At(0);
        var completion = new Completion(CompletionType.Normal, value, null!);
        return g.AsyncGeneratorEnqueue(completion);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-asyncgenerator-prototype-return
    /// </summary>
    private JsValue Return(JsValue thisObject, JsCallArguments arguments)
    {
        // Per spec: If generator is not valid, return a rejected promise
        if (!TryGetAsyncGeneratorInstance(thisObject, out var g))
        {
            return CreateRejectedPromiseWithTypeError("object must be an AsyncGenerator instance");
        }

        var value = arguments.At(0);
        var completion = new Completion(CompletionType.Return, value, null!);
        return g.AsyncGeneratorEnqueue(completion);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-asyncgenerator-prototype-throw
    /// </summary>
    private JsValue Throw(JsValue thisObject, JsCallArguments arguments)
    {
        // Per spec: If generator is not valid, return a rejected promise
        if (!TryGetAsyncGeneratorInstance(thisObject, out var g))
        {
            return CreateRejectedPromiseWithTypeError("object must be an AsyncGenerator instance");
        }

        var exception = arguments.At(0);
        var completion = new Completion(CompletionType.Throw, exception, null!);
        return g.AsyncGeneratorEnqueue(completion);
    }

    private static bool TryGetAsyncGeneratorInstance(JsValue thisObj, out AsyncGeneratorInstance instance)
    {
        instance = (thisObj as AsyncGeneratorInstance)!;
        return instance is not null;
    }

    /// <summary>
    /// Creates a rejected promise with a TypeError.
    /// Per spec, when the generator is invalid, we return a rejected promise instead of throwing.
    /// </summary>
    private JsValue CreateRejectedPromiseWithTypeError(string message)
    {
        var promiseCapability = PromiseConstructor.NewPromiseCapability(_engine, _engine.Realm.Intrinsics.Promise);
        var error = _engine.Realm.Intrinsics.TypeError.Construct(message);
        promiseCapability.Reject.Call(Undefined, new[] { error });
        return promiseCapability.PromiseInstance;
    }
}
