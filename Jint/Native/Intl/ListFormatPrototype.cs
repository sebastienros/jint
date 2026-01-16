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

        result.CreateDataPropertyOrThrow("locale", listFormat.Locale);
        result.CreateDataPropertyOrThrow("type", listFormat.ListType);
        result.CreateDataPropertyOrThrow("style", listFormat.Style);

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

        // Handle strings - iterate over each character
        if (iterable.IsString())
        {
            var str = TypeConverter.ToString(iterable);
            foreach (var c in str)
            {
                list.Add(c.ToString());
            }
            return list.ToArray();
        }

        // Handle array-like objects
        if (iterable is JsArray jsArray)
        {
            var length = jsArray.Length;
            for (uint i = 0; i < length; i++)
            {
                var element = jsArray.Get(i);
                // Per ECMA-402 13.5.2 step 5.a: If Type(next) is not String, throw a TypeError
                if (!element.IsString())
                {
                    Throw.TypeError(_realm, "Iterable yielded a non-String value");
                }
                list.Add(element.AsString());
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
                            // Per ECMA-402 13.5.2 step 5.a: If Type(next) is not String, then
                            // i. Let error be ThrowCompletion(a newly created TypeError object).
                            // ii. Return ? IteratorClose(iteratorRecord, error).
                            if (!value.IsString())
                            {
                                // Call IteratorClose before throwing
                                IteratorClose(iteratorInstance);
                                Throw.TypeError(_realm, "Iterable yielded a non-String value");
                            }
                            list.Add(value.AsString());
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
                    // Per ECMA-402 13.5.2 step 5.a: If Type(next) is not String, throw a TypeError
                    if (!element.IsString())
                    {
                        Throw.TypeError(_realm, "Iterable yielded a non-String value");
                    }
                    list.Add(element.AsString());
                }
            }
        }
        else
        {
            Throw.TypeError(_realm, "Argument is not iterable");
        }

        return list.ToArray();
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-iteratorclose
    /// Calls the iterator's return method if it exists.
    /// </summary>
    private static void IteratorClose(ObjectInstance iterator)
    {
        // 1. Let return be ? GetMethod(iterator, "return").
        var returnMethod = iterator.Get("return");

        // 2. If return is undefined, return NormalCompletion(empty).
        if (returnMethod.IsUndefined() || returnMethod.IsNull())
        {
            return;
        }

        // 3. Call return method
        if (returnMethod is ICallable callable)
        {
            callable.Call(iterator, []);
        }
    }
}
