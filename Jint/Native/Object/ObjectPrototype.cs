#pragma warning disable CA1859 // Use concrete types when possible for improved performance -- most of prototype methods return JsValue

using Jint.Native.Array;
using Jint.Native.Symbol;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.Native.Object;

[JsObject]
public sealed partial class ObjectPrototype : Prototype
{
    [JsProperty(Name = "constructor", Flags = PropertyFlag.Configurable | PropertyFlag.Writable)]
    private readonly ObjectConstructor _constructor;

    internal ObjectChangeFlags _objectChangeFlags;

    internal ObjectPrototype(
        Engine engine,
        Realm realm,
        ObjectConstructor constructor) : base(engine, realm)
    {
        _constructor = constructor;
    }

    protected override void Initialize() => CreateProperties_Generated();

    /// <summary>
    /// https://tc39.es/ecma262/#sec-get-object.prototype.__proto__
    /// </summary>
    [JsAccessor("__proto__")]
    private JsValue ProtoGet(JsValue thisObject)
        => TypeConverter.ToObject(_realm, thisObject).GetPrototypeOf() ?? Null;

    /// <summary>
    /// https://tc39.es/ecma262/#sec-set-object.prototype.__proto__
    /// </summary>
    [JsAccessor("__proto__", AccessorKind.Set)]
    private JsValue ProtoSet(JsValue thisObject, JsValue value)
    {
        TypeConverter.RequireObjectCoercible(_engine, thisObject);

        if (!value.IsObject() && !value.IsNull() || thisObject is not ObjectInstance objectInstance)
        {
            return Undefined;
        }

        if (!objectInstance.SetPrototypeOf(value))
        {
            Throw.TypeError(_realm, "Invalid prototype");
        }

        return Undefined;
    }

    internal override bool SetPrototypeOf(JsValue value)
    {
        return SameValue(value, _prototype ?? Null);
    }

    public override bool DefineOwnProperty(JsValue property, PropertyDescriptor desc)
    {
        TrackChanges(property);
        return base.DefineOwnProperty(property, desc);
    }

    protected internal override void SetOwnProperty(JsValue property, PropertyDescriptor desc)
    {
        TrackChanges(property);
        base.SetOwnProperty(property, desc);
    }

    private void TrackChanges(JsValue property)
    {
        EnsureInitialized();
        if (ArrayInstance.IsArrayIndex(property, out _))
        {
            _objectChangeFlags |= ObjectChangeFlags.ArrayIndex;
        }
        else
        {
            _objectChangeFlags |= property.IsSymbol() ? ObjectChangeFlags.Symbol : ObjectChangeFlags.Property;
        }
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-object.prototype.__defineGetter__
    /// </summary>
    [JsFunction(Length = 2, Name = "__defineGetter__")]
    private JsValue DefineGetter(JsValue thisObject, JsValue p, JsValue getter)
    {
        var o = TypeConverter.ToObject(_realm, thisObject);

        if (!getter.IsCallable)
        {
            Throw.TypeError(_realm, "Object.prototype.__defineGetter__: Expecting function");
        }

        var desc = new GetSetPropertyDescriptor(getter, null, enumerable: true, configurable: true);
        var key = TypeConverter.ToPropertyKey(p);
        o.DefinePropertyOrThrow(key, desc);

        return Undefined;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-object.prototype.__defineSetter__
    /// </summary>
    [JsFunction(Length = 2, Name = "__defineSetter__")]
    private JsValue DefineSetter(JsValue thisObject, JsValue p, JsValue setter)
    {
        var o = TypeConverter.ToObject(_realm, thisObject);

        if (!setter.IsCallable)
        {
            Throw.TypeError(_realm, "Object.prototype.__defineSetter__: Expecting function");
        }

        var desc = new GetSetPropertyDescriptor(null, setter, enumerable: true, configurable: true);
        var key = TypeConverter.ToPropertyKey(p);
        o.DefinePropertyOrThrow(key, desc);

        return Undefined;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-object.prototype.__lookupGetter__
    /// </summary>
    [JsFunction(Length = 1, Name = "__lookupGetter__")]
    private JsValue LookupGetter(JsValue thisObject, JsValue p)
    {
        var o = TypeConverter.ToObject(_realm, thisObject);
        var key = TypeConverter.ToPropertyKey(p);
        while (true)
        {
            var desc = o.GetOwnProperty(key);
            if (!ReferenceEquals(desc, PropertyDescriptor.Undefined))
            {
                if (desc.IsAccessorDescriptor())
                {
                    return desc.Get ?? Undefined;
                }

                return Undefined;
            }

            o = o.GetPrototypeOf();
            if (o is null)
            {
                return Undefined;
            }
        }
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-object.prototype.__lookupSetter__
    /// </summary>
    [JsFunction(Length = 1, Name = "__lookupSetter__")]
    private JsValue LookupSetter(JsValue thisObject, JsValue p)
    {
        var o = TypeConverter.ToObject(_realm, thisObject);
        var key = TypeConverter.ToPropertyKey(p);
        while (true)
        {
            var desc = o.GetOwnProperty(key);
            if (!ReferenceEquals(desc, PropertyDescriptor.Undefined))
            {
                if (desc.IsAccessorDescriptor())
                {
                    return desc.Set ?? Undefined;
                }

                return Undefined;
            }

            o = o.GetPrototypeOf();
            if (o is null)
            {
                return Undefined;
            }
        }
    }

    [JsFunction(Length = 1)]
    private JsValue PropertyIsEnumerable(JsValue thisObject, JsValue v)
    {
        var p = TypeConverter.ToPropertyKey(v);
        var o = TypeConverter.ToObject(_realm, thisObject);
        var desc = o.GetOwnProperty(p);
        if (desc == PropertyDescriptor.Undefined)
        {
            return JsBoolean.False;
        }
        return desc.Enumerable;
    }

    [JsFunction(Length = 0)]
    private JsValue ValueOf(JsValue thisObject)
    {
        var o = TypeConverter.ToObject(_realm, thisObject);
        return o;
    }

    [JsFunction(Length = 1)]
    private JsValue IsPrototypeOf(JsValue thisObject, JsValue arg)
    {
        if (!arg.IsObject())
        {
            return JsBoolean.False;
        }

        var v = arg.AsObject();

        var o = TypeConverter.ToObject(_realm, thisObject);
        while (true)
        {
            v = v.Prototype;

            if (v is null)
            {
                return JsBoolean.False;
            }

            if (ReferenceEquals(o, v))
            {
                return JsBoolean.True;
            }
        }
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-object.prototype.tolocalestring
    /// </summary>
    [JsFunction(Length = 0)]
    private JsValue ToLocaleString(JsValue thisObject)
    {
        return Invoke(thisObject, "toString", []);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-object.prototype.tostring
    /// </summary>
    [JsFunction(Length = 0, Name = "toString")]
    internal JsValue ToObjectString(JsValue thisObject)
    {
        if (thisObject.IsUndefined())
        {
            return "[object Undefined]";
        }

        if (thisObject.IsNull())
        {
            return "[object Null]";
        }

        var o = TypeConverter.ToObject(_realm, thisObject);
        var isArray = o.IsArray();

        var tag = o.Get(GlobalSymbolRegistry.ToStringTag);
        if (!tag.IsString())
        {
            if (isArray)
            {
                tag = "Array";
            }
            else if (o.IsCallable)
            {
                tag = "Function";
            }
            else
            {
                tag = (o is JsProxy ? ObjectClass.Object : o.Class).ToString();
            }
        }

        return "[object " + tag + "]";
    }

    /// <summary>
    /// http://www.ecma-international.org/ecma-262/5.1/#sec-15.2.4.5
    /// </summary>
    [JsFunction(Length = 1)]
    private JsValue HasOwnProperty(JsValue thisObject, JsValue v)
    {
        var p = TypeConverter.ToPropertyKey(v);
        var o = TypeConverter.ToObject(_realm, thisObject);
        var desc = o.GetOwnProperty(p);
        return desc != PropertyDescriptor.Undefined;
    }
}
