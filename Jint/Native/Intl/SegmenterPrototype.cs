using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.Native.Intl;

/// <summary>
/// https://tc39.es/ecma402/#sec-properties-of-intl-segmenter-prototype-object
/// </summary>
[JsObject]
internal sealed partial class SegmenterPrototype : Prototype
{
    [JsProperty(Name = "constructor", Flags = PropertyFlag.NonEnumerable)]
    private readonly SegmenterConstructor _constructor;

    [JsSymbol("ToStringTag", Flags = PropertyFlag.Configurable)] private static readonly JsString SegmenterToStringTag = new("Intl.Segmenter");

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
        CreateProperties_Generated();
        CreateSymbols_Generated();
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
    [JsFunction(Length = 1)]
    private JsSegments Segment(JsValue thisObject, JsValue input)
    {
        var segmenter = ValidateSegmenter(thisObject);
        var stringInput = TypeConverter.ToString(input);
        return segmenter.Segment(_engine, stringInput);
    }

    /// <summary>
    /// https://tc39.es/ecma402/#sec-intl.segmenter.prototype.resolvedoptions
    /// </summary>
    [JsFunction(Length = 0)]
    private JsObject ResolvedOptions(JsValue thisObject)
    {
        var segmenter = ValidateSegmenter(thisObject);

        var result = OrdinaryObjectCreate(Engine, Engine.Realm.Intrinsics.Object.PrototypeObject);

        result.CreateDataPropertyOrThrow("locale", segmenter.Locale);
        result.CreateDataPropertyOrThrow("granularity", segmenter.Granularity);

        return result;
    }
}
