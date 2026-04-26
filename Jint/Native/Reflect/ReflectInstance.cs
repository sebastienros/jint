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

    [JsFunction(Length = 3)]
    private JsValue Apply(JsValue thisObject, JsCallArguments arguments)
    {
        var target = arguments.At(0);
        var thisArgument = arguments.At(1);
        var argumentsList = arguments.At(2);

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
    [JsFunction(Length = 3)]
    private JsValue DefineProperty(JsValue thisObject, JsCallArguments arguments)
    {
        var target = arguments.At(0) as ObjectInstance;
        if (target is null)
        {
            Throw.TypeError(_realm, "Reflect.defineProperty called on non-object");
        }

        var propertyKey = arguments.At(1);
        var attributes = arguments.At(2);

        var key = TypeConverter.ToPropertyKey(propertyKey);
        var desc = PropertyDescriptor.ToPropertyDescriptor(_realm, attributes);

        return target.DefineOwnProperty(key, desc);
    }

    [JsFunction(Length = 2)]
    private JsValue DeleteProperty(JsValue thisObject, JsCallArguments arguments)
    {
        var o = arguments.At(0) as ObjectInstance;
        if (o is null)
        {
            Throw.TypeError(_realm, "Reflect.deleteProperty called on non-object");
        }

        var property = TypeConverter.ToPropertyKey(arguments.At(1));
        return o.Delete(property) ? JsBoolean.True : JsBoolean.False;
    }

    [JsFunction(Length = 2)]
    private JsValue Has(JsValue thisObject, JsCallArguments arguments)
    {
        var o = arguments.At(0) as ObjectInstance;
        if (o is null)
        {
            Throw.TypeError(_realm, "Reflect.has called on non-object");
        }

        var property = TypeConverter.ToPropertyKey(arguments.At(1));
        return o.HasProperty(property) ? JsBoolean.True : JsBoolean.False;
    }

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

    [JsFunction(Length = 2)]
    private JsValue GetOwnPropertyDescriptor(JsValue thisObject, JsCallArguments arguments)
    {
        if (!arguments.At(0).IsObject())
        {
            Throw.TypeError(_realm, "Reflect.getOwnPropertyDescriptor called on non-object");
        }
        return _realm.Intrinsics.Object.GetOwnPropertyDescriptor(Undefined, arguments);
    }

    [JsFunction(Length = 1)]
    private JsValue OwnKeys(JsValue thisObject, JsCallArguments arguments)
    {
        var o = arguments.At(0) as ObjectInstance;
        if (o is null)
        {
            Throw.TypeError(_realm, "Reflect.get called on non-object");
        }

        var keys = o.GetOwnPropertyKeys();
        return _realm.Intrinsics.Array.CreateArrayFromList(keys);
    }

    [JsFunction(Length = 1)]
    private JsValue IsExtensible(JsValue thisObject, JsCallArguments arguments)
    {
        var o = arguments.At(0) as ObjectInstance;
        if (o is null)
        {
            Throw.TypeError(_realm, "Reflect.isExtensible called on non-object");
        }

        return o.Extensible;
    }

    [JsFunction(Length = 1)]
    private JsValue PreventExtensions(JsValue thisObject, JsCallArguments arguments)
    {
        var o = arguments.At(0) as ObjectInstance;
        if (o is null)
        {
            Throw.TypeError(_realm, "Reflect.preventExtensions called on non-object");
        }

        return o.PreventExtensions();
    }

    [JsFunction(Length = 1)]
    private JsValue GetPrototypeOf(JsValue thisObject, JsCallArguments arguments)
    {
        var target = arguments.At(0);

        if (!target.IsObject())
        {
            Throw.TypeError(_realm, "Reflect.getPrototypeOf called on non-object");
        }

        return _realm.Intrinsics.Object.GetPrototypeOf(Undefined, arguments);
    }

    [JsFunction(Length = 2)]
    private JsValue SetPrototypeOf(JsValue thisObject, JsCallArguments arguments)
    {
        var target = arguments.At(0);

        var o = target as ObjectInstance;
        if (o is null)
        {
            Throw.TypeError(_realm, "Reflect.setPrototypeOf called on non-object");
        }

        var prototype = arguments.At(1);
        if (!prototype.IsObject() && !prototype.IsNull())
        {
            Throw.TypeError(_realm, $"Object prototype may only be an Object or null: {prototype}");
        }

        return o.SetPrototypeOf(prototype);
    }
}
