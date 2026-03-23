using Jint.Native.Object;
using Jint.Native.Symbol;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;

namespace Jint.Native.Intl;

/// <summary>
/// https://tc39.es/ecma402/#sec-properties-of-intl-segmenter-prototype-object
/// </summary>
internal sealed class SegmenterPrototype : Prototype
{
    private readonly SegmenterConstructor _constructor;

    public SegmenterPrototype(
        Engine engine,
        Realm realm,
        SegmenterConstructor constructor,
        ObjectPrototype objectPrototype) : base(engine, realm)
    {
        _prototype = objectPrototype;
        _constructor = constructor;
    }

    protected override void Initialize()
    {
        const PropertyFlag LengthFlags = PropertyFlag.Configurable;
        const PropertyFlag PropertyFlags = PropertyFlag.Writable | PropertyFlag.Configurable;

        var properties = new PropertyDictionary(3, checkExistingKeys: false)
        {
            ["constructor"] = new PropertyDescriptor(_constructor, PropertyFlag.NonEnumerable),
            ["segment"] = new PropertyDescriptor(new ClrFunction(Engine, "segment", Segment, 1, LengthFlags), PropertyFlags),
            ["resolvedOptions"] = new PropertyDescriptor(new ClrFunction(Engine, "resolvedOptions", ResolvedOptions, 0, LengthFlags), PropertyFlags),
        };
        SetProperties(properties);

        var symbols = new SymbolDictionary(1)
        {
            [GlobalSymbolRegistry.ToStringTag] = new("Intl.Segmenter", PropertyFlag.Configurable)
        };
        SetSymbols(symbols);
    }

    private JsSegmenter ValidateSegmenter(JsValue thisObject)
    {
        if (thisObject is JsSegmenter segmenter)
        {
            return segmenter;
        }

        Throw.TypeError(_realm, "Value is not an Intl.Segmenter");
        return null!; // Never reached
    }

    /// <summary>
    /// https://tc39.es/ecma402/#sec-intl.segmenter.prototype.segment
    /// </summary>
    private JsSegments Segment(JsValue thisObject, JsCallArguments arguments)
    {
        var segmenter = ValidateSegmenter(thisObject);
        var input = arguments.At(0);

        var stringInput = TypeConverter.ToString(input);
        return segmenter.Segment(_engine, stringInput);
    }

    /// <summary>
    /// https://tc39.es/ecma402/#sec-intl.segmenter.prototype.resolvedoptions
    /// </summary>
    private JsObject ResolvedOptions(JsValue thisObject, JsCallArguments arguments)
    {
        var segmenter = ValidateSegmenter(thisObject);

        var result = OrdinaryObjectCreate(Engine, Engine.Realm.Intrinsics.Object.PrototypeObject);

        result.CreateDataPropertyOrThrow("locale", segmenter.Locale);
        result.CreateDataPropertyOrThrow("granularity", segmenter.Granularity);

        return result;
    }
}
