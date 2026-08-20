#if NET8_0_OR_GREATER
using System.Runtime.InteropServices;
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.WebApi.Abort;

namespace Jint.WebApi.Streams;

/// <summary>
/// The WebIDL dictionaries the stream constructors and methods take, converted exactly as
/// https://webidl.spec.whatwg.org/#es-dictionary prescribes.
/// </summary>
/// <remarks>
/// <para>
/// Two things about that conversion are observable and therefore load-bearing here. Members are read in
/// <b>lexicographical order of identifier</b>, not declaration order, so a dictionary of getters sees its
/// properties touched in a fixed order. And the conversion happens where the argument is declared: a
/// dictionary argument is converted at the WebIDL layer, <i>before</i> the operation's own steps run, while
/// the <c>underlyingSource</c>/<c>underlyingSink</c>/<c>transformer</c> objects are converted in the
/// constructors' prose — which is why <c>new ReadableStream(sourceWhoseStartGetterThrows,
/// strategyWhoseSizeGetterThrows)</c> reports the <i>strategy's</i> exception.
/// </para>
/// <para>
/// A member whose value is <see langword="undefined"/> does not exist, which is why every "does the member
/// exist" test below is a test against <see langword="null"/> rather than against
/// <see cref="JsValue.Undefined"/>.
/// </para>
/// </remarks>
internal static class StreamDictionaries
{
    /// <summary>
    /// The <c>QueuingStrategy</c> dictionary — https://streams.spec.whatwg.org/#dictdef-queuingstrategy.
    /// </summary>
    [StructLayout(LayoutKind.Auto)]
    internal readonly record struct QueuingStrategyRecord(double? HighWaterMark, ICallable? Size);

    /// <summary>
    /// The <c>UnderlyingSource</c> dictionary — https://streams.spec.whatwg.org/#dictdef-underlyingsource.
    /// </summary>
    [StructLayout(LayoutKind.Auto)]
    internal readonly record struct UnderlyingSourceRecord(ICallable? Start, ICallable? Pull, ICallable? Cancel, bool TypeExists);

    /// <summary>
    /// The <c>UnderlyingSink</c> dictionary — https://streams.spec.whatwg.org/#dictdef-underlyingsink.
    /// </summary>
    [StructLayout(LayoutKind.Auto)]
    internal readonly record struct UnderlyingSinkRecord(ICallable? Start, ICallable? Write, ICallable? Close, ICallable? Abort, bool TypeExists);

    /// <summary>
    /// The <c>Transformer</c> dictionary — https://streams.spec.whatwg.org/#dictdef-transformer.
    /// </summary>
    [StructLayout(LayoutKind.Auto)]
    internal readonly record struct TransformerRecord(
        ICallable? Start,
        ICallable? Transform,
        ICallable? Flush,
        ICallable? Cancel,
        bool ReadableTypeExists,
        bool WritableTypeExists);

    /// <summary>
    /// The <c>StreamPipeOptions</c> dictionary — https://streams.spec.whatwg.org/#dictdef-streampipeoptions.
    /// </summary>
    [StructLayout(LayoutKind.Auto)]
    internal readonly record struct StreamPipeOptionsRecord(bool PreventAbort, bool PreventCancel, bool PreventClose, JsAbortSignal? Signal);

    /// <summary>
    /// Converts a value to a dictionary's backing object: <see langword="null"/> means every member is
    /// absent, which is what <see langword="undefined"/> and <c>null</c> convert to.
    /// </summary>
    private static ObjectInstance? AsDictionary(Realm realm, JsValue value, string what)
    {
        if (value.IsUndefined() || value.IsNull())
        {
            return null;
        }

        if (value is not ObjectInstance objectInstance)
        {
            Throw.TypeError(realm, $"{what} is not an object");
            return null;
        }

        return objectInstance;
    }

    /// <summary>
    /// Reads one member declared as a callback function: present and not callable is a <c>TypeError</c>,
    /// https://webidl.spec.whatwg.org/#es-callback-function.
    /// </summary>
    private static ICallable? ReadCallback(Realm realm, ObjectInstance? dictionary, string name, string what)
    {
        if (dictionary is null)
        {
            return null;
        }

        var value = dictionary.Get(name);
        if (value.IsUndefined())
        {
            return null;
        }

        if (value is not ICallable callable)
        {
            Throw.TypeError(realm, $"{what}.{name} is not a function");
            return null;
        }

        return callable;
    }

    /// <summary>Whether a member exists at all, for the members the streams only test for existence.</summary>
    private static bool Exists(ObjectInstance? dictionary, string name)
        => dictionary is not null && !dictionary.Get(name).IsUndefined();

    /// <summary>
    /// The <c>QueuingStrategy</c> conversion. Members in lexicographical order: <c>highWaterMark</c>, then
    /// <c>size</c>. <c>highWaterMark</c> is an <c>unrestricted double</c>, so it is coerced here and only
    /// range-checked later by <see cref="ExtractHighWaterMark"/> — which is why
    /// <c>{ highWaterMark: -1 }</c> is a <c>RangeError</c> from the constructor body rather than a
    /// conversion failure.
    /// </summary>
    internal static QueuingStrategyRecord ReadQueuingStrategy(Realm realm, JsValue value, string what)
    {
        var dictionary = AsDictionary(realm, value, what);
        if (dictionary is null)
        {
            return default;
        }

        var highWaterMarkValue = dictionary.Get("highWaterMark");
        double? highWaterMark = highWaterMarkValue.IsUndefined() ? null : TypeConverter.ToNumber(highWaterMarkValue);
        var size = ReadCallback(realm, dictionary, "size", what);
        return new QueuingStrategyRecord(highWaterMark, size);
    }

    /// <summary>
    /// The <c>UnderlyingSource</c> conversion. Members in lexicographical order:
    /// <c>autoAllocateChunkSize</c>, <c>cancel</c>, <c>pull</c>, <c>start</c>, <c>type</c>.
    /// </summary>
    /// <remarks>
    /// <c>autoAllocateChunkSize</c> is read and converted even though it is meaningless without
    /// <c>type: "bytes"</c>: the conversion is observable — it is an <c>[EnforceRange] unsigned long long</c>,
    /// so a getter that throws, or a value out of range, is reported from here — and the specification
    /// simply ignores the converted value for a non-byte stream.
    /// </remarks>
    internal static UnderlyingSourceRecord ReadUnderlyingSource(Realm realm, JsValue value)
    {
        const string What = "The underlying source";
        var dictionary = AsDictionary(realm, value, What);
        if (dictionary is null)
        {
            return default;
        }

        ReadEnforcedUnsignedLongLong(realm, dictionary, "autoAllocateChunkSize", What);
        var cancel = ReadCallback(realm, dictionary, "cancel", What);
        var pull = ReadCallback(realm, dictionary, "pull", What);
        var start = ReadCallback(realm, dictionary, "start", What);
        var typeExists = ReadReadableStreamType(realm, dictionary);

        return new UnderlyingSourceRecord(start, pull, cancel, typeExists);
    }

    /// <summary>
    /// The <c>ReadableStreamType</c> enumeration — https://streams.spec.whatwg.org/#enumdef-readablestreamtype.
    /// Its only value is <c>"bytes"</c>; anything else is a <c>TypeError</c> from the enumeration conversion,
    /// https://webidl.spec.whatwg.org/#es-enumeration.
    /// </summary>
    private static bool ReadReadableStreamType(Realm realm, ObjectInstance dictionary)
    {
        var type = dictionary.Get("type");
        if (type.IsUndefined())
        {
            return false;
        }

        if (!string.Equals(TypeConverter.ToString(type), "bytes", StringComparison.Ordinal))
        {
            Throw.TypeError(realm, "The underlying source's type is not a valid enumeration value for ReadableStreamType");
        }

        return true;
    }

    /// <summary>
    /// The <c>UnderlyingSink</c> conversion. Members in lexicographical order: <c>abort</c>, <c>close</c>,
    /// <c>start</c>, <c>type</c>, <c>write</c>. <c>type</c> is <c>any</c>, so it is only tested for
    /// existence — the <c>WritableStream</c> constructor rejects any value at all, which is how the
    /// specification reserves the name.
    /// </summary>
    internal static UnderlyingSinkRecord ReadUnderlyingSink(Realm realm, JsValue value)
    {
        const string What = "The underlying sink";
        var dictionary = AsDictionary(realm, value, What);
        if (dictionary is null)
        {
            return default;
        }

        var abort = ReadCallback(realm, dictionary, "abort", What);
        var close = ReadCallback(realm, dictionary, "close", What);
        var start = ReadCallback(realm, dictionary, "start", What);
        var typeExists = Exists(dictionary, "type");
        var write = ReadCallback(realm, dictionary, "write", What);

        return new UnderlyingSinkRecord(start, write, close, abort, typeExists);
    }

    /// <summary>
    /// The <c>Transformer</c> conversion. Members in lexicographical order: <c>cancel</c>, <c>flush</c>,
    /// <c>readableType</c>, <c>start</c>, <c>transform</c>, <c>writableType</c>.
    /// </summary>
    internal static TransformerRecord ReadTransformer(Realm realm, JsValue value)
    {
        const string What = "The transformer";
        var dictionary = AsDictionary(realm, value, What);
        if (dictionary is null)
        {
            return default;
        }

        var cancel = ReadCallback(realm, dictionary, "cancel", What);
        var flush = ReadCallback(realm, dictionary, "flush", What);
        var readableTypeExists = Exists(dictionary, "readableType");
        var start = ReadCallback(realm, dictionary, "start", What);
        var transform = ReadCallback(realm, dictionary, "transform", What);
        var writableTypeExists = Exists(dictionary, "writableType");

        return new TransformerRecord(start, transform, flush, cancel, readableTypeExists, writableTypeExists);
    }

    /// <summary>
    /// The <c>StreamPipeOptions</c> conversion. Members in lexicographical order: <c>preventAbort</c>,
    /// <c>preventCancel</c>, <c>preventClose</c>, <c>signal</c>. The three booleans default to false; the
    /// signal has no default, so an explicit <see langword="undefined"/> means "not given" and any other
    /// non-<c>AbortSignal</c> value is a <c>TypeError</c>.
    /// </summary>
    internal static StreamPipeOptionsRecord ReadStreamPipeOptions(Realm realm, JsValue value)
    {
        const string What = "The pipe options";
        var dictionary = AsDictionary(realm, value, What);
        if (dictionary is null)
        {
            return default;
        }

        var preventAbort = TypeConverter.ToBoolean(dictionary.Get("preventAbort"));
        var preventCancel = TypeConverter.ToBoolean(dictionary.Get("preventCancel"));
        var preventClose = TypeConverter.ToBoolean(dictionary.Get("preventClose"));

        var signalValue = dictionary.Get("signal");
        JsAbortSignal? signal = null;
        if (!signalValue.IsUndefined())
        {
            signal = signalValue as JsAbortSignal;
            if (signal is null)
            {
                Throw.TypeError(realm, "The pipe options' signal is not an AbortSignal");
            }
        }

        return new StreamPipeOptionsRecord(preventAbort, preventCancel, preventClose, signal);
    }

    /// <summary>
    /// The <c>ReadableWritablePair</c> conversion —
    /// https://streams.spec.whatwg.org/#dictdef-readablewritablepair. Both members are <c>required</c> and
    /// of interface type, and are read in lexicographical order: <c>readable</c>, then <c>writable</c>.
    /// </summary>
    internal static (JsReadableStream Readable, JsWritableStream Writable) ReadReadableWritablePair(Realm realm, JsValue value)
    {
        const string What = "The transform";
        var dictionary = AsDictionary(realm, value, What);

        var readableValue = dictionary is null ? JsValue.Undefined : dictionary.Get("readable");
        if (readableValue is not JsReadableStream readable)
        {
            Throw.TypeError(realm, $"{What}.readable is not a ReadableStream");
            return default;
        }

        var writableValue = dictionary!.Get("writable");
        if (writableValue is not JsWritableStream writable)
        {
            Throw.TypeError(realm, $"{What}.writable is not a WritableStream");
            return default;
        }

        return (readable, writable);
    }

    /// <summary>
    /// The <c>ReadableStreamGetReaderOptions</c> conversion —
    /// https://streams.spec.whatwg.org/#dictdef-readablestreamgetreaderoptions. Its single member is the
    /// <c>ReadableStreamReaderMode</c> enumeration, whose only value is <c>"byob"</c>.
    /// </summary>
    /// <returns>Whether the <c>mode</c> member exists, i.e. whether a BYOB reader was asked for.</returns>
    internal static bool ReadByobModeRequested(Realm realm, JsValue value)
    {
        var dictionary = AsDictionary(realm, value, "The reader options");
        if (dictionary is null)
        {
            return false;
        }

        var mode = dictionary.Get("mode");
        if (mode.IsUndefined())
        {
            return false;
        }

        if (!string.Equals(TypeConverter.ToString(mode), "byob", StringComparison.Ordinal))
        {
            Throw.TypeError(realm, "The reader options' mode is not a valid enumeration value for ReadableStreamReaderMode");
        }

        return true;
    }

    /// <summary>
    /// The <c>ReadableStreamIteratorOptions</c> conversion —
    /// https://streams.spec.whatwg.org/#dictdef-readablestreamiteratoroptions. <c>preventCancel</c> has a
    /// default of false, so an absent member and an explicit <see langword="undefined"/> both mean false.
    /// </summary>
    internal static bool ReadPreventCancel(Realm realm, JsValue value)
    {
        var dictionary = AsDictionary(realm, value, "The iterator options");
        return dictionary is not null && TypeConverter.ToBoolean(dictionary.Get("preventCancel"));
    }

    /// <summary>
    /// The <c>QueuingStrategyInit</c> conversion —
    /// https://streams.spec.whatwg.org/#dictdef-queuingstrategyinit. <c>highWaterMark</c> is
    /// <c>required</c>, so a missing member is a <c>TypeError</c> rather than a default; the value itself is
    /// deliberately not validated here, which is why <c>new CountQueuingStrategy({highWaterMark: -1})</c>
    /// succeeds and only the stream built with it fails.
    /// </summary>
    internal static double ReadQueuingStrategyInit(Realm realm, JsValue value, string interfaceName)
    {
        var dictionary = AsDictionary(realm, value, $"The {interfaceName} init");
        var highWaterMark = dictionary is null ? JsValue.Undefined : dictionary.Get("highWaterMark");
        if (highWaterMark.IsUndefined())
        {
            Throw.TypeError(realm, $"Failed to construct '{interfaceName}': required member highWaterMark is undefined");
        }

        return TypeConverter.ToNumber(highWaterMark);
    }

    /// <summary>
    /// https://streams.spec.whatwg.org/#validate-and-normalize-high-water-mark
    /// </summary>
    internal static double ExtractHighWaterMark(Realm realm, in QueuingStrategyRecord strategy, double defaultHighWaterMark)
    {
        if (strategy.HighWaterMark is not { } highWaterMark)
        {
            return defaultHighWaterMark;
        }

        if (double.IsNaN(highWaterMark) || highWaterMark < 0)
        {
            Throw.RangeError(realm, "The highWaterMark of a queuing strategy must be a non-negative, non-NaN number");
        }

        // +∞ is explicitly allowed: it makes backpressure never apply.
        return highWaterMark;
    }

    /// <summary>
    /// https://streams.spec.whatwg.org/#make-size-algorithm-from-size-function. The callback's return type
    /// is <c>unrestricted double</c>, so the returned value is coerced with <c>ToNumber</c> here and a throw
    /// from either the call or the coercion propagates to the caller.
    /// </summary>
    internal static Func<JsValue, double> ExtractSizeAlgorithm(in QueuingStrategyRecord strategy)
    {
        if (strategy.Size is not { } size)
        {
            return static _ => 1;
        }

        return chunk => TypeConverter.ToNumber(size.Call(JsValue.Undefined, chunk));
    }

    /// <summary>
    /// The <c>[EnforceRange] unsigned long long</c> conversion,
    /// https://webidl.spec.whatwg.org/#js-unsigned-long-long: a value that is not a finite number, or whose
    /// integer part falls outside the type, is a <c>TypeError</c> rather than a wrap.
    /// </summary>
    private static void ReadEnforcedUnsignedLongLong(Realm realm, ObjectInstance dictionary, string name, string what)
    {
        var value = dictionary.Get(name);
        if (value.IsUndefined())
        {
            return;
        }

        var number = TypeConverter.ToNumber(value);
        if (!double.IsFinite(number))
        {
            Throw.TypeError(realm, $"{what}.{name} is not a finite number");
        }

        var integer = Math.Truncate(number);
        if (integer < 0 || integer > 18446744073709551615d)
        {
            Throw.TypeError(realm, $"{what}.{name} is outside the range of an unsigned long long");
        }
    }
}
#endif
