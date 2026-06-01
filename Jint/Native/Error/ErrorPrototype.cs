using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.Native.Error;

/// <summary>
/// http://www.ecma-international.org/ecma-262/5.1/#sec-15.11.4
/// </summary>
[JsObject]
internal sealed partial class ErrorPrototype : ErrorInstance
{
    [JsProperty(Name = "name", Flags = PropertyFlag.NonEnumerable)]
    private readonly JsString _name;

    private readonly Realm _realm;

    [JsProperty(Name = "constructor", Flags = PropertyFlag.NonEnumerable)]
    private readonly ErrorConstructor _constructor;

    [JsProperty(Name = "message", Flags = PropertyFlag.NonEnumerable)]
    private static readonly JsString MessageDefault = JsString.Empty;

    internal ErrorPrototype(
        Engine engine,
        Realm realm,
        ErrorConstructor constructor,
        ObjectInstance prototype,
        JsString name)
        : base(engine, ObjectClass.Object)
    {
        _realm = realm;
        _name = name;
        _constructor = constructor;
        _prototype = prototype;
    }

    protected override void Initialize()
    {
        CreateProperties_Generated();

        // The "stack" accessor is an own property of the intrinsic %Error.prototype% only; the
        // NativeError prototypes (TypeError.prototype, RangeError.prototype, ...) inherit it through
        // the prototype chain. The source generator adds it to every ErrorPrototype instance, so
        // remove it from the non-root prototypes — those whose [[Prototype]] is itself an ErrorPrototype
        // (i.e. %Error.prototype%), whereas the root's [[Prototype]] is %Object.prototype%.
        if (_prototype is ErrorPrototype)
        {
            RemoveOwnProperty(CommonProperties.Stack);
        }
    }

    /// <summary>
    /// https://tc39.es/proposal-error-stacks/#sec-get-error.prototype-stack
    /// </summary>
    [JsAccessor("stack")]
    private JsValue StackGet(JsValue thisObject)
    {
        // 1. Let E be the this value.
        // 2. If E is not an Object, throw a TypeError exception.
        if (thisObject is not ObjectInstance)
        {
            Throw.TypeError(_realm, "get Error.prototype.stack called on non-object");
            return Undefined;
        }

        // 3. If E does not have an [[ErrorData]] internal slot, return undefined.
        if (thisObject is not JsError error)
        {
            return Undefined;
        }

        // 4. Return an implementation-defined string that represents the stack trace of E.
        return error._stack ?? JsString.Empty;
    }

    /// <summary>
    /// https://tc39.es/proposal-error-stacks/#sec-set-error.prototype-stack
    /// </summary>
    [JsAccessor("stack", AccessorKind.Set)]
    private JsValue StackSet(JsValue thisObject, JsValue value)
    {
        // 1. Let E be the this value.
        // 2. If E is not an Object, throw a TypeError exception.
        if (thisObject is not ObjectInstance)
        {
            Throw.TypeError(_realm, "set Error.prototype.stack called on non-object");
            return Undefined;
        }

        // 3. If v is not a String, throw a TypeError exception.
        if (!value.IsString())
        {
            Throw.TypeError(_realm, "set Error.prototype.stack called with a non-string value");
            return Undefined;
        }

        // 4. Perform ? SetterThatIgnoresPrototypeProperties(this value, %Error.prototype%, "stack", v).
        SetterThatIgnoresPrototypeProperties(thisObject, this, CommonProperties.Stack, value);

        // 5. Return undefined.
        return Undefined;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-setterthatignoresprototypeproperties
    /// </summary>
    private void SetterThatIgnoresPrototypeProperties(JsValue thisValue, ObjectInstance home, JsValue p, JsValue v)
    {
        // 1. If this is not an Object, throw a TypeError exception.
        if (thisValue is not ObjectInstance objectInstance)
        {
            Throw.TypeError(_realm, "Error.prototype.stack setter called on non-object");
            return;
        }

        // 2. If SameValue(this, home) is true, throw a TypeError exception.
        if (SameValue(thisValue, home))
        {
            Throw.TypeError(_realm, "Cannot set the \"stack\" property directly on %Error.prototype%");
            return;
        }

        // 3. Let desc be ? this.[[GetOwnProperty]](p).
        var desc = objectInstance.GetOwnProperty(p);

        // 4. If desc is undefined, perform ? CreateDataPropertyOrThrow(this, p, v).
        if (desc == PropertyDescriptor.Undefined)
        {
            objectInstance.CreateDataPropertyOrThrow(p, v);
        }
        else
        {
            // 5. Else, perform ? Set(this, p, v, true).
            objectInstance.Set(p, v, throwOnError: true);
        }
    }

    [JsFunction]
    public JsValue ToString(JsValue thisObject)
    {
        var o = thisObject.TryCast<ObjectInstance>();
        if (o is null)
        {
            Throw.TypeError(_realm, $"Method Error.prototype.toString called on incompatible receiver {thisObject}");
        }

        var nameProp = o.Get("name", this);
        var name = nameProp.IsUndefined() ? "Error" : TypeConverter.ToString(nameProp);

        var msgProp = o.Get("message", this);
        string msg = msgProp.IsUndefined() ? "" : TypeConverter.ToString(msgProp);

        if (name == "")
        {
            return msg;
        }
        if (msg == "")
        {
            return name;
        }
        return name + ": " + msg;
    }
}
