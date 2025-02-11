using Jint.Native.Function;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.Native.Intl;

/// <summary>
/// https://tc39.es/ecma402/#sec-intl-locale-constructor
/// </summary>
internal sealed class LocaleConstructor : Constructor
{
    private static readonly JsString _functionName = new("Locale");

    public LocaleConstructor(
        Engine engine,
        Realm realm,
        FunctionPrototype functionPrototype,
        ObjectPrototype objectPrototype) : base(engine, realm, _functionName)
    {
        _prototype = functionPrototype;
        PrototypeObject = new LocalePrototype(engine, realm, this, objectPrototype);
        _length = new PropertyDescriptor(JsNumber.PositiveZero, PropertyFlag.Configurable);
        _prototypeDescriptor = new PropertyDescriptor(PrototypeObject, PropertyFlag.AllForbidden);
    }

    public LocalePrototype PrototypeObject { get; }

    public override ObjectInstance Construct(JsCallArguments arguments, JsValue newTarget)
    {
        throw new NotImplementedException();
    }
}
