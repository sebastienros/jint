using Jint.Collections;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;

namespace Jint.Native.Boolean;

/// <summary>
///     http://www.ecma-international.org/ecma-262/5.1/#sec-15.6.4
/// </summary>
internal sealed class BooleanPrototype : BooleanInstance
{
    private readonly Realm _realm;
    private readonly BooleanConstructor _constructor;

    internal BooleanPrototype(
        Engine engine,
        Realm realm,
        BooleanConstructor constructor,
        ObjectPrototype objectPrototype) : base(engine, JsBoolean.False)
    {
        _prototype = objectPrototype;
        _realm = realm;
        _constructor = constructor;
    }

    protected override void Initialize()
    {
        var properties = new PropertyDictionary(3, checkExistingKeys: false)
        {
            ["constructor"] = new PropertyDescriptor(_constructor, PropertyFlag.NonEnumerable),
            ["toString"] = new PropertyDescriptor(new ClrFunction(Engine, "toString", ToBooleanString, 0, PropertyFlag.Configurable), true, false, true),
            ["valueOf"] = new PropertyDescriptor(new ClrFunction(Engine, "valueOf", ValueOf, 0, PropertyFlag.Configurable), true, false, true)
        };
        SetProperties(properties);
    }

    private JsValue ValueOf(JsValue thisObject, JsCallArguments arguments)
    {
        if (thisObject._type == InternalTypes.Boolean)
        {
            return thisObject;
        }

        if (thisObject is BooleanInstance bi)
        {
            return bi.BooleanData;
        }

        ExceptionHelper.ThrowTypeError(_realm);
        return Undefined;
    }

    private JsString ToBooleanString(JsValue thisObject, JsCallArguments arguments)
    {
        var b = ValueOf(thisObject, Arguments.Empty);
        return ((JsBoolean) b)._value ? JsString.TrueString : JsString.FalseString;
    }
}
