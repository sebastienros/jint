#pragma warning disable CA1859 // Use concrete types when possible for improved performance -- most of prototype methods return JsValue

using Jint.Native.Function;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.Native.Reflect;

/// <summary>
/// https://www.ecma-international.org/ecma-262/6.0/index.html#sec-reflect-object
/// </summary>
[JsObject]
internal sealed partial class ReflectInstance : ObjectInstance
{
    private readonly Realm _realm;

    [JsSymbol("ToStringTag", Flags = PropertyFlag.Configurable)] private static readonly JsString ReflectToStringTag = new("Reflect");

    internal ReflectInstance(
        Engine engine,
        Realm realm,
        ObjectPrototype objectPrototype) : base(engine)
    {
        _realm = realm;
        _prototype = objectPrototype;
    }

    protected override void Initialize()
    {
        CreateProperties_Generated();
        CreateSymbols_Generated();
    }

    [JsFunction]
    private JsValue Apply(JsValue thisObject, JsValue target, JsValue thisArgument, JsValue argumentsList)
    {
        if (!target.IsCallable)
        {
            Throw.TypeError(_realm, "Reflect.apply requires the first argument to be a function");
        }

        var args = FunctionPrototype.CreateListFromArrayLike(_realm, argumentsList);

        // 3. Perform PrepareForTailCall().

        return ((ICallable) target).Call(thisArgument, args);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-reflect.construct
    /// </summary>
    // Spec distinguishes "newTarget is not present" (defaults to target) from "newTarget is undefined"
    // (must throw via IsConstructor). The arguments-array form is the only way to detect that
    // difference; the source generator's per-parameter unpacking always passes Undefined for missing
    // positions, conflating the two cases.
    [JsFunction(Length = 2)]
    private JsValue Construct(JsValue thisObject, JsCallArguments arguments)
    {
        var target = AssertConstructor(_engine, arguments.At(0));

        var newTargetArgument = arguments.At(2, arguments[0]);
        AssertConstructor(_engine, newTargetArgument);

        var args = FunctionPrototype.CreateListFromArrayLike(_realm, arguments.At(1));

        return target.Construct(args, newTargetArgument);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-reflect.defineproperty
    /// </summary>
    [JsFunction]
    private JsValue DefineProperty(JsValue thisObject, JsValue target, JsValue propertyKey, JsValue attributes)
    {
        var o = target as ObjectInstance;
        if (o is null)
        {
            Throw.TypeError(_realm, "Reflect.defineProperty called on non-object");
        }

        var key = TypeConverter.ToPropertyKey(propertyKey);
        var desc = PropertyDescriptor.ToPropertyDescriptor(_realm, attributes);

        return o.DefineOwnProperty(key, desc);
    }

    [JsFunction]
    private JsValue DeleteProperty(JsValue thisObject, JsValue target, JsValue propertyKey)
    {
        var o = target as ObjectInstance;
        if (o is null)
        {
            Throw.TypeError(_realm, "Reflect.deleteProperty called on non-object");
        }

        var property = TypeConverter.ToPropertyKey(propertyKey);
        return o.Delete(property) ? JsBoolean.True : JsBoolean.False;
    }

    [JsFunction]
    private JsValue Has(JsValue thisObject, JsValue target, JsValue propertyKey)
    {
        var o = target as ObjectInstance;
        if (o is null)
        {
            Throw.TypeError(_realm, "Reflect.has called on non-object");
        }

        var property = TypeConverter.ToPropertyKey(propertyKey);
        return o.HasProperty(property) ? JsBoolean.True : JsBoolean.False;
    }

    // "receiver is not present" defaults to target; "receiver is undefined" is preserved and
    // forwarded to [[Set]] (where, per spec, a non-Object receiver causes the set to return false).
    // Generator-unpacked params can't represent "not present", so this method takes the array form.
    [JsFunction(Length = 3)]
    private JsValue Set(JsValue thisObject, JsCallArguments arguments)
    {
        var target = arguments.At(0);
        var property = TypeConverter.ToPropertyKey(arguments.At(1));
        var value = arguments.At(2);
        var receiver = arguments.At(3, target);

        var o = target as ObjectInstance;
        if (o is null)
        {
            Throw.TypeError(_realm, "Reflect.set called on non-object");
        }

        return o.Set(property, value, receiver);
    }

    // Same not-present-vs-undefined distinction as Set: receiver defaults to target only when the
    // caller didn't pass a third argument. Explicit undefined is preserved and reaches [[Get]] /
    // accessor invocation as the receiver.
    [JsFunction(Length = 2)]
    private JsValue Get(JsValue thisObject, JsCallArguments arguments)
    {
        var target = arguments.At(0);
        var o = target as ObjectInstance;
        if (o is null)
        {
            Throw.TypeError(_realm, "Reflect.get called on non-object");
        }

        var receiver = arguments.At(2, target);
        var property = TypeConverter.ToPropertyKey(arguments.At(1));
        return o.Get(property, receiver);
    }

    [JsFunction]
    private JsValue GetOwnPropertyDescriptor(JsValue thisObject, JsValue target, JsValue propertyKey)
    {
        if (!target.IsObject())
        {
            Throw.TypeError(_realm, "Reflect.getOwnPropertyDescriptor called on non-object");
        }
        return _realm.Intrinsics.Object.GetOwnPropertyDescriptor(Undefined, [target, propertyKey]);
    }

    [JsFunction]
    private JsValue OwnKeys(JsValue thisObject, JsValue target)
    {
        var o = target as ObjectInstance;
        if (o is null)
        {
            Throw.TypeError(_realm, "Reflect.ownKeys called on non-object");
        }

        var keys = o.GetOwnPropertyKeys();
        return _realm.Intrinsics.Array.CreateArrayFromList(keys);
    }

    [JsFunction]
    private JsValue IsExtensible(JsValue thisObject, JsValue target)
    {
        var o = target as ObjectInstance;
        if (o is null)
        {
            Throw.TypeError(_realm, "Reflect.isExtensible called on non-object");
        }

        return o.Extensible;
    }

    [JsFunction]
    private JsValue PreventExtensions(JsValue thisObject, JsValue target)
    {
        var o = target as ObjectInstance;
        if (o is null)
        {
            Throw.TypeError(_realm, "Reflect.preventExtensions called on non-object");
        }

        return o.PreventExtensions();
    }

    [JsFunction]
    private JsValue GetPrototypeOf(JsValue thisObject, JsValue target)
    {
        if (!target.IsObject())
        {
            Throw.TypeError(_realm, "Reflect.getPrototypeOf called on non-object");
        }

        return _realm.Intrinsics.Object.GetPrototypeOf(Undefined, [target]);
    }

    [JsFunction]
    private JsValue SetPrototypeOf(JsValue thisObject, JsValue target, JsValue prototype)
    {
        var o = target as ObjectInstance;
        if (o is null)
        {
            Throw.TypeError(_realm, "Reflect.setPrototypeOf called on non-object");
        }

        if (!prototype.IsObject() && !prototype.IsNull())
        {
            Throw.TypeError(_realm, $"Object prototype may only be an Object or null: {prototype}");
        }

        return o.SetPrototypeOf(prototype);
    }
}
