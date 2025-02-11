using Jint.Native.Function;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.Native.Intl;

/// <summary>
/// https://tc39.es/ecma402/#sec-intl-relativetimeformat-constructor
/// </summary>
internal sealed class RelativeTimeFormatConstructor : Constructor
{
    private static readonly JsString _functionName = new("RelativeTimeFormat");

    public RelativeTimeFormatConstructor(
        Engine engine,
        Realm realm,
        FunctionPrototype functionPrototype,
        ObjectPrototype objectPrototype) : base(engine, realm, _functionName)
    {
        _prototype = functionPrototype;
        PrototypeObject = new RelativeTimeFormatPrototype(engine, realm, this, objectPrototype);
        _length = new PropertyDescriptor(JsNumber.PositiveZero, PropertyFlag.Configurable);
        _prototypeDescriptor = new PropertyDescriptor(PrototypeObject, PropertyFlag.AllForbidden);
    }

    public RelativeTimeFormatPrototype PrototypeObject { get; }

    public override ObjectInstance Construct(JsCallArguments arguments, JsValue newTarget)
    {
        var locales = arguments.At(0);
        var options = arguments.At(1);

        if (newTarget.IsUndefined())
        {
            newTarget = this;
        }

        var relativeTimeFormat = OrdinaryCreateFromConstructor<JsObject, object>(
            newTarget,
            static intrinsics => intrinsics.RelativeTimeFormat.PrototypeObject,
            static (engine, _, _) => new JsObject(engine));

        InitializeRelativeTimeFormat(relativeTimeFormat, locales, options);
        return relativeTimeFormat;
    }

    /// <summary>
    /// https://tc39.es/ecma402/#sec-InitializeRelativeTimeFormat
    /// </summary>
    private static void InitializeRelativeTimeFormat(JsObject relativeTimeFormat, JsValue locales, JsValue options)
    {
        // TODO
    }
}
