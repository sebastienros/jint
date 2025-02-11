using Jint.Native.Function;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.Native.Intl;

/// <summary>
/// https://tc39.es/ecma402/#sec-intl-pluralrules-constructor
/// </summary>
internal sealed class PluralRulesConstructor : Constructor
{
    private static readonly JsString _functionName = new("PluralRules");

    public PluralRulesConstructor(
        Engine engine,
        Realm realm,
        FunctionPrototype functionPrototype,
        ObjectPrototype objectPrototype) : base(engine, realm, _functionName)
    {
        _prototype = functionPrototype;
        PrototypeObject = new PluralRulesPrototype(engine, realm, this, objectPrototype);
        _length = new PropertyDescriptor(JsNumber.PositiveZero, PropertyFlag.Configurable);
        _prototypeDescriptor = new PropertyDescriptor(PrototypeObject, PropertyFlag.AllForbidden);
    }

    public PluralRulesPrototype PrototypeObject { get; }

    public override ObjectInstance Construct(JsCallArguments arguments, JsValue newTarget)
    {
        var locales = arguments.At(0);
        var options = arguments.At(1);

        if (newTarget.IsUndefined())
        {
            newTarget = this;
        }

        var pluralRules = OrdinaryCreateFromConstructor<JsObject, object>(
            newTarget,
            static intrinsics => intrinsics.PluralRules.PrototypeObject,
            static (engine, _, _) => new JsObject(engine));

        InitializePluralRules(pluralRules, locales, options);
        return pluralRules;
    }

    /// <summary>
    /// https://tc39.es/ecma402/#sec-initializepluralrules
    /// </summary>
    private static void InitializePluralRules(JsObject pluralRules, JsValue locales, JsValue options)
    {
        // TODO
    }
}
