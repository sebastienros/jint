using Jint.Collections;
using Jint.Native.Object;
using Jint.Native.Symbol;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;

namespace Jint.Native.Promise;

internal sealed class PromisePrototype : Prototype
{
    private readonly PromiseConstructor _constructor;

    internal PromisePrototype(
        Engine engine,
        Realm realm,
        PromiseConstructor constructor,
        ObjectPrototype objectPrototype) : base(engine, realm)
    {
        _prototype = objectPrototype;
        _constructor = constructor;
    }

    protected override void Initialize()
    {
        const PropertyFlag lengthFlags = PropertyFlag.Configurable;
        const PropertyFlag propertyFlags = PropertyFlag.Configurable | PropertyFlag.Writable;
        var properties = new PropertyDictionary(5, checkExistingKeys: false)
        {
            ["constructor"] = new(_constructor, PropertyFlag.NonEnumerable),
            ["then"] = new(new ClrFunction(Engine, "then", Then, 2, lengthFlags), propertyFlags),
            ["catch"] = new(new ClrFunction(Engine, "catch", Catch, 1, lengthFlags), propertyFlags),
            ["finally"] = new(new ClrFunction(Engine, "finally", Finally, 1, lengthFlags), propertyFlags)
        };
        SetProperties(properties);

        var symbols = new SymbolDictionary(1)
        {
            [GlobalSymbolRegistry.ToStringTag] = new(new JsString("Promise"), PropertyFlag.Configurable)
        };
        SetSymbols(symbols);
    }

    // https://tc39.es/ecma262/#sec-promise.prototype.then
    // When the then method is called with arguments onFulfilled and onRejected,
    // the following steps are taken:
    //
    // 1. Let promise be the this value.
    // 2. If IsPromise(promise) is false, throw a TypeError exception.
    // 3. Let C be ? SpeciesConstructor(promise, %Promise%).
    // 4. Let resultCapability be ? NewPromiseCapability(C).
    // 5. Return PerformPromiseThen(promise, onFulfilled, onRejected, resultCapability).
    private JsValue Then(JsValue thisValue, JsCallArguments arguments)
    {
        // 1. Let promise be the this value.
        // 2. If IsPromise(promise) is false, throw a TypeError exception.
        var promise = thisValue as JsPromise;
        if (promise is null)
        {
            ExceptionHelper.ThrowTypeError(_realm, "Method Promise.prototype.then called on incompatible receiver");
        }

        // 3. Let C be ? SpeciesConstructor(promise, %Promise%).
        var ctor = SpeciesConstructor(promise, _realm.Intrinsics.Promise);

        // 4. Let resultCapability be ? NewPromiseCapability(C).
        var capability = PromiseConstructor.NewPromiseCapability(_engine, (JsValue) ctor);

        // 5. Return PerformPromiseThen(promise, onFulfilled, onRejected, resultCapability).
        return PromiseOperations.PerformPromiseThen(_engine, promise, arguments.At(0), arguments.At(1), capability);
    }

    // https://tc39.es/ecma262/#sec-promise.prototype.catch
    //
    // When the catch method is called with argument onRejected,
    // the following steps are taken:
    //
    // 1. Let promise be the this value.
    // 2. Return ? Invoke(promise, "then", « undefined, onRejected »).
    private JsValue Catch(JsValue thisValue, JsCallArguments arguments) =>
        _engine.Invoke(thisValue, "then", new[] {Undefined, arguments.At(0)});

    // https://tc39.es/ecma262/#sec-promise.prototype.finally
    private JsValue Finally(JsValue thisValue, JsCallArguments arguments)
    {
        // 1. Let promise be the this value.
        // 2. If Type(promise) is not Object, throw a TypeError exception.
        var promise = thisValue as ObjectInstance;
        if (promise is null)
        {
            ExceptionHelper.ThrowTypeError(_realm, "this passed to Promise.prototype.finally is not an object");
        }

        // 3. Let C be ? SpeciesConstructor(promise, %Promise%).
        // 4. Assert: IsConstructor(C) is true.
        var ctor = SpeciesConstructor(promise, _realm.Intrinsics.Promise);

        JsValue thenFinally;
        JsValue catchFinally;
        var onFinally = arguments.At(0);

        // 5. If IsCallable(onFinally) is false, then
        if (onFinally is not ICallable onFinallyFunc)
        {
            // a. Let thenFinally be onFinally.
            // b. Let catchFinally be onFinally.
            thenFinally = onFinally;
            catchFinally = onFinally;
        }
        else
        {
            thenFinally = ThenFinallyFunctions(onFinallyFunc, ctor);
            catchFinally = CatchFinallyFunctions(onFinallyFunc, ctor);
        }

        // 7. Return ? Invoke(promise, "then", « thenFinally, catchFinally »).
        return _engine.Invoke(promise, "then", [thenFinally, catchFinally]);
    }

    // https://tc39.es/ecma262/#sec-thenfinallyfunctions
    private ClrFunction ThenFinallyFunctions(ICallable onFinally, IConstructor ctor) =>
        new ClrFunction(_engine, "", (_, args) =>
        {
            var value = args.At(0);

            //4.  Let result be ? Call(onFinally, undefined).
            var result = onFinally.Call(Undefined, Arguments.Empty);

            // 7. Let promise be ? PromiseResolve(C, result).
            var promise = _realm.Intrinsics.Promise.Resolve((JsValue) ctor, [result]);

            // 8. Let valueThunk be equivalent to a function that returns value.
            var valueThunk = new ClrFunction(_engine, "", (_, _) => value);

            // 9. Return ? Invoke(promise, "then", « valueThunk »).
            return _engine.Invoke(promise, "then", [valueThunk]);
        }, 1, PropertyFlag.Configurable);

    // https://tc39.es/ecma262/#sec-catchfinallyfunctions
    private ClrFunction CatchFinallyFunctions(ICallable onFinally, IConstructor ctor) =>
        new ClrFunction(_engine, "", (_, args) =>
        {
            var reason = args.At(0);

            //4.  Let result be ? Call(onFinally, undefined).
            var result = onFinally.Call(Undefined, Arguments.Empty);

            // 7. Let promise be ? PromiseResolve(C, result).
            var promise = _realm.Intrinsics.Promise.Resolve((JsValue) ctor, [result]);

            // 8. Let thrower be equivalent to a function that throws reason.
            var thrower = new ClrFunction(_engine, "", (_, _) => throw new JavaScriptException(reason));

            // 9. Return ? Invoke(promise, "then", « thrower »).
            return _engine.Invoke(promise, "then", [thrower]);
        }, 1, PropertyFlag.Configurable);
}
