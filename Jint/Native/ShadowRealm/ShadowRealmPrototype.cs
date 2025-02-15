using Jint.Collections;
using Jint.Native.Object;
using Jint.Native.Symbol;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;

namespace Jint.Native.ShadowRealm;

/// <summary>
/// https://tc39.es/proposal-shadowrealm/#sec-properties-of-the-shadowrealm-prototype-object
/// </summary>
internal sealed class ShadowRealmPrototype : Prototype
{
    private readonly ShadowRealmConstructor _constructor;

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
        const PropertyFlag propertyFlags = PropertyFlag.Configurable | PropertyFlag.Writable;
        var properties = new PropertyDictionary(5, checkExistingKeys: false)
        {
            ["length"] = new PropertyDescriptor(0, PropertyFlag.Configurable),
            ["constructor"] = new PropertyDescriptor(_constructor, PropertyFlag.NonEnumerable),
            ["evaluate"] = new PropertyDescriptor(new ClrFunction(Engine, "evaluate", Evaluate, 1, PropertyFlag.Configurable), propertyFlags),
            ["importValue"] = new PropertyDescriptor(new ClrFunction(Engine, "importValue", ImportValue, 2, PropertyFlag.Configurable), propertyFlags),
        };
        SetProperties(properties);

        var symbols = new SymbolDictionary(1) { [GlobalSymbolRegistry.ToStringTag] = new PropertyDescriptor("ShadowRealm", false, false, true) };
        SetSymbols(symbols);
    }

    /// <summary>
    /// https://tc39.es/proposal-shadowrealm/#sec-shadowrealm.prototype.evaluate
    /// </summary>
    private JsValue Evaluate(JsValue thisObject, JsCallArguments arguments)
    {
        var shadowRealm = ValidateShadowRealmObject(thisObject);
        var sourceText = arguments.At(0);

        if (!sourceText.IsString())
        {
            ExceptionHelper.ThrowTypeError(_realm, "Invalid source text " + sourceText);
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
    private JsValue ImportValue(JsValue thisObject, JsCallArguments arguments)
    {
        var specifier = arguments.At(0);
        var exportName = arguments.At(1);

        var O = ValidateShadowRealmObject(thisObject);
        var specifierString = TypeConverter.ToJsString(specifier);
        if (!specifier.IsString())
        {
            ExceptionHelper.ThrowTypeError(_realm, "Invalid specifier");
        }

        if (!exportName.IsString())
        {
            ExceptionHelper.ThrowTypeError(_realm, "Invalid exportName");
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

        ExceptionHelper.ThrowTypeError(_realm, "object must be a ShadowRealm");
        return default;
    }
}
