using Jint.Native.Object;
using Jint.Native.Promise;
using Jint.Native.Symbol;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;

namespace Jint.Native.Iterator;

/// <summary>
/// https://tc39.es/ecma262/#sec-asynciteratorprototype
/// </summary>
internal sealed class AsyncIteratorPrototype : Prototype
{
    internal AsyncIteratorPrototype(
        Engine engine,
        Realm realm,
        ObjectInstance objectPrototype) : base(engine, realm)
    {
        _prototype = objectPrototype;
    }

    protected override void Initialize()
    {
        var symbols = new SymbolDictionary(2)
        {
            [GlobalSymbolRegistry.AsyncIterator] = new(
                new ClrFunction(Engine, "[Symbol.asyncIterator]", AsyncIterator, 0, PropertyFlag.Configurable),
                PropertyFlag.Writable | PropertyFlag.Configurable),
            [GlobalSymbolRegistry.AsyncDispose] = new(
                new ClrFunction(Engine, "[Symbol.asyncDispose]", AsyncDispose, 0, PropertyFlag.Configurable),
                PropertyFlag.Writable | PropertyFlag.Configurable)
        };
        SetSymbols(symbols);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-asynciteratorprototype-asynciterator
    /// %AsyncIteratorPrototype% [ @@asyncIterator ] ( )
    /// 1. Return the this value.
    /// </summary>
    private static JsValue AsyncIterator(JsValue thisObject, JsCallArguments arguments)
    {
        return thisObject;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-%asynciteratorprototype%-@@asyncDispose
    /// %AsyncIteratorPrototype% [ @@asyncDispose ] ( )
    /// </summary>
    private JsValue AsyncDispose(JsValue thisObject, JsCallArguments arguments)
    {
        // 1. Let O be the this value.
        var o = thisObject;

        // 2. Let promiseCapability be ! NewPromiseCapability(%Promise%).
        var promiseCapability = PromiseConstructor.NewPromiseCapability(_engine, _engine.Realm.Intrinsics.Promise);

        // 3. Let return be GetMethod(O, "return").
        ICallable? returnMethod;
        try
        {
            returnMethod = o.IsObject() ? o.AsObject().GetMethod(CommonProperties.Return) : null;
        }
        catch (JavaScriptException ex)
        {
            // 4. IfAbruptRejectPromise(return, promiseCapability).
            promiseCapability.Reject.Call(Undefined, ex.Error);
            return promiseCapability.PromiseInstance;
        }

        // 5. If return is undefined, then
        if (returnMethod is null)
        {
            // a. Perform ! Call(promiseCapability.[[Resolve]], undefined, « undefined »).
            promiseCapability.Resolve.Call(Undefined, Undefined);
        }
        else
        {
            // 6. Else,
            JsValue result;
            try
            {
                // a. Let result be Call(return, O, « undefined »).
                result = returnMethod.Call(o, Undefined);
            }
            catch (JavaScriptException ex)
            {
                // b. IfAbruptRejectPromise(result, promiseCapability).
                promiseCapability.Reject.Call(Undefined, ex.Error);
                return promiseCapability.PromiseInstance;
            }

            // c. Let resultWrapper be Completion(PromiseResolve(%Promise%, result)).
            JsPromise resultWrapper;
            try
            {
                resultWrapper = (JsPromise) _engine.Realm.Intrinsics.Promise.PromiseResolve(result);
            }
            catch (JavaScriptException ex)
            {
                // d. IfAbruptRejectPromise(resultWrapper, promiseCapability).
                promiseCapability.Reject.Call(Undefined, ex.Error);
                return promiseCapability.PromiseInstance;
            }

            // e. Let unwrap be a new Abstract Closure that performs the following steps when called:
            //    i. Return undefined.
            // f. Let onFulfilled be CreateBuiltinFunction(unwrap, 1, "", « »).
            var onFulfilled = new ClrFunction(_engine, "", (_, _) => Undefined, 1, PropertyFlag.Configurable);

            // g. Perform PerformPromiseThen(resultWrapper, onFulfilled, undefined, promiseCapability).
            PromiseOperations.PerformPromiseThen(_engine, resultWrapper, onFulfilled, null!, promiseCapability);
        }

        // 7. Return promiseCapability.[[Promise]].
        return promiseCapability.PromiseInstance;
    }
}
