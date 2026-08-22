#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Native.Iterator;
using Jint.Native.Object;
using Jint.Native.Symbol;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

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
    /// "Otherwise, init is a record: for each key → value of init, append (key, value)."
    /// </summary>
    /// <remarks>
    /// <para>
    /// The record is converted <i>in full</i> before the fill appends anything, because the conversion is
    /// what turns the argument into a <c>record&lt;ByteString, ByteString&gt;</c> in the first place —
    /// https://webidl.spec.whatwg.org/#es-record. Its step 3 is <c>? O.[[OwnPropertyKeys]]()</c>, with no
    /// filter, so a Symbol key is a member of the walk like any other; and its step 4 is, per key,
    /// </para>
    /// <list type="number">
    /// <item><description><c>[[GetOwnProperty]]</c>, and</description></item>
    /// <item><description>
    /// when the descriptor exists and is enumerable: "let <i>typedKey</i> be <i>key</i> converted to an IDL
    /// value of type <i>K</i>", then "let <i>value</i> be <c>? Get(O, key)</c>", then the value's own
    /// conversion.
    /// </description></item>
    /// </list>
    /// <para>
    /// The <b>key</b> is converted before the <c>Get</c>, which is the whole of what
    /// <c>fetch/api/headers/headers-record.any.js</c> counts through a proxy: <i>K</i> is <c>ByteString</c>,
    /// so a Symbol key — or a string carrying a code unit above 255 — ends the conversion there, and the
    /// <c>Get</c> that would have followed never happens.
    /// </para>
    /// </remarks>
    private void FillFromRecord(Realm realm, ObjectInstance init)
    {
        var keys = init.GetOwnPropertyKeys();
        var record = new List<(string Name, string Value)>(keys.Count);

        foreach (var key in keys)
        {
            var descriptor = init.GetOwnProperty(key);
            if (descriptor == PropertyDescriptor.Undefined || !descriptor.Enumerable)
            {
                continue;
            }

            var name = FetchValues.ToByteString(realm, key);
            record.Add((name, FetchValues.ToByteString(realm, init.Get(key))));
        }

        foreach (var (name, value) in record)
        {
            AppendConverted(realm, name, value);
        }
    }

    /// <summary>
    /// The <c>append</c> operation, https://fetch.spec.whatwg.org/#dom-headers-append, for the fill above,
    /// whose two <c>ByteString</c> conversions belong to the record conversion that precedes it.
    /// </summary>
    private void AppendConverted(Realm realm, string name, string value)
    {
        var (headerName, headerValue) = ValidateConverted(realm, "append", name, value);
        RequireMutable(realm, "append");
        List.Append(headerName, headerValue);
    }

    /// <summary>
    /// The same operation for <c>Headers.prototype.append</c>, where the <c>ByteString</c> conversions are
    /// WebIDL's argument conversions and so happen here.
    /// </summary>
    internal void AppendChecked(Realm realm, JsValue name, JsValue value)
    {
        var (headerName, headerValue) = Validate(realm, "append", name, value);
        RequireMutable(realm, "append");
        List.Append(headerName, headerValue);
    }

    /// <summary>
    /// The <c>ByteString</c> argument conversions, in declaration order, and then <see cref="ValidateConverted"/>.
    /// </summary>
    internal static (string Name, string Value) Validate(Realm realm, string operation, JsValue name, JsValue value)
        => ValidateConverted(realm, operation, FetchValues.ToByteString(realm, name), FetchValues.ToByteString(realm, value));

    /// <summary>
    /// Steps 1 and 2 of every mutating operation: normalize the value, then refuse a name that is not a token
    /// or a value carrying NUL, CR or LF.
    /// </summary>
    private static (string Name, string Value) ValidateConverted(Realm realm, string operation, string name, string value)
    {
        var headerValue = HeaderList.Normalize(value);

        if (!HeaderList.IsName(name))
        {
            Throw.TypeError(realm, $"Failed to execute '{operation}' on 'Headers': Invalid name");
        }

        if (!HeaderList.IsValue(headerValue))
        {
            Throw.TypeError(realm, $"Failed to execute '{operation}' on 'Headers': Invalid value");
        }

        return (name, headerValue);
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
