using Jint.Native.Function;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.Native.Intl;

/// <summary>
/// https://tc39.es/ecma402/#sec-intl-datetimeformat-constructor
/// </summary>
internal sealed class DateTimeFormatConstructor : Constructor
{
    private static readonly JsString _functionName = new("DateTimeFormat");

    public DateTimeFormatConstructor(
        Engine engine,
        Realm realm,
        FunctionPrototype functionPrototype,
        ObjectPrototype objectPrototype) : base(engine, realm, _functionName)
    {
        _prototype = functionPrototype;
        PrototypeObject = new DateTimeFormatPrototype(engine, realm, this, objectPrototype);
        _length = new PropertyDescriptor(JsNumber.PositiveZero, PropertyFlag.Configurable);
        _prototypeDescriptor = new PropertyDescriptor(PrototypeObject, PropertyFlag.AllForbidden);
    }

    public DateTimeFormatPrototype PrototypeObject { get; }

    public override ObjectInstance Construct(JsCallArguments arguments, JsValue newTarget)
    {
        var locales = arguments.At(0);
        var options = arguments.At(1);

        if (newTarget.IsUndefined())
        {
            newTarget = this;
        }

        var dateTimeFormat = OrdinaryCreateFromConstructor<JsObject, object>(
            newTarget,
            static intrinsics => intrinsics.DateTimeFormat.PrototypeObject,
            static (engine, _, _) => new JsObject(engine));

        InitializeDateTimeFormat(dateTimeFormat, locales, options);
        return dateTimeFormat;
    }

    /// <summary>
    /// https://tc39.es/ecma402/#sec-initializedatetimeformat
    /// </summary>
    private static void InitializeDateTimeFormat(JsObject dateTimeFormat, JsValue locales, JsValue options)
    {
        // TODO
    }
}
