using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.Native.Intl;

/// <summary>
/// https://tc39.es/ecma402/#sec-properties-of-intl-listformat-prototype-object
/// </summary>
[JsObject(UseShape = true)]
internal sealed partial class ListFormatPrototype : Prototype
{
    [JsProperty(Name = "constructor", Flags = PropertyFlag.NonEnumerable)]
    private readonly ListFormatConstructor _constructor;

    [JsSymbol("ToStringTag", Flags = PropertyFlag.Configurable)] private static readonly JsString ListFormatToStringTag = new("Intl.ListFormat");

    public ListFormatPrototype(
        Engine engine,
        Realm realm,
        ListFormatConstructor constructor,
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
    /// https://tc39.es/ecma402/#sec-Intl.ListFormat.prototype.format
    /// </summary>
    [JsFunction]
    private JsValue Format(JsValue thisObject, JsValue list)
    {
        var listFormat = ValidateListFormat(thisObject);
        var stringList = StringListFromIterable(list);
        return listFormat.Format(stringList);
    }

    /// <summary>
    /// https://tc39.es/ecma402/#sec-Intl.ListFormat.prototype.formatToParts
    /// </summary>
    [JsFunction]
    private JsArray FormatToParts(JsValue thisObject, JsValue list)
    {
        var listFormat = ValidateListFormat(thisObject);
        var stringList = StringListFromIterable(list);
        return listFormat.FormatToParts(Engine, stringList);
    }

    /// <summary>
    /// https://tc39.es/ecma402/#sec-Intl.ListFormat.prototype.resolvedoptions
    /// </summary>
    [JsFunction]
    private JsObject ResolvedOptions(JsValue thisObject)
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
    /// GetIterator once, then IteratorStepValue until done, so every step calls the [[NextMethod]] GetIteratorFromMethod
    /// read and never reads <c>next</c> again; a string and an array are iterated through their <c>@@iterator</c> like
    /// anything else. A non-String value closes the iterator under a TypeError, which wins over anything the close throws.
    /// Until sebastienros/jint#4438 this read <c>next</c> on every step, walked a string by code unit and an array by
    /// index, took an object without <c>@@iterator</c> for an array-like, and let a throwing <c>return</c> replace the error.
    /// </summary>
    private string[] StringListFromIterable(JsValue iterable)
    {
        // 1. If iterable is undefined, then
        //    a. Return a new empty List.
        if (iterable.IsUndefined())
        {
            return [];
        }

        // 2. Let iteratorRecord be ? GetIterator(iterable, sync).
        var iteratorRecord = iterable.GetIterator(_realm);

        // 3. Let list be a new empty List.
        var list = new List<string>();
        try
        {
            // 4. Repeat,
            //    a. Let next be ? IteratorStepValue(iteratorRecord).
            //    b. If next is done, then
            //       i. Return list.
            // A step that throws, and the one that reports done, leave the record done, so neither is followed by an
            // IteratorClose (https://tc39.es/ecma262/#sec-iteratorstepvalue).
            while (iteratorRecord.TryIteratorStepValue(out var next))
            {
                //    c. If next is not a String, then
                //       i. Let error be ThrowCompletion(a newly created TypeError object).
                if (!next.IsString())
                {
                    Throw.TypeError(_realm, "Iterable yielded a non-String value");
                }

                //    d. Append next to list.
                list.Add(next.AsString());
            }
        }
        catch
        {
            //       ii. Return ? IteratorClose(iteratorRecord, error).
            // Under a throw completion nothing the close raises replaces the TypeError
            // (https://tc39.es/ecma262/#sec-iteratorclose step 5).
            iteratorRecord.CloseIfNotDone(CompletionType.Throw);
            throw;
        }

        return list.ToArray();
    }
}
