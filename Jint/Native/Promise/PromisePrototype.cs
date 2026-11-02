using System.Runtime.CompilerServices;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;

namespace Jint.Native.Promise;

[JsObject(UseShape = true)]
internal sealed partial class PromisePrototype : Prototype
{
    [JsProperty(Name = "constructor", Flags = PropertyFlag.NonEnumerable)]
    private readonly PromiseConstructor _constructor;

    [JsSymbol("ToStringTag", Flags = PropertyFlag.Configurable)] private static readonly JsString PromiseToStringTag = new("Promise");

    // Stands in for an own `constructor` that is not a data property; its value is never a constructor.
    private static readonly PropertyDescriptor NotADataProperty = new(Undefined, PropertyFlag.None);

    // The own `constructor` data descriptor as it stood at _constructorDescriptorVersion (NotADataProperty if it was not
    // one), or null while it has not been read. Replacing, deleting or redefining the property as an accessor goes
    // through SetOwnProperty, SetProperty or RemoveOwnProperty and moves _propertiesVersion; an assignment, or a
    // redefinition that keeps it a data property, changes this descriptor's value in place, which is re-read every time.
    private PropertyDescriptor? _constructorDescriptor;
    private uint _constructorDescriptorVersion;

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
        CreateProperties_Generated();
        CreateSymbols_Generated();
    }

    /// <summary>
    /// Whether this prototype's own <c>constructor</c> is a data property holding its realm's <c>%Promise%</c>, so that
    /// reading <c>constructor</c> off a promise that inherits it answers <c>%Promise%</c> and runs no code.
    /// </summary>
    /// <remarks>
    /// <see href="https://tc39.es/ecma262/#sec-promise-resolve">PromiseResolve</see> performs that read for every
    /// promise it is handed, which is every <c>await</c> of one; this is what lets it skip the read when the answer
    /// is known. The descriptor is looked up again only after this prototype's own property set has changed.
    /// </remarks>
    internal bool HasIntrinsicConstructor
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            var descriptor = _constructorDescriptor;
            if (descriptor is null || _propertiesVersion != _constructorDescriptorVersion)
            {
                descriptor = ReadConstructorDescriptor();
            }

            return ReferenceEquals(descriptor.Value, _constructor);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private PropertyDescriptor ReadConstructorDescriptor()
    {
        // GetOwnProperty first: it initializes the prototype on first use, and the version is the one after that.
        var descriptor = GetOwnProperty(CommonProperties.Constructor);
        if (!descriptor.IsDataDescriptor())
        {
            descriptor = NotADataProperty;
        }

        _constructorDescriptorVersion = _propertiesVersion;
        _constructorDescriptor = descriptor;
        return descriptor;
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
    [JsFunction]
    private JsValue Then(JsValue thisObject, JsValue onFulfilled, JsValue onRejected)
    {
        // 1. Let promise be the this value.
        // 2. If IsPromise(promise) is false, throw a TypeError exception.
        var promise = thisObject as JsPromise;
        if (promise is null)
        {
            Throw.TypeError(_realm, "Method Promise.prototype.then called on incompatible receiver");
        }

        // 3. Let C be ? SpeciesConstructor(promise, %Promise%).
        var ctor = SpeciesConstructor(promise, _realm.Intrinsics.Promise);

        // 4. Let resultCapability be ? NewPromiseCapability(C).
        var capability = PromiseConstructor.NewPromiseCapability(_engine, (JsValue) ctor);

        // 5. Return PerformPromiseThen(promise, onFulfilled, onRejected, resultCapability).
        return PromiseOperations.PerformPromiseThen(_engine, promise, onFulfilled, onRejected, capability);
    }

    // https://tc39.es/ecma262/#sec-promise.prototype.catch
    //
    // When the catch method is called with argument onRejected,
    // the following steps are taken:
    //
    // 1. Let promise be the this value.
    // 2. Return ? Invoke(promise, "then", « undefined, onRejected »).
    [JsFunction]
    private JsValue Catch(JsValue thisObject, JsValue onRejected) =>
        _engine.Invoke(thisObject, "then", [Undefined, onRejected]);

    // https://tc39.es/ecma262/#sec-promise.prototype.finally
    [JsFunction]
    private JsValue Finally(JsValue thisObject, JsValue onFinally)
    {
        // 1. Let promise be the this value.
        // 2. If Type(promise) is not Object, throw a TypeError exception.
        var promise = thisObject as ObjectInstance;
        if (promise is null)
        {
            Throw.TypeError(_realm, "this passed to Promise.prototype.finally is not an object");
        }

        // 3. Let C be ? SpeciesConstructor(promise, %Promise%).
        // 4. Assert: IsConstructor(C) is true.
        var ctor = SpeciesConstructor(promise, _realm.Intrinsics.Promise);

        JsValue thenFinally;
        JsValue catchFinally;

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
