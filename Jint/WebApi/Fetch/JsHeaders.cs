#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Native.Iterator;
using Jint.Native.Object;
using Jint.Native.Symbol;
using Jint.Runtime;

namespace Jint.WebApi.Fetch;

/// <summary>
/// A <c>Headers</c> instance: the JavaScript face of a <see cref="HeaderList"/>.
/// <para>
/// https://fetch.spec.whatwg.org/#headers-class
/// </para>
/// </summary>
/// <remarks>
/// The whole state is the header list and its guard, both of which live on <see cref="List"/>; the instance
/// itself carries no own property, which is what a browser reports for
/// <c>Object.getOwnPropertyNames(new Headers())</c>. <see cref="HeadersPrototype"/> reaches the list through
/// a brand check.
/// </remarks>
internal sealed class JsHeaders : ObjectInstance
{
    internal JsHeaders(Engine engine, HeaderList list) : base(engine, ObjectClass.Object)
    {
        List = list;
    }

    /// <summary>https://fetch.spec.whatwg.org/#concept-headers-header-list</summary>
    internal HeaderList List { get; }

    /// <summary>
    /// The header list fill, https://fetch.spec.whatwg.org/#concept-headers-fill — the <c>HeadersInit</c>
    /// union's two arms.
    /// </summary>
    /// <remarks>
    /// The union is resolved the way https://webidl.spec.whatwg.org/#es-union says: an object with a callable
    /// <c>@@iterator</c> is the sequence arm, anything else the record arm. So a <c>Map</c> and a
    /// <c>Headers</c> both fill pairwise, while a plain object fills from its own enumerable string-keyed
    /// properties.
    /// </remarks>
    internal void Fill(Realm realm, JsValue init)
    {
        if (init is not ObjectInstance initObject)
        {
            Throw.TypeError(realm, "Failed to construct 'Headers': The provided value is not of type '(sequence<sequence<ByteString>> or record<ByteString, ByteString>)'");
            return;
        }

        var iteratorMethod = initObject.Get(GlobalSymbolRegistry.Iterator);
        if (iteratorMethod is ICallable)
        {
            FillFromSequence(realm, initObject);
            return;
        }

        FillFromRecord(realm, initObject);
    }

    /// <summary>
    /// "If init is a sequence, then for each header of init: if header's size is not 2, then throw a
    /// TypeError; append (header[0], header[1])."
    /// </summary>
    private void FillFromSequence(Realm realm, ObjectInstance init)
    {
        var outer = init.GetIterator(realm);
        while (outer.TryIteratorStepValue(out var header))
        {
            var pair = ReadPair(realm, outer, header);
            AppendChecked(realm, pair.Name, pair.Value);
        }
    }

    private static (JsValue Name, JsValue Value) ReadPair(Realm realm, IteratorInstance outer, JsValue header)
    {
        if (header is not ObjectInstance)
        {
            outer.Close(CompletionType.Throw);
            Throw.TypeError(realm, "Failed to construct 'Headers': The provided value cannot be converted to a sequence");
        }

        var inner = header.GetIterator(realm);
        if (!inner.TryIteratorStepValue(out var name) || !inner.TryIteratorStepValue(out var value))
        {
            outer.Close(CompletionType.Throw);
            Throw.TypeError(realm, "Failed to construct 'Headers': Invalid value — each header must be a name/value pair");
            return default;
        }

        // A third element makes the sequence's size not 2, which the fill step refuses outright.
        if (inner.TryIteratorStepValue(out _))
        {
            inner.Close(CompletionType.Throw);
            outer.Close(CompletionType.Throw);
            Throw.TypeError(realm, "Failed to construct 'Headers': Invalid value — each header must be a name/value pair");
        }

        return (name, value);
    }

    /// <summary>
    /// "Otherwise, init is a record: for each key → value of init, append (key, value)." A record's members
    /// are its own enumerable string-keyed properties, in own-property order —
    /// https://webidl.spec.whatwg.org/#es-record.
    /// </summary>
    private void FillFromRecord(Realm realm, ObjectInstance init)
    {
        foreach (var key in init.GetOwnPropertyKeys(Types.String))
        {
            var descriptor = init.GetOwnProperty(key);
            if (!descriptor.Enumerable)
            {
                continue;
            }

            AppendChecked(realm, key, init.Get(key));
        }
    }

    /// <summary>
    /// The <c>append</c> operation, https://fetch.spec.whatwg.org/#dom-headers-append, shared by the fill
    /// above and by <c>Headers.prototype.append</c>.
    /// </summary>
    internal void AppendChecked(Realm realm, JsValue name, JsValue value)
    {
        var (headerName, headerValue) = Validate(realm, "append", name, value);
        RequireMutable(realm, "append");
        List.Append(headerName, headerValue);
    }

    /// <summary>
    /// Steps 1 and 2 of every mutating operation: normalize the value, then refuse a name that is not a token
    /// or a value carrying NUL, CR or LF.
    /// </summary>
    internal static (string Name, string Value) Validate(Realm realm, string operation, JsValue name, JsValue value)
    {
        var headerName = FetchValues.ToByteString(realm, name);
        var headerValue = HeaderList.Normalize(FetchValues.ToByteString(realm, value));

        if (!HeaderList.IsName(headerName))
        {
            Throw.TypeError(realm, $"Failed to execute '{operation}' on 'Headers': Invalid name");
        }

        if (!HeaderList.IsValue(headerValue))
        {
            Throw.TypeError(realm, $"Failed to execute '{operation}' on 'Headers': Invalid value");
        }

        return (headerName, headerValue);
    }

    /// <summary>
    /// The guard check every mutating operation performs,
    /// https://fetch.spec.whatwg.org/#concept-headers-append step 3.
    /// </summary>
    internal void RequireMutable(Realm realm, string operation)
    {
        if (List.Guard == HeadersGuard.Immutable)
        {
            Throw.TypeError(realm, $"Failed to execute '{operation}' on 'Headers': Headers are immutable");
        }
    }
}
#endif
