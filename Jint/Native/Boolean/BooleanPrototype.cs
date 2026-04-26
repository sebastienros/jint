using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.Native.Boolean;

/// <summary>
///     http://www.ecma-international.org/ecma-262/5.1/#sec-15.6.4
/// </summary>
[JsObject]
internal sealed partial class BooleanPrototype : BooleanInstance
{
    private readonly Realm _realm;

    [JsProperty(Name = "constructor", Flags = PropertyFlag.NonEnumerable)]
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

    protected override void Initialize() => CreateProperties_Generated();

    [JsFunction(Length = 0)]
    private JsValue ValueOf(JsValue thisObject)
    {
        if (thisObject._type == InternalTypes.Boolean)
        {
            return thisObject;
        }

        if (thisObject is BooleanInstance bi)
        {
            return bi.BooleanData;
        }

        Throw.TypeError(_realm, "Boolean.prototype.valueOf requires that 'this' be a Boolean");
        return Undefined;
    }

    [JsFunction(Length = 0, Name = "toString")]
    private JsString ToBooleanString(JsValue thisObject)
    {
        var b = ValueOf(thisObject);
        return ((JsBoolean) b)._value ? JsString.TrueString : JsString.FalseString;
    }
}
