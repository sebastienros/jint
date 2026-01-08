using Jint.Native.Object;
using Jint.Native.Symbol;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;

namespace Jint.Native.Intl;

/// <summary>
/// https://tc39.es/ecma402/#sec-properties-of-intl-listformat-prototype-object
/// </summary>
internal sealed class ListFormatPrototype : Prototype
{
    private readonly ListFormatConstructor _constructor;

    public ListFormatPrototype(Engine engine,
        Realm realm,
        ListFormatConstructor constructor,
        ObjectPrototype objectPrototype) : base(engine, realm)
    {
        _prototype = objectPrototype;
        _constructor = constructor;
    }

    protected override void Initialize()
    {
        const PropertyFlag LengthFlags = PropertyFlag.Configurable;
        const PropertyFlag PropertyFlags = PropertyFlag.Writable | PropertyFlag.Configurable;

        var properties = new PropertyDictionary(4, checkExistingKeys: false)
        {
            ["constructor"] = new PropertyDescriptor(_constructor, PropertyFlag.NonEnumerable),
            ["format"] = new PropertyDescriptor(new ClrFunction(Engine, "format", Format, 1, LengthFlags), PropertyFlags),
            ["formatToParts"] = new PropertyDescriptor(new ClrFunction(Engine, "formatToParts", FormatToParts, 1, LengthFlags), PropertyFlags),
            ["resolvedOptions"] = new PropertyDescriptor(new ClrFunction(Engine, "resolvedOptions", ResolvedOptions, 0, LengthFlags), PropertyFlags),
        };
        SetProperties(properties);

        var symbols = new SymbolDictionary(1)
        {
            [GlobalSymbolRegistry.ToStringTag] = new("Intl.ListFormat", PropertyFlag.Configurable)
        };
        SetSymbols(symbols);
    }

    private JsListFormat ValidateListFormat(JsValue thisObject)
    {
        if (thisObject is JsListFormat listFormat)
        {
            return listFormat;
        }

        Throw.TypeError(_realm, "Value is not an Intl.ListFormat");
        return null!; // Never reached
    }

    /// <summary>
    /// https://tc39.es/ecma402/#sec-intl.listformat.prototype.format
    /// </summary>
    private JsValue Format(JsValue thisObject, JsCallArguments arguments)
    {
        var listFormat = ValidateListFormat(thisObject);
        var list = arguments.At(0);

        var stringList = StringListFromIterable(list);
        return listFormat.Format(stringList);
    }

    /// <summary>
    /// https://tc39.es/ecma402/#sec-intl.listformat.prototype.formattoparts
    /// </summary>
    private JsArray FormatToParts(JsValue thisObject, JsCallArguments arguments)
    {
        var listFormat = ValidateListFormat(thisObject);
        var list = arguments.At(0);

        var stringList = StringListFromIterable(list);
        return listFormat.FormatToParts(Engine, stringList);
    }

    /// <summary>
    /// https://tc39.es/ecma402/#sec-intl.listformat.prototype.resolvedoptions
    /// </summary>
    private JsObject ResolvedOptions(JsValue thisObject, JsCallArguments arguments)
    {
        var listFormat = ValidateListFormat(thisObject);

        var result = OrdinaryObjectCreate(Engine, Engine.Realm.Intrinsics.Object.PrototypeObject);

        result.Set("locale", listFormat.Locale);
        result.Set("type", listFormat.ListType);
        result.Set("style", listFormat.Style);

        return result;
    }

    /// <summary>
    /// https://tc39.es/ecma402/#sec-createstringlistfromiterable
    /// </summary>
    private string[] StringListFromIterable(JsValue iterable)
    {
        if (iterable.IsUndefined())
        {
            return [];
        }

        var list = new List<string>();

        // Handle array-like objects
        if (iterable is JsArray jsArray)
        {
            var length = jsArray.Length;
            for (uint i = 0; i < length; i++)
            {
                var element = jsArray.Get(i);
                list.Add(TypeConverter.ToString(element));
            }
        }
        else if (iterable.IsObject())
        {
            // Handle generic iterables
            var obj = iterable.AsObject();
            var iteratorMethod = obj.Get(GlobalSymbolRegistry.Iterator);
            if (!iteratorMethod.IsUndefined() && !iteratorMethod.IsNull())
            {
                if (iteratorMethod is not ICallable callable)
                {
                    Throw.TypeError(_realm, "Iterator is not callable");
                    return null!;
                }

                var iteratorObj = callable.Call(obj, []);
                if (iteratorObj.IsObject())
                {
                    var iteratorInstance = iteratorObj.AsObject();
                    while (true)
                    {
                        var nextMethod = iteratorInstance.Get("next");
                        if (nextMethod is not ICallable nextCallable)
                        {
                            Throw.TypeError(_realm, "Iterator next is not callable");
                            return null!;
                        }

                        var next = nextCallable.Call(iteratorInstance, []);
                        if (next.IsObject())
                        {
                            var done = next.AsObject().Get("done");
                            if (TypeConverter.ToBoolean(done))
                            {
                                break;
                            }

                            var value = next.AsObject().Get("value");
                            list.Add(TypeConverter.ToString(value));
                        }
                        else
                        {
                            break;
                        }
                    }
                }
            }
            else
            {
                // Array-like object with length property
                var length = TypeConverter.ToLength(obj.Get("length"));
                for (ulong i = 0; i < length; i++)
                {
                    var element = obj.Get(i.ToString(System.Globalization.CultureInfo.InvariantCulture));
                    list.Add(TypeConverter.ToString(element));
                }
            }
        }
        else
        {
            Throw.TypeError(_realm, "Argument is not iterable");
        }

        return list.ToArray();
    }
}
