using System;
using System.Linq;
using System.Threading.Tasks;
using Jint.Collections;
using Jint.Native.Function;
using Jint.Native.Object;
using Jint.Native.Symbol;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;

namespace Jint.Native.Promise
{
    public sealed class PromisePrototype : ObjectInstance
    {
        private PromiseConstructor _promiseConstructor;

        private PromisePrototype(Engine engine) : base(engine)
        {
        }

        public static PromisePrototype CreatePrototypeObject(Engine engine, PromiseConstructor promiseConstructor)
        {
            var obj = new PromisePrototype(engine)
            {
                _prototype = engine.Object.PrototypeObject,
                _promiseConstructor = promiseConstructor
            };

            return obj;
        }

        protected override void Initialize()
        {
            const PropertyFlag lengthFlags = PropertyFlag.Configurable;
            const PropertyFlag propertyFlags = PropertyFlag.Configurable | PropertyFlag.Writable;
            var properties = new PropertyDictionary(5, checkExistingKeys: false)
            {
                ["constructor"] = new(_promiseConstructor, PropertyFlag.NonEnumerable),
                ["then"] = new(new ClrFunctionInstance(Engine, "then", Then, 2, lengthFlags), propertyFlags),
                ["catch"] = new(new ClrFunctionInstance(Engine, "catch", Catch, 1, lengthFlags), propertyFlags),
                ["finally"] = new(new ClrFunctionInstance(Engine, "finally", Finally, 1, lengthFlags), propertyFlags)
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
        private JsValue Then(JsValue thisValue, JsValue[] args)
        {
            // 1. Let promise be the this value.
            // 2. If IsPromise(promise) is false, throw a TypeError exception.
            var promise = thisValue as PromiseInstance ?? ExceptionHelper.ThrowTypeError<PromiseInstance>(_engine,
                "Method Promise.prototype.then called on incompatible receiver");

            // 3. Let C be ? SpeciesConstructor(promise, %Promise%).
            var ctor = SpeciesConstructor(promise, _engine.Promise);

            // 4. Let resultCapability be ? NewPromiseCapability(C).
            var capability = PromiseConstructor.NewPromiseCapability(_engine, ctor as JsValue);

            // 5. Return PerformPromiseThen(promise, onFulfilled, onRejected, resultCapability).
            return PromiseOperations.PerformPromiseThen(_engine, promise, args.At(0), args.At(1), capability);
        }

        // https://tc39.es/ecma262/#sec-promise.prototype.catch
        // 
        // When the catch method is called with argument onRejected,
        // the following steps are taken:
        //
        // 1. Let promise be the this value.
        // 2. Return ? Invoke(promise, "then", « undefined, onRejected »).
        private JsValue Catch(JsValue thisValue, JsValue[] args) =>
            Invoke(thisValue, "then", new[] {Undefined, args.At(0)});

        // https://tc39.es/ecma262/#sec-promise.prototype.finally
        private JsValue Finally(JsValue thisValue, JsValue[] args)
        {
            // 1. Let promise be the this value.
            // 2. If Type(promise) is not Object, throw a TypeError exception.
            var promise = thisValue as ObjectInstance ?? ExceptionHelper.ThrowTypeError<ObjectInstance>(_engine,
                "this passed to Promise.prototype.finally is not an object");

            // 3. Let C be ? SpeciesConstructor(promise, %Promise%).
            // 4. Assert: IsConstructor(C) is true.
            var ctor = SpeciesConstructor(promise, _engine.Promise);

            JsValue thenFinally;
            JsValue catchFinally;
            var onFinally = args.At(0);

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
            return Invoke(promise, "then", new[] {thenFinally, catchFinally});
        }

        // https://tc39.es/ecma262/#sec-thenfinallyfunctions
        private JsValue ThenFinallyFunctions(ICallable onFinally, IConstructor ctor) =>
            new ClrFunctionInstance(_engine, "", (_, args) =>
            {
                var value = args.At(0);

                //4.  Let result be ? Call(onFinally, undefined).
                var result = onFinally.Call(Undefined, Arguments.Empty);

                // 7. Let promise be ? PromiseResolve(C, result).
                var promise = _engine.Promise.Resolve(ctor as JsValue, new[] {result});

                // 8. Let valueThunk be equivalent to a function that returns value.
                var valueThunk = new ClrFunctionInstance(_engine, "", (_, _) => value);

                // 9. Return ? Invoke(promise, "then", « valueThunk »).
                return Invoke(promise, "then", new JsValue[] {valueThunk});
            }, 1, PropertyFlag.Configurable);

        // https://tc39.es/ecma262/#sec-catchfinallyfunctions
        private JsValue CatchFinallyFunctions(ICallable onFinally, IConstructor ctor) =>
            new ClrFunctionInstance(_engine, "", (_, args) =>
            {
                var reason = args.At(0);

                //4.  Let result be ? Call(onFinally, undefined).
                var result = onFinally.Call(Undefined, Arguments.Empty);

                // 7. Let promise be ? PromiseResolve(C, result).
                var promise = _engine.Promise.Resolve(ctor as JsValue, new[] {result});

                // 8. Let thrower be equivalent to a function that throws reason.
                var thrower = new ClrFunctionInstance(_engine, "", (_, _) => throw new JavaScriptException(reason));

                // 9. Return ? Invoke(promise, "then", « thrower »).
                return Invoke(promise, "then", new JsValue[] {thrower});
            }, 1, PropertyFlag.Configurable);
    }
}