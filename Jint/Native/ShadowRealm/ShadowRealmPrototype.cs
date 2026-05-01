using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.Native.ShadowRealm;

/// <summary>
/// https://tc39.es/proposal-shadowrealm/#sec-properties-of-the-shadowrealm-prototype-object
/// </summary>
[JsObject]
internal sealed partial class ShadowRealmPrototype : Prototype
{
    [JsProperty(Name = "constructor", Flags = PropertyFlag.NonEnumerable)]
    private readonly ShadowRealmConstructor _constructor;

    [JsSymbol("ToStringTag", Flags = PropertyFlag.Configurable)] private static readonly JsString ShadowRealmToStringTag = new("ShadowRealm");

    internal ShadowRealmPrototype(
        Engine engine,
        Realm realm,
        ShadowRealmConstructor constructor,
        ObjectPrototype prototype) : base(engine, realm)
    {
        _prototype = prototype;
        _constructor = constructor;
    }

    protected override void Initialize()
    {
        CreateProperties_Generated();
        CreateSymbols_Generated();
    }

    /// <summary>
    /// https://tc39.es/proposal-shadowrealm/#sec-shadowrealm.prototype.evaluate
    /// </summary>
    [JsFunction(Length = 1)]
    private JsValue Evaluate(JsValue thisObject, JsValue sourceText)
    {
        var shadowRealm = ValidateShadowRealmObject(thisObject);

        if (!sourceText.IsString())
        {
            Throw.TypeError(_realm, "Invalid source text " + sourceText);
        }

        var parserOptions = _engine.GetActiveParserOptions();
        // Just like in the case of eval, we don't allow top level returns.
        var adjustedParserOptions = parserOptions.AllowReturnOutsideFunction
            ? parserOptions with { AllowReturnOutsideFunction = false }
            : parserOptions;
        var parser = _engine.GetParserFor(adjustedParserOptions);

        return shadowRealm.PerformShadowRealmEval(sourceText.AsString(), parserOptions, parser, _realm);
    }

    /// <summary>
    /// https://tc39.es/proposal-shadowrealm/#sec-shadowrealm.prototype.importvalue
    /// </summary>
    [JsFunction(Length = 2)]
    private JsValue ImportValue(JsValue thisObject, JsValue specifier, JsValue exportName)
    {
        var O = ValidateShadowRealmObject(thisObject);
        var specifierString = TypeConverter.ToJsString(specifier);
        if (!specifier.IsString())
        {
            Throw.TypeError(_realm, "Invalid specifier");
        }

        if (!exportName.IsString())
        {
            Throw.TypeError(_realm, "Invalid exportName");
        }

        var callerRealm = _realm;
        return O.ShadowRealmImportValue(specifierString.ToString(), exportName.ToString(), callerRealm);
    }

    private ShadowRealm ValidateShadowRealmObject(JsValue thisObject)
    {
        if (thisObject is ShadowRealm shadowRealm)
        {
            return shadowRealm;
        }

        Throw.TypeError(_realm, "object must be a ShadowRealm");
        return default;
    }
}
