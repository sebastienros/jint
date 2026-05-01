using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;

namespace Jint.Native.Intl;

/// <summary>
/// https://tc39.es/ecma402/#sec-properties-of-the-intl-collator-prototype-object
/// </summary>
[JsObject]
internal sealed partial class CollatorPrototype : Prototype
{
    [JsProperty(Name = "constructor", Flags = PropertyFlag.NonEnumerable)]
    private readonly CollatorConstructor _constructor;

    [JsSymbol("ToStringTag", Flags = PropertyFlag.Configurable)] private static readonly JsString CollatorToStringTag = new("Intl.Collator");

    public CollatorPrototype(Engine engine,
        Realm realm,
        CollatorConstructor constructor,
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

    private JsCollator ValidateCollator(JsValue thisObject)
    {
        if (thisObject is JsCollator collator)
        {
            return collator;
        }

        Throw.TypeError(_realm, "Value is not an Intl.Collator");
        return null!; // Never reached
    }

    /// <summary>
    /// https://tc39.es/ecma402/#sec-intl.collator.prototype.compare
    /// </summary>
    [JsAccessor("compare")]
    private ClrFunction GetCompare(JsValue thisObject)
    {
        var collator = ValidateCollator(thisObject);

        // Return a bound compare function
        // The spec requires this to be a bound function stored on the collator
        // For simplicity, we create a new function each time
        return new ClrFunction(Engine, "", (_, args) =>
        {
            var x = TypeConverter.ToString(args.At(0));
            var y = TypeConverter.ToString(args.At(1));
            return collator.Compare(x, y);
        }, 2, PropertyFlag.Configurable);
    }

    /// <summary>
    /// https://tc39.es/ecma402/#sec-intl.collator.prototype.resolvedoptions
    /// </summary>
    [JsFunction(Length = 0)]
    private JsObject ResolvedOptions(JsValue thisObject)
    {
        var collator = ValidateCollator(thisObject);

        var result = OrdinaryObjectCreate(Engine, Engine.Realm.Intrinsics.Object.PrototypeObject);

        result.CreateDataPropertyOrThrow("locale", collator.Locale);
        result.CreateDataPropertyOrThrow("usage", collator.Usage);
        result.CreateDataPropertyOrThrow("sensitivity", collator.Sensitivity);
        result.CreateDataPropertyOrThrow("ignorePunctuation", collator.IgnorePunctuation);
        result.CreateDataPropertyOrThrow("collation", collator.Collation);
        result.CreateDataPropertyOrThrow("numeric", collator.Numeric);
        result.CreateDataPropertyOrThrow("caseFirst", collator.CaseFirst);

        return result;
    }
}
