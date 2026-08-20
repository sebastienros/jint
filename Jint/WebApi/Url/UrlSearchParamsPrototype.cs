#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Native.Iterator;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.WebApi.Url.Parsing;

namespace Jint.WebApi.Url;

/// <summary>
/// <c>URLSearchParams.prototype</c> — the interface prototype object.
/// <para>
/// https://url.spec.whatwg.org/#urlsearchparams
/// </para>
/// </summary>
/// <remarks>
/// <para>
/// The interface's <c>iterable&lt;USVString, USVString&gt;</c> declaration is what gives the prototype
/// <c>entries</c>, <c>keys</c>, <c>values</c>, <c>forEach</c> and an <c>@@iterator</c> that is the very same
/// function object as <c>entries</c> — function identity a script can observe with <c>===</c>, which is why it
/// is a <c>[JsSymbolAlias]</c> rather than a second dispatcher.
/// </para>
/// <para>
/// Every mutating member ends in the update steps, so a <c>URLSearchParams</c> that belongs to a <c>URL</c>
/// rewrites that URL's query as it changes. The two directions are not symmetric in encoding, and deliberately
/// so: this list serializes as application/x-www-form-urlencoded (space becomes <c>+</c>, <c>~</c> becomes
/// <c>%7E</c>) while the URL's own query serializer uses the query percent-encode set — so
/// <c>url.searchParams.sort()</c> can change <c>url.href</c> even when it changes no name and no value. The
/// specification calls that out with the same example.
/// </para>
/// <para>
/// One documented simplification, the same one <c>console</c> and <c>URL.prototype</c> carry: the operations
/// are non-enumerable, where https://webidl.spec.whatwg.org/#es-operations makes an interface's operations
/// enumerable. The <c>size</c> attribute is enumerable and configurable as WebIDL specifies.
/// </para>
/// </remarks>
[JsSymbolAlias("Iterator", "entries")]
[JsObject(UseShape = true)]
internal sealed partial class UrlSearchParamsPrototype : Prototype
{
    [JsProperty(Name = "constructor", Flags = PropertyFlag.NonEnumerable)]
    private readonly UrlSearchParamsConstructor _constructor;

    [JsSymbol("ToStringTag", Flags = PropertyFlag.Configurable)]
    private static readonly JsString UrlSearchParamsToStringTag = new("URLSearchParams");

    /// <summary>
    /// The iterator prototype the three iterator-returning members share. It is per realm like every other
    /// prototype, and lives here rather than on <c>Intrinsics</c> because nothing else can reach it.
    /// </summary>
    private UrlSearchParamsIteratorPrototype? _iteratorPrototype;

    internal UrlSearchParamsPrototype(
        Engine engine,
        Realm realm,
        UrlSearchParamsConstructor constructor,
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

    private UrlSearchParamsIteratorPrototype IteratorPrototypeObject
        => _iteratorPrototype ??= new UrlSearchParamsIteratorPrototype(_engine, _realm, _realm.Intrinsics.IteratorPrototype);

    /// <summary>
    /// https://url.spec.whatwg.org/#dom-urlsearchparams-size
    /// </summary>
    [JsAccessor("size", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsNumber SizeGet(JsValue thisObject) => JsNumber.Create(Brand(thisObject).List.Count);

    /// <summary>
    /// https://url.spec.whatwg.org/#dom-urlsearchparams-append
    /// </summary>
    [JsFunction(Name = "append", Length = 2)]
    private JsValue Append(JsValue thisObject, JsValue name, JsValue value)
    {
        var parameters = Brand(thisObject);
        parameters.List.Add(new FormUrlEncodedEntry(UrlValues.ToUsvString(name), UrlValues.ToUsvString(value)));
        parameters.Update();
        return Undefined;
    }

    /// <summary>
    /// https://url.spec.whatwg.org/#dom-urlsearchparams-delete
    /// </summary>
    [JsFunction(Name = "delete", Length = 1)]
    private JsValue Delete(JsValue thisObject, JsValue name, JsValue value)
    {
        var parameters = Brand(thisObject);
        var target = UrlValues.ToUsvString(name);
        var targetValue = UrlValues.ToOptionalUsvString(value);

        parameters.List.RemoveAll(entry =>
            string.Equals(entry.Name, target, StringComparison.Ordinal)
            && (targetValue is null || string.Equals(entry.Value, targetValue, StringComparison.Ordinal)));

        parameters.Update();
        return Undefined;
    }

    /// <summary>
    /// https://url.spec.whatwg.org/#dom-urlsearchparams-get
    /// </summary>
    [JsFunction(Name = "get", Length = 1)]
    private JsValue GetValue(JsValue thisObject, JsValue name)
    {
        var parameters = Brand(thisObject);
        var target = UrlValues.ToUsvString(name);

        foreach (var entry in parameters.List)
        {
            if (string.Equals(entry.Name, target, StringComparison.Ordinal))
            {
                return JsString.Create(entry.Value);
            }
        }

        return Null;
    }

    /// <summary>
    /// https://url.spec.whatwg.org/#dom-urlsearchparams-getall
    /// </summary>
    [JsFunction(Name = "getAll", Length = 1)]
    private JsArray GetAll(JsValue thisObject, JsValue name)
    {
        var parameters = Brand(thisObject);
        var target = UrlValues.ToUsvString(name);

        var values = new List<JsValue>();
        foreach (var entry in parameters.List)
        {
            if (string.Equals(entry.Name, target, StringComparison.Ordinal))
            {
                values.Add(JsString.Create(entry.Value));
            }
        }

        return new JsArray(_engine, values.ToArray());
    }

    /// <summary>
    /// https://url.spec.whatwg.org/#dom-urlsearchparams-has
    /// </summary>
    [JsFunction(Name = "has", Length = 1)]
    private JsBoolean Has(JsValue thisObject, JsValue name, JsValue value)
    {
        var parameters = Brand(thisObject);
        var target = UrlValues.ToUsvString(name);
        var targetValue = UrlValues.ToOptionalUsvString(value);

        foreach (var entry in parameters.List)
        {
            if (string.Equals(entry.Name, target, StringComparison.Ordinal)
                && (targetValue is null || string.Equals(entry.Value, targetValue, StringComparison.Ordinal)))
            {
                return JsBoolean.True;
            }
        }

        return JsBoolean.False;
    }

    /// <summary>
    /// https://url.spec.whatwg.org/#dom-urlsearchparams-set
    /// </summary>
    [JsFunction(Name = "set", Length = 2)]
    private JsValue SetValue(JsValue thisObject, JsValue name, JsValue value)
    {
        var parameters = Brand(thisObject);
        var target = UrlValues.ToUsvString(name);
        var targetValue = UrlValues.ToUsvString(value);

        var list = parameters.List;
        var first = -1;
        for (var i = 0; i < list.Count; i++)
        {
            if (!string.Equals(list[i].Name, target, StringComparison.Ordinal))
            {
                continue;
            }

            if (first < 0)
            {
                first = i;
                list[i] = new FormUrlEncodedEntry(target, targetValue);
                continue;
            }

            list.RemoveAt(i);
            i--;
        }

        if (first < 0)
        {
            list.Add(new FormUrlEncodedEntry(target, targetValue));
        }

        parameters.Update();
        return Undefined;
    }

    /// <summary>
    /// https://url.spec.whatwg.org/#dom-urlsearchparams-sort
    /// </summary>
    /// <remarks>
    /// The sort is by name only and must be stable — "sorting in ascending order" in Infra is a stable sort,
    /// and tuples with equal names keep their relative order, which is what makes <c>getAll</c> survive a
    /// <c>sort</c>. <c>List&lt;T&gt;.Sort</c> is an unstable introsort, so this goes through the engine's own
    /// merge sort instead. "Code unit less than" is an ordinal comparison of UTF-16 code units.
    /// </remarks>
    [JsFunction(Name = "sort", Length = 0)]
    private JsValue Sort(JsValue thisObject)
    {
        var parameters = Brand(thisObject);
        var sorted = parameters.List.StableOrder(_nameComparer);
        parameters.List.Clear();
        parameters.List.AddRange(sorted);
        parameters.Update();
        return Undefined;
    }

    private static readonly IComparer<FormUrlEncodedEntry> _nameComparer = new NameComparer();

    private sealed class NameComparer : IComparer<FormUrlEncodedEntry>
    {
        public int Compare(FormUrlEncodedEntry x, FormUrlEncodedEntry y) => string.CompareOrdinal(x.Name, y.Name);
    }

    /// <summary>
    /// The default iterator's <c>forEach</c>, https://webidl.spec.whatwg.org/#js-iterable — the callback takes
    /// the value first and the name second, and the list is re-read on every step so a callback that mutates
    /// it is observed.
    /// </summary>
    [JsFunction(Name = "forEach", Length = 1)]
    private JsValue ForEach(JsValue thisObject, JsValue callback, JsValue thisArg)
    {
        var parameters = Brand(thisObject);
        var callable = GetCallable(callback);

        for (var i = 0; i < parameters.List.Count; i++)
        {
            var entry = parameters.List[i];
            callable.Call(thisArg, [JsString.Create(entry.Value), JsString.Create(entry.Name), parameters]);
        }

        return Undefined;
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
    /// The anonymous stringifier, https://url.spec.whatwg.org/#dom-urlsearchparams-stringification-behavior.
    /// </summary>
    [JsFunction(Name = "toString", Length = 0)]
    private JsString Stringify(JsValue thisObject) => JsString.Create(FormUrlEncoded.Serialize(Brand(thisObject).List));

    /// <summary>
    /// The WebIDL brand check every member performs.
    /// </summary>
    private JsUrlSearchParams Brand(JsValue thisObject)
    {
        if (thisObject is JsUrlSearchParams parameters)
        {
            return parameters;
        }

        Throw.TypeError(_realm, "Illegal invocation: receiver is not a URLSearchParams");
        return null!;
    }
}
#endif
