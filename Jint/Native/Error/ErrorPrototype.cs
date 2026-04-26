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

    protected override void Initialize() => CreateProperties_Generated();

    [JsFunction(Length = 0)]
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
