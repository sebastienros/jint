using Jint.Native.Object;
using Jint.Native.Symbol;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;

namespace Jint.Native.Intl;

/// <summary>
/// https://tc39.es/ecma402/#sec-properties-of-the-intl-collator-prototype-object
/// </summary>
internal sealed class CollatorPrototype : Prototype
{
    private readonly CollatorConstructor _constructor;

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
        const PropertyFlag LengthFlags = PropertyFlag.Configurable;
        const PropertyFlag PropertyFlags = PropertyFlag.Writable | PropertyFlag.Configurable;

        var properties = new PropertyDictionary(2, checkExistingKeys: false)
        {
            ["constructor"] = new PropertyDescriptor(_constructor, PropertyFlag.NonEnumerable),
            ["resolvedOptions"] = new PropertyDescriptor(new ClrFunction(Engine, "resolvedOptions", ResolvedOptions, 0, LengthFlags), PropertyFlags),
        };
        SetProperties(properties);

        // compare is an accessor property - accessor properties don't have writable attribute
        SetAccessor("compare", new GetSetPropertyDescriptor(
            new ClrFunction(Engine, "get compare", GetCompare, 0, LengthFlags),
            Undefined,
            PropertyFlag.Configurable));

        var symbols = new SymbolDictionary(1)
        {
            [GlobalSymbolRegistry.ToStringTag] = new("Intl.Collator", PropertyFlag.Configurable)
        };
        SetSymbols(symbols);
    }

    private void SetAccessor(string name, GetSetPropertyDescriptor descriptor)
    {
        SetProperty(name, descriptor);
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
    private ClrFunction GetCompare(JsValue thisObject, JsCallArguments arguments)
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
    private JsObject ResolvedOptions(JsValue thisObject, JsCallArguments arguments)
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
