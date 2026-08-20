#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Native.Array;
using Jint.Native.Iterator;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.WebApi.Fetch;

/// <summary>
/// <c>Headers.prototype</c> — the interface prototype object.
/// <para>
/// https://fetch.spec.whatwg.org/#headers-class
/// </para>
/// </summary>
/// <remarks>
/// <para>
/// The IDL declares <c>iterable&lt;ByteString, ByteString&gt;</c>, which is what gives the prototype
/// <c>entries</c>, <c>keys</c>, <c>values</c>, <c>forEach</c> and an <c>@@iterator</c> that is the very same
/// function object as <c>entries</c> — https://webidl.spec.whatwg.org/#es-iterable.
/// </para>
/// <para>
/// The pairs iterated over are the result of <i>sort and combine</i>, so iteration is over distinct
/// lowercased names in ascending byte order with their values joined by <c>", "</c> — whatever order they
/// were appended in — and <c>Set-Cookie</c> is the one name that contributes an entry per value. The list is
/// re-read on every step, as https://webidl.spec.whatwg.org/#es-default-iterator-object specifies, so a
/// callback that mutates the headers is observed.
/// </para>
/// <para>
/// One documented simplification, the same one <c>URL.prototype</c> and <c>Blob.prototype</c> carry: the
/// operations are non-enumerable, where https://webidl.spec.whatwg.org/#es-operations makes an interface's
/// operations enumerable.
/// </para>
/// </remarks>
[JsSymbolAlias("Iterator", "entries")]
[JsObject(UseShape = true)]
internal sealed partial class HeadersPrototype : Prototype
{
    [JsProperty(Name = "constructor", Flags = PropertyFlag.NonEnumerable)]
    private readonly HeadersConstructor _constructor;

    [JsSymbol("ToStringTag", Flags = PropertyFlag.Configurable)]
    private static readonly JsString HeadersToStringTag = new("Headers");

    /// <summary>
    /// The iterator prototype the three iterator-returning members share — per realm, like every other
    /// prototype, and here rather than on <c>Intrinsics</c> because nothing else can reach it.
    /// </summary>
    private HeadersIteratorPrototype? _iteratorPrototype;

    internal HeadersPrototype(
        Engine engine,
        Realm realm,
        HeadersConstructor constructor,
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

    private HeadersIteratorPrototype IteratorPrototypeObject
        => _iteratorPrototype ??= new HeadersIteratorPrototype(_engine, _realm, _realm.Intrinsics.IteratorPrototype);

    /// <summary>
    /// https://fetch.spec.whatwg.org/#dom-headers-append
    /// </summary>
    [JsFunction(Name = "append", Length = 2)]
    private JsValue Append(JsValue thisObject, JsValue name, JsValue value)
    {
        Brand(thisObject).AppendChecked(_realm, name, value);
        return Undefined;
    }

    /// <summary>
    /// https://fetch.spec.whatwg.org/#dom-headers-delete
    /// </summary>
    /// <remarks>
    /// Deleting a name the list does not hold is not an error — the specification's step 5 simply returns.
    /// </remarks>
    [JsFunction(Name = "delete", Length = 1)]
    private JsValue Delete(JsValue thisObject, JsValue name)
    {
        var headers = Brand(thisObject);
        var headerName = FetchValues.ToByteString(_realm, name);

        if (!HeaderList.IsName(headerName))
        {
            Throw.TypeError(_realm, "Failed to execute 'delete' on 'Headers': Invalid name");
        }

        headers.RequireMutable(_realm, "delete");
        headers.List.Delete(headerName);
        return Undefined;
    }

    /// <summary>
    /// https://fetch.spec.whatwg.org/#dom-headers-get — the combined value, or <c>null</c>.
    /// </summary>
    [JsFunction(Name = "get", Length = 1)]
    private JsValue GetHeader(JsValue thisObject, JsValue name)
    {
        var headers = Brand(thisObject);
        var headerName = FetchValues.ToByteString(_realm, name);

        if (!HeaderList.IsName(headerName))
        {
            Throw.TypeError(_realm, "Failed to execute 'get' on 'Headers': Invalid name");
        }

        var value = headers.List.Get(headerName);
        return value is null ? Null : JsString.Create(value);
    }

    /// <summary>
    /// https://fetch.spec.whatwg.org/#dom-headers-getsetcookie — the one place a header list's values are
    /// handed out uncombined, because a <c>Set-Cookie</c> value may itself contain a comma.
    /// </summary>
    [JsFunction(Name = "getSetCookie", Length = 0)]
    private JsArray GetSetCookie(JsValue thisObject)
    {
        var values = Brand(thisObject).List.GetSetCookie();

        var items = new JsValue[values.Count];
        for (var i = 0; i < values.Count; i++)
        {
            items[i] = JsString.Create(values[i]);
        }

        return new JsArray(_engine, items);
    }

    /// <summary>
    /// https://fetch.spec.whatwg.org/#dom-headers-has
    /// </summary>
    [JsFunction(Name = "has", Length = 1)]
    private JsBoolean Has(JsValue thisObject, JsValue name)
    {
        var headers = Brand(thisObject);
        var headerName = FetchValues.ToByteString(_realm, name);

        if (!HeaderList.IsName(headerName))
        {
            Throw.TypeError(_realm, "Failed to execute 'has' on 'Headers': Invalid name");
        }

        return JsBoolean.Create(headers.List.Contains(headerName));
    }

    /// <summary>
    /// https://fetch.spec.whatwg.org/#dom-headers-set — unlike <c>append</c> this replaces, and the header
    /// keeps the position the first one with that name had.
    /// </summary>
    [JsFunction(Name = "set", Length = 2)]
    private JsValue SetHeader(JsValue thisObject, JsValue name, JsValue value)
    {
        var headers = Brand(thisObject);
        var (headerName, headerValue) = JsHeaders.Validate(_realm, "set", name, value);
        headers.RequireMutable(_realm, "set");
        headers.List.Set(headerName, headerValue);
        return Undefined;
    }

    /// <summary>
    /// The default iterator's <c>forEach</c>, https://webidl.spec.whatwg.org/#js-iterable — the callback takes
    /// the value first and the name second, and the pairs are recomputed on every step so a callback that
    /// mutates the list is observed.
    /// </summary>
    [JsFunction(Name = "forEach", Length = 1)]
    private JsValue ForEach(JsValue thisObject, JsValue callback, JsValue thisArg)
    {
        var headers = Brand(thisObject);
        var callable = GetCallable(callback);

        var index = 0;
        while (true)
        {
            var pairs = headers.List.SortAndCombine();
            if (index >= pairs.Count)
            {
                return Undefined;
            }

            var pair = pairs[index];
            index++;
            callable.Call(thisArg, [JsString.Create(pair.Value), JsString.Create(pair.LowerName), headers]);
        }
    }

    /// <summary>
    /// https://webidl.spec.whatwg.org/#js-iterable — <c>entries</c>, and through the alias above also
    /// <c>@@iterator</c>.
    /// </summary>
    [JsFunction(Name = "entries", Length = 0)]
    private IteratorInstance Entries(JsValue thisObject) => IteratorPrototypeObject.ConstructEntryIterator(Brand(thisObject));

    /// <summary>
    /// https://webidl.spec.whatwg.org/#js-iterable
    /// </summary>
    [JsFunction(Name = "keys", Length = 0)]
    private IteratorInstance Keys(JsValue thisObject) => IteratorPrototypeObject.ConstructKeyIterator(Brand(thisObject));

    /// <summary>
    /// https://webidl.spec.whatwg.org/#js-iterable
    /// </summary>
    [JsFunction(Name = "values", Length = 0)]
    private IteratorInstance Values(JsValue thisObject) => IteratorPrototypeObject.ConstructValueIterator(Brand(thisObject));

    /// <summary>
    /// The WebIDL brand check every member performs.
    /// </summary>
    private JsHeaders Brand(JsValue thisObject)
    {
        if (thisObject is JsHeaders headers)
        {
            return headers;
        }

        Throw.TypeError(_realm, "Illegal invocation: receiver is not a Headers");
        return null!;
    }
}
#endif
