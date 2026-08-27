using System.Buffers;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using Jint.Collections;
using Jint.Native.BigInt;
using Jint.Native.Boolean;
using Jint.Native.Number;
using Jint.Native.Object;
using Jint.Native.String;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;

namespace Jint.Native.Json;

/// <summary>
/// Serializes a <see cref="JsValue"/> to a JSON document, implementing
/// https://tc39.es/ecma262/#sec-json.stringify. This is the same code path <c>JSON.stringify</c> takes, so
/// <c>toJSON</c>, replacers and the <c>space</c> argument behave exactly as they do in script.
/// </summary>
/// <remarks>
/// <para>
/// <b>Lifetime.</b> An instance is bound to the <see cref="Engine"/> it was constructed with, and to the
/// <see cref="Jint.ResultLimits"/> it was constructed with: that engine
/// supplies the wrapper object each call serializes out of, the realm the <c>toJSON</c> lookup resolves
/// against, and the execution constraints checked while walking large objects and arrays. It is reusable
/// across calls — every field the per-call prologue only conditionally assigns (the replacer function, the
/// replacer-array property list, the indentation and the cycle-detection stack) is reset at the start of
/// each <c>Serialize</c> call, so a previous call cannot leak state into the next, including a call that
/// threw part-way through on a circular structure. The same instance cannot be re-entered from a
/// <c>toJSON</c>, replacer, or host callback because its per-call state is instance state. Hold one per engine
/// (and serialize access to the engine anyway, which is itself single-threaded), or construct one per call as
/// <c>JSON.stringify</c> does.
/// </para>
/// <para>
/// <b>Choosing an overload.</b> The <see cref="JsValue"/>-returning overloads are the fit unless the sink is
/// genuinely bytes. The document is always built as UTF-16 internally; the
/// <see cref="IBufferWriter{T}"/> overloads exist only to transcode that to UTF-8 directly into the caller's
/// buffer instead of materializing a string first. A caller who writes to an <see cref="IBufferWriter{T}"/>
/// and then turns the bytes back into a <see cref="string"/> has re-paid the very transcode the overload
/// exists to avoid, and added a decode on top.
/// </para>
/// <para>
/// <b>Document length.</b> The <see cref="JsValue"/>-returning overloads produce a JavaScript string and
/// are bounded by the same maximum length as every other way of building one: a document that would
/// exceed it raises <c>RangeError: Invalid string length</c>, which a script can catch, and raises it
/// while the document is being built rather than after it has been paid for. The
/// <see cref="Jint.ResultLimits"/> this instance was constructed with additionally bound both overload
/// families, including exact UTF-8 bytes before an <see cref="IBufferWriter{T}"/> is touched. The
/// single-argument constructor takes <see cref="Options.ResultLimits"/>, whose compatibility default is
/// unlimited; pass a different set to bound one caller more tightly than the engine.
/// </para>
/// <para>
/// <b>BigInt.</b> A <c>BigInt</c> that reaches the output throws a
/// <see cref="Runtime.JavaScriptException"/> carrying a <c>TypeError</c> ("Do not know how to serialize a
/// BigInt"), as the specification requires. A host using this type as a storage codec has one escape hatch:
/// the serializer performs the spec's <c>GetV(value, "toJSON")</c> lookup for BigInt values as well as for
/// objects, so installing a <c>toJSON</c> on <c>BigInt.prototype</c> —
/// <c>engine.Evaluate("BigInt.prototype.toJSON = function () { return this.toString(); }")</c> — makes
/// <c>1n</c> serialize as <c>"1"</c> instead of throwing. That is a realm-wide change rather than a
/// serializer setting: the property is visible to script and script's own <c>JSON.stringify</c> picks up the
/// same <c>toJSON</c>, so it is a deliberate host decision about the whole realm, not a private hook.
/// </para>
/// </remarks>
public sealed class JsonSerializer
{
    private const int ConstraintCheckInterval = Engine.ConstraintCheckInterval;

    private readonly Engine _engine;
    private readonly ResultLimits _limits;
    private ObjectTraverseStack _stack = null!;
    private string? _indent;
    private string _gap = string.Empty;
    private List<JsValue>? _propertyList;
    private bool _hasReplacerFunction;
    private long _propertyCount;
    private int _depth;
    private long _maxOutputBytes;
    private ResultLimit? _documentLengthResultLimit;
    private bool _active;

    // The largest document this call is allowed to build. JsString.MaxLength for the overloads whose
    // result is a JavaScript string, so JSON.stringify can no longer hand back a string the engine's
    // own limit forbids; the CLR array ceiling for the IBufferWriter overloads, whose result is bytes
    // the host consumes and never a JsString, so the language's string limit has no business bounding
    // them and a host writing a gigabyte-sized document to a stream keeps working.
    private long _maxDocumentLength;

    // Declared last: this is the only field that is not read on the per-key hot path (only
    // UnwrapValueSlow touches it, and only when _hasReplacerFunction says there is one), and embedding
    // the invoker by value is what keeps the replacer call array-free. It grows the instance by about
    // one cache line, which is one allocation per Serialize call against a walk of the whole graph —
    // unlike ArrayPrototype.ArrayComparer, where the same embedding cost ~4% because that object is
    // dereferenced once per comparison of an n log n sort with almost no work in between.
    private CallbackInvoker _replacerInvoker;

    private static readonly JsString toJsonProperty = new("toJSON");

    /// <summary>
    /// Creates a serializer bound to <paramref name="engine"/>, applying that engine's configured
    /// <see cref="Jint.ResultLimits"/>.
    /// </summary>
    public JsonSerializer(Engine engine)
        : this(engine, engine.Options.ResultLimits)
    {
    }

    /// <summary>
    /// Creates a serializer bound to <paramref name="engine"/> that applies <paramref name="limits"/> to
    /// every document it produces.
    /// </summary>
    /// <param name="engine">The engine supplying the realm, the wrapper object and the execution constraints.</param>
    /// <param name="limits">
    /// The output bounds for every call on this instance, in place of <see cref="Options.ResultLimits"/>.
    /// </param>
    public JsonSerializer(Engine engine, ResultLimits limits)
    {
        if (limits is null)
        {
            Throw.ArgumentNullException(nameof(limits));
        }

        _engine = engine;
        _limits = limits!;
    }

    /// <summary>
    /// Serializes <paramref name="value"/> to a JSON document.
    /// </summary>
    /// <returns>
    /// A <see cref="JsString"/> holding the document, or <see cref="JsValue.Undefined"/> when
    /// <paramref name="value"/> has no JSON representation — it is <c>undefined</c>, a function or a symbol,
    /// or its <c>toJSON</c> returned one of those — which are the cases where <c>JSON.stringify</c> itself
    /// evaluates to <c>undefined</c>. Check for the sentinel rather than converting unconditionally:
    /// <c>Serialize(v) is JsString s ? s.ToString() : null</c>. <c>Serialize(v).ToString()</c> does not fail
    /// for those inputs — it hands back the literal text <c>undefined</c>, which is not JSON, and which a
    /// caller storing the result will only discover on the way back out.
    /// </returns>
    public JsValue Serialize(JsValue value)
    {
        return Serialize(value, JsValue.Undefined, JsValue.Undefined);
    }

    /// <summary>
    /// Serializes <paramref name="value"/> to a JSON document, applying <paramref name="replacer"/> (a
    /// function or an array of keys to keep) and <paramref name="space"/> (the indentation) as
    /// <c>JSON.stringify</c> does.
    /// </summary>
    /// <returns>
    /// A <see cref="JsString"/> holding the document, or <see cref="JsValue.Undefined"/> when what is to be
    /// serialized has no JSON representation — the value itself, or whatever a <c>toJSON</c> or
    /// <paramref name="replacer"/> turned it into, is <c>undefined</c>, a function or a symbol — which are
    /// the cases where <c>JSON.stringify</c> itself evaluates to <c>undefined</c>. Check for the sentinel
    /// rather than converting unconditionally:
    /// <c>Serialize(v, r, s) is JsString js ? js.ToString() : null</c>.
    /// <c>Serialize(v, r, s).ToString()</c> does not fail for those inputs — it hands back the literal text
    /// <c>undefined</c>, which is not JSON, and which a caller storing the result will only discover on the
    /// way back out.
    /// </returns>
    public JsValue Serialize(JsValue value, JsValue replacer, JsValue space)
    {
        return _engine.ExecuteWithConstraints(
            _engine.Options.Strict,
            () => SerializeEntry(value, replacer, space));
    }

    private JsValue SerializeEntry(JsValue value, JsValue replacer, JsValue space)
    {
        Enter();
        try
        {
            if (!TryCreateHolder(value, replacer, space, JsString.MaxLength, utf8: false, out var wrapper))
            {
                return JsValue.Undefined;
            }

            var json = new ValueStringBuilder();
            try
            {
                if (SerializeJSONProperty(JsString.Empty, wrapper, ref json) == SerializeResult.Undefined)
                {
                    return JsValue.Undefined;
                }

                // The walk refuses an over-long document while it is being built; this catches the one
                // thing a single append can still overshoot by — the expansion of a quoted string's
                // escapes — before the document is turned into a JsString.
                ThrowIfDocumentLengthExceeded(json.Length);
                return new JsString(json.ToString());
            }
            finally
            {
                // Dispose rather than the ToString() this used to end in: on the throwing path that
                // materialized the whole partial document, which is up to half a billion characters the
                // caller never sees. ToString() disposes on the way out, so this is a no-op after it.
                json.Dispose();
            }
        }
        finally
        {
            _active = false;
        }
    }

    /// <summary>
    /// Serializes <paramref name="value"/> and writes the document to <paramref name="writer"/> as UTF-8,
    /// for callers that hold or emit UTF-8 and would otherwise transcode the result of
    /// <see cref="Serialize(JsValue)"/> themselves.
    /// </summary>
    /// <returns>
    /// <see langword="false"/> when the value has no JSON representation, matching the cases where
    /// <see cref="Serialize(JsValue)"/> returns <see cref="JsValue.Undefined"/>; nothing is written then.
    /// Otherwise <see langword="true"/>.
    /// </returns>
    public bool Serialize(JsValue value, IBufferWriter<byte> writer)
    {
        return Serialize(value, JsValue.Undefined, JsValue.Undefined, writer);
    }

    /// <summary>
    /// Serializes <paramref name="value"/> and writes the document to <paramref name="writer"/> as UTF-8,
    /// for callers that hold or emit UTF-8 and would otherwise transcode the result of
    /// <see cref="Serialize(JsValue, JsValue, JsValue)"/> themselves.
    /// </summary>
    /// <returns>
    /// <see langword="false"/> when the value has no JSON representation, matching the cases where
    /// <see cref="Serialize(JsValue, JsValue, JsValue)"/> returns <see cref="JsValue.Undefined"/>; nothing
    /// is written then. Otherwise <see langword="true"/>.
    /// </returns>
    /// <remarks>
    /// The bytes are exactly <c>Encoding.UTF8.GetBytes</c> of what the string-returning overload produces
    /// for the same arguments. That is byte-for-byte round-trippable except where the document itself is
    /// not well-formed UTF-16: string contents and property names are always escaped as <c>\uXXXX</c>, but
    /// the <c>space</c> indentation, raw JSON text and host-supplied interop JSON are copied through
    /// verbatim, so an unpaired surrogate reaching the output through one of those becomes U+FFFD here.
    /// A caller transcoding the string overload's result with <c>Encoding.UTF8</c> gets the same
    /// substitution, so this is a property of UTF-8 rather than a divergence between the two overloads.
    /// </remarks>
    public bool Serialize(JsValue value, JsValue replacer, JsValue space, IBufferWriter<byte> writer)
    {
        if (writer is null)
        {
            Throw.ArgumentNullException(nameof(writer));
        }

        return _engine.ExecuteWithConstraints(
            _engine.Options.Strict,
            () => SerializeEntry(value, replacer, space, writer));
    }

    private bool SerializeEntry(
        JsValue value,
        JsValue replacer,
        JsValue space,
        IBufferWriter<byte> writer)
    {
        Enter();
        try
        {
            if (!TryCreateHolder(value, replacer, space, ClrLimits.MaxArrayLength, utf8: true, out var wrapper))
            {
                return false;
            }

            var json = new ValueStringBuilder();
            try
            {
                if (SerializeJSONProperty(JsString.Empty, wrapper, ref json) == SerializeResult.Undefined)
                {
                    return false;
                }

                var byteCount = GetUtf8ByteCount(json.AsSpan());
                if (byteCount > _maxOutputBytes)
                {
                    ThrowResultLimit(ResultLimit.OutputBytes, _maxOutputBytes, byteCount);
                }

                WriteUtf8(json.AsSpan(), writer);
                return true;
            }
            finally
            {
                json.Dispose();
            }
        }
        finally
        {
            _active = false;
        }
    }

    private void Enter()
    {
        if (_active)
        {
            Throw.InvalidOperationException("The JsonSerializer instance is already serializing a value.");
        }

        _active = true;
    }

    /// <summary>
    /// The shared prologue of https://tc39.es/ecma262/#sec-json.stringify: configures the replacer and the
    /// gap, and builds the wrapper object the value is serialized out of. Returns <see langword="false"/>
    /// for the inputs that produce no output at all.
    /// </summary>
    private bool TryCreateHolder(
        JsValue value,
        JsValue replacer,
        JsValue space,
        long maxDocumentLength,
        bool utf8,
        out ObjectInstance wrapper)
    {
        _stack = new ObjectTraverseStack(_engine);
        var resultLength = _limits.MaxOutputCharacters;
        _documentLengthResultLimit = ResultLimit.OutputCharacters;
        if (utf8 && _limits.MaxOutputBytes < resultLength)
        {
            // Every UTF-16 code unit contributes at least one UTF-8 byte, so this is a conservative
            // pre-allocation bound. The finished document is counted exactly before the writer is touched.
            resultLength = _limits.MaxOutputBytes;
            _documentLengthResultLimit = ResultLimit.OutputBytes;
        }

        _maxDocumentLength = System.Math.Min(maxDocumentLength, resultLength);
        _maxOutputBytes = _limits.MaxOutputBytes;
        if (maxDocumentLength < resultLength)
        {
            _documentLengthResultLimit = null;
        }
        _propertyCount = 0;
        _depth = 0;

        // JSON.stringify allocates a serializer per call, but the type is public and a host may hold an
        // instance across calls: every field the prologue only conditionally assigns has to be cleared
        // here, or the previous call's replacer, property list or indentation leaks into this one.
        _indent = null;
        _gap = string.Empty;
        _propertyList = null;
        _hasReplacerFunction = false;
        _replacerInvoker = default;

        // for JSON.stringify(), any function passed as the first argument will return undefined
        // if the replacer is not defined. The function is not called either.
        if (value.HasCall && ReferenceEquals(replacer, JsValue.Undefined))
        {
            wrapper = null!;
            return false;
        }

        SetupReplacer(replacer);
        _gap = BuildSpacingGap(space);

        wrapper = _engine.Realm.Intrinsics.Object.Construct(Arguments.Empty);
        wrapper.DefineOwnProperty(JsString.Empty, new PropertyDescriptor(value, PropertyFlag.ConfigurableEnumerableWritable));
        return true;
    }

    /// <summary>
    /// Throws <c>RangeError: Invalid string length</c> — the same catchable error every other
    /// string-building path raises — when a document of <paramref name="length"/> characters would
    /// exceed what this call may produce.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The check does not live in <c>ValueStringBuilder</c>, which also backs URI encoding and the
    /// Intl/Temporal formatters and has no realm to raise a JavaScript error into. It lives at the
    /// points where this walk accumulates — once per array element, once per object key and before a
    /// string is copied in — so a document that cannot fit is refused while it is being built rather
    /// than after the whole cost has been paid.
    /// </para>
    /// <para>
    /// The length is a <see cref="long"/> so a caller can add what it is about to append, and what the
    /// elements it has not reached yet must contribute, without the sum wrapping. Every such estimate
    /// is a lower bound on the finished document, so this can never refuse one that would have fit.
    /// The realm is read only on the throwing path: <see cref="Engine.Realm"/> peeks the execution
    /// context stack, which is not something to do once per element.
    /// </para>
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ThrowIfDocumentLengthExceeded(long length)
    {
        if (length > _maxDocumentLength)
        {
            if (_documentLengthResultLimit is { } resultLimit)
            {
                ThrowResultLimit(resultLimit, _maxDocumentLength, length);
            }

            Throw.RangeError(_engine.Realm, "Invalid string length");
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ThrowIfStringLengthExceeded(int length)
    {
        if (length > _limits.MaxStringLength)
        {
            ThrowResultLimit(ResultLimit.StringLength, _limits.MaxStringLength, length);
        }
    }

    private void CountProperties(long count)
    {
        var observed = checked(_propertyCount + count);
        if (observed > _limits.MaxPropertyCount)
        {
            ThrowResultLimit(ResultLimit.PropertyCount, _limits.MaxPropertyCount, observed);
        }

        _propertyCount = observed;
    }

    private void EnterContainer(ObjectInstance value)
    {
        var observed = _depth + 1;
        if (observed > _limits.MaxDepth)
        {
            ThrowResultLimit(ResultLimit.Depth, _limits.MaxDepth, observed);
        }

        _stack.Enter(value);
        _depth = observed;
    }

    private void ExitContainer()
    {
        _stack.Exit();
        _depth--;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowResultLimit(ResultLimit limit, long maximum, long observed)
    {
        throw new ResultLimitExceededException(limit, maximum, observed);
    }

    private static long GetUtf8ByteCount(ReadOnlySpan<char> chars)
    {
        const int CharChunkLength = 1024;
        const int ByteChunkLength = 4096;

        var encoder = Encoding.UTF8.GetEncoder();
        Span<byte> destination = stackalloc byte[ByteChunkLength];
        long total = 0;
        while (!chars.IsEmpty)
        {
            var chunk = chars.Length <= CharChunkLength ? chars : chars.Slice(0, CharChunkLength);
            var flush = chunk.Length == chars.Length;
            encoder.Convert(chunk, destination, flush, out var charsUsed, out var bytesUsed, out _);
            total += bytesUsed;
            chars = chars.Slice(charsUsed);
        }

        return total;
    }

    /// <summary>
    /// Transcodes the finished document in bounded rounds so the writer is never asked for more than one
    /// chunk's worth of destination space, however large the document is. The stateful encoder carries a
    /// surrogate pair split across a chunk boundary into the next round.
    /// </summary>
    private static void WriteUtf8(ReadOnlySpan<char> chars, IBufferWriter<byte> writer)
    {
        const int ChunkLength = 1024;

        var encoder = Encoding.UTF8.GetEncoder();
        while (!chars.IsEmpty)
        {
            var chunk = chars.Length <= ChunkLength ? chars : chars.Slice(0, ChunkLength);
            var flush = chunk.Length == chars.Length;

            // GetMaxByteCount leaves room for a surrogate held over from the previous round, so a
            // conforming writer always hands back enough space to drain the whole chunk in one go.
            var sizeHint = Encoding.UTF8.GetMaxByteCount(chunk.Length);
            var destination = writer.GetSpan(sizeHint);
            if (destination.Length < sizeHint)
            {
                // GetSpan must return at least sizeHint bytes. A shorter span cannot be relied on to
                // drain any of the chunk, so the loop would spin (or, on the pointer-based path, fault)
                // instead of making progress.
                Throw.InvalidOperationException(
                    $"The {nameof(IBufferWriter<byte>)} returned a buffer of {destination.Length} bytes for a size hint of {sizeHint} bytes.");
            }

            int charsUsed;
            int bytesUsed;
            encoder.Convert(chunk, destination, flush, out charsUsed, out bytesUsed, out _);

            writer.Advance(bytesUsed);
            chars = chars.Slice(charsUsed);
        }
    }

    private void SetupReplacer(JsValue replacer)
    {
        if (replacer is not ObjectInstance oi)
        {
            return;
        }

        if (oi.HasCall)
        {
            // Built once here rather than per key: the replacer is invoked for every key of the whole
            // graph, and how it must be invoked depends only on the callback — which no key, and no
            // mutation a replacer performs, can change. Create() rather than Rent() because this
            // invoker is a field of a public type a host may hold across calls, assigned here in the
            // prologue and read by the recursive walk: its lifetime is the serializer's, not one
            // scope's, so there is no bracket to return it from — not even on the path that succeeds.
            // Where a scope does exist, JsonInstance.Parse rents and returns in a finally. On the
            // register lane there is no array either way.
            _replacerInvoker = CallbackInvoker.Create(_engine, (ICallable) oi, 2);
            _hasReplacerFunction = true;
        }
        else
        {
            if (oi.IsSpecArray())
            {
                _propertyList = new List<JsValue>();
                var len = oi.GetLength();
                CountProperties(len);
                var k = 0;
                while (k < len)
                {
                    if (k > 0 && k % ConstraintCheckInterval == 0)
                    {
                        _engine.Constraints.Check();
                    }

                    var prop = JsString.Create(k);
                    var v = replacer.Get(prop);
                    var item = JsValue.Undefined;
                    if (v.IsString())
                    {
                        item = v;
                    }
                    else if (v.IsNumber())
                    {
                        item = TypeConverter.ToString(v);
                    }
                    else if (v.IsObject())
                    {
                        if (v is StringInstance or NumberInstance)
                        {
                            item = TypeConverter.ToString(v);
                        }
                    }

                    if (!item.IsUndefined())
                    {
                        ThrowIfStringLengthExceeded(((JsString) item).Length);
                        if (!_propertyList.Contains(item))
                        {
                            _propertyList.Add(item);
                        }
                    }

                    k++;
                }
            }
        }
    }

    private string BuildSpacingGap(JsValue space)
    {
        if (space.IsObject())
        {
            var spaceObj = space.AsObject();
            if (spaceObj.Class == ObjectClass.Number)
            {
                space = TypeConverter.ToNumber(spaceObj);
            }
            else if (spaceObj.Class == ObjectClass.String)
            {
                space = TypeConverter.ToJsString(spaceObj);
            }
        }

        // defining the gap
        if (space.IsNumber())
        {
            var number = ((JsNumber) space)._value;
            if (number > 0)
            {
                return new string(' ', (int) System.Math.Min(10, number));
            }

            return string.Empty;
        }

        if (space.IsString())
        {
            ThrowIfStringLengthExceeded(((JsString) space).Length);
            var stringSpace = space.ToString();
            return stringSpace.Length <= 10 ? stringSpace : stringSpace.Substring(0, 10);
        }

        return string.Empty;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-serializejsonproperty
    /// </summary>
    private SerializeResult SerializeJSONProperty(JsValue key, JsValue holder, ref ValueStringBuilder json)
    {
        return SerializeJSONValue(ReadUnwrappedValue(key, holder), ref json);
    }

    /// <summary>
    /// The value-writing half of SerializeJSONProperty: serializes an already-read-and-unwrapped
    /// value. Split out so the shaped fast path can feed slot-read values through the identical
    /// pipeline.
    /// </summary>
    private SerializeResult SerializeJSONValue(JsValue value, ref ValueStringBuilder json)
    {
        if (ReferenceEquals(value, JsValue.Null))
        {
            json.Append("null");
            return SerializeResult.NotUndefined;
        }

        if (value.IsBoolean())
        {
            json.Append(((JsBoolean) value)._value ? "true" : "false");
            return SerializeResult.NotUndefined;
        }

        if (value.IsString())
        {
            ThrowIfStringLengthExceeded(((JsString) value).Length);
            var stringValue = value.ToString();

            ThrowIfQuotedStringLengthExceeded(json.Length, stringValue);

            QuoteJSONString(stringValue, ref json);
            return SerializeResult.NotUndefined;
        }

        if (value.IsNumber())
        {
            var doubleValue = ((JsNumber) value)._value;

            if (value.IsInteger())
            {
                json.Append((long) doubleValue);
                return SerializeResult.NotUndefined;
            }

            var isFinite = double.IsFinite(doubleValue);
            if (isFinite)
            {
                if (TypeConverter.CanBeStringifiedAsLong(doubleValue))
                {
                    json.Append((long) doubleValue);
                    return SerializeResult.NotUndefined;
                }

                json.Append(NumberPrototype.ToNumberString(doubleValue));
                return SerializeResult.NotUndefined;
            }

            json.Append("null");
            return SerializeResult.NotUndefined;
        }

        if (value.IsBigInt())
        {
            Throw.TypeError(_engine.Realm, "Do not know how to serialize a BigInt");
        }

        if (value is ObjectInstance { HasCall: false } objectInstance)
        {
            // Handle RawJSON objects - output rawJSON property directly
            if (objectInstance is JsRawJson rawJson)
            {
                ThrowIfStringLengthExceeded(rawJson.RawJson.Length);
                // Raw JSON text and the host hook below are copied through verbatim, so what they
                // add is exactly their length: both are single appends large enough to be worth
                // refusing before the copy.
                ThrowIfDocumentLengthExceeded(json.Length + (long) rawJson.RawJson.Length);
                json.Append(rawJson.RawJson);
                return SerializeResult.NotUndefined;
            }

            if (CanSerializesAsArray(objectInstance))
            {
                SerializeJSONArray(objectInstance, ref json);
                return SerializeResult.NotUndefined;
            }

            if (objectInstance is IObjectWrapper wrapper
                && _engine.Options.Interop.SerializeToJson is { } serialize)
            {
                // The pattern keeps the append tolerant of a host handing back null, which it has
                // always been: ValueStringBuilder.Append(string?) returns for null.
                var hostJson = serialize(wrapper.Target, _gap, _indent);
                if (hostJson is { Length: > 0 })
                {
                    ThrowIfStringLengthExceeded(hostJson.Length);
                    ThrowIfDocumentLengthExceeded(json.Length + (long) hostJson.Length);
                    json.Append(hostJson);
                }
                return SerializeResult.NotUndefined;
            }

            SerializeJSONObject(objectInstance, ref json);
            return SerializeResult.NotUndefined;
        }

        return SerializeResult.Undefined;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private JsValue ReadUnwrappedValue(JsValue key, JsValue holder)
    {
        var value = holder.Get(key);

        if (value._type <= InternalTypes.Integer && !_hasReplacerFunction)
        {
            return value;
        }

        return UnwrapValueSlow(key, holder, value);
    }

    /// <summary>
    /// The observable part of the value read: toJSON lookup/call, replacer function call and wrapper
    /// instance unwrapping. Split from <see cref="ReadUnwrappedValue"/> so the shaped fast path can
    /// defer materializing the key (the toJSON/replacer argument) until a value actually needs it.
    /// </summary>
    private JsValue UnwrapValueSlow(JsValue key, JsValue holder, JsValue value)
    {
        var isBigInt = value is BigIntInstance || value.IsBigInt();
        if (value.IsObject() || isBigInt)
        {
            var toJson = value.GetV(_engine.Realm, toJsonProperty);
            if (toJson.IsUndefined() && isBigInt)
            {
                toJson = _engine.Realm.Intrinsics.BigInt.PrototypeObject.Get(toJsonProperty);
            }
            if (toJson.IsObject())
            {
                if (toJson.AsObject() is ICallable callableToJson)
                {
                    value = callableToJson.Call(value, TypeConverter.ToPropertyKey(key));
                }
            }
        }

        if (_hasReplacerFunction)
        {
            value = _replacerInvoker.Call(holder, TypeConverter.ToPropertyKey(key), value);
        }

        if (value.IsObject())
        {
            value = value switch
            {
                NumberInstance => TypeConverter.ToNumber(value),
                StringInstance => TypeConverter.ToString(value),
                BooleanInstance booleanInstance => booleanInstance.BooleanData,
                BigIntInstance bigIntInstance => bigIntInstance.BigIntData,
                _ => value
            };
        }

        return value;
    }

    private static bool CanSerializesAsArray(ObjectInstance value)
    {
        if (value is JsArray)
        {
            return true;
        }

        if (value is JsProxy proxyInstance && CanSerializesAsArray(proxyInstance._target))
        {
            return true;
        }

        if (value is ObjectWrapper { IsArrayLike: true })
        {
            return true;
        }

        // A host array-like serializes as a JSON array, following the ObjectWrapper convention above rather than
        // the browser's NodeList answer ({"0":…}) — consistency within Jint's host surface wins, and the type is
        // new so nothing can regress.
        if (value is ArrayLikeObject)
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-quotejsonstring
    /// </summary>
    /// <remarks>
    /// MethodImplOptions.AggressiveOptimization = 512 which is only exposed in .NET Core.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining | (MethodImplOptions) 512)]
    private static unsafe void QuoteJSONString(string value, ref ValueStringBuilder json)
    {
        if (value.Length == 0)
        {
            json.Append("\"\"");
            return;
        }

        json.Append('"');

#if NET8_0_OR_GREATER
        fixed (char* ptr = value)
        {
            int remainingLength = value.Length;
            int offset = 0;
            while (true)
            {
                int index = System.Text.Encodings.Web.JavaScriptEncoder.Default.FindFirstCharacterToEncode(ptr + offset, remainingLength);
                if (index < 0)
                {
                    // append the remaining text which doesn't need any encoding.
                    json.Append(value.AsSpan(offset));
                    break;
                }

                index += offset;
                if (index - offset > 0)
                {
                    // append everything which does not need any encoding until the found index.
                    json.Append(value.AsSpan(offset, index - offset));
                }

                AppendJsonStringCharacter(value, ref index, ref json);

                offset = index + 1;
                remainingLength = value.Length - offset;
                if (remainingLength == 0)
                {
                    break;
                }
            }
        }
#else
        for (var i = 0; i < value.Length; i++)
        {
            AppendJsonStringCharacter(value, ref i, ref json);
        }
#endif
        json.Append('"');
    }

    private void ThrowIfQuotedStringLengthExceeded(long currentLength, string value)
    {
        if (_documentLengthResultLimit is null)
        {
            ThrowIfDocumentLengthExceeded(currentLength + 2L + value.Length);
            return;
        }

        long quotedLength = 2;
        for (var i = 0; i < value.Length; i++)
        {
            var c = value[i];
            if (char.IsSurrogatePair(value, i))
            {
                quotedLength += 2;
                i++;
            }
            else if (c is '\"' or '\\' or '\b' or '\f' or '\n' or '\r' or '\t')
            {
                quotedLength += 2;
            }
            else
            {
                quotedLength += c < 0x20 || char.IsSurrogate(c) ? 6 : 1;
            }

            ThrowIfDocumentLengthExceeded(currentLength + quotedLength);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void AppendJsonStringCharacter(string value, ref int index, ref ValueStringBuilder json)
    {
        var c = value[index];
        switch (c)
        {
            case '\"':
                json.Append("\\\"");
                break;
            case '\\':
                json.Append("\\\\");
                break;
            case '\b':
                json.Append("\\b");
                break;
            case '\f':
                json.Append("\\f");
                break;
            case '\n':
                json.Append("\\n");
                break;
            case '\r':
                json.Append("\\r");
                break;
            case '\t':
                json.Append("\\t");
                break;
            default:
                if (char.IsSurrogatePair(value, index))
                {
#if NET8_0_OR_GREATER
                    json.Append(value.AsSpan(index, 2));
                    index++;
#else
                    json.Append(c);
                    index++;
                    json.Append(value[index]);
#endif
                }
                else if (c < 0x20 || char.IsSurrogate(c))
                {
                    json.Append("\\u");
                    json.Append(((int) c).ToString("x4", CultureInfo.InvariantCulture));
                }
                else
                {
                    json.Append(c);
                }
                break;
        }
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-serializejsonarray
    /// </summary>
    private void SerializeJSONArray(ObjectInstance value, ref ValueStringBuilder json)
    {
        var len = TypeConverter.ToUint32(value.Get(CommonProperties.Length));
        CountProperties(len);
        if (len == 0)
        {
            EnterContainer(value);
            ExitContainer();
            json.Append("[]");
            return;
        }

        EnterContainer(value);
        var stepback = _indent;
        if (_gap.Length > 0)
        {
            _indent += _gap;
        }

        const char separator = ',';
        bool hasPrevious = false;

        for (int i = 0; i < len; i++)
        {
            if (i > 0 && i % ConstraintCheckInterval == 0)
            {
                _engine.Constraints.Check();
            }

            // What is written so far can no longer be taken back — an array element is never rolled
            // back the way an object member with no JSON representation is — and every index still to
            // come adds at least two characters: a separator (or the opening bracket) plus at least
            // one character of value text, since an element with no representation is written as
            // null. So this is a lower bound on the finished document, and it is what makes an array
            // whose length alone puts the result out of reach fail here instead of after half a
            // billion characters have been built.
            ThrowIfDocumentLengthExceeded(json.Length + 2L * (len - i));

            if (hasPrevious)
            {
                json.Append(separator);
            }
            else
            {
                json.Append('[');
            }

            if (_gap.Length > 0)
            {
                json.Append('\n');
                json.Append(_indent);
            }

            if (SerializeJSONProperty(i, value, ref json) == SerializeResult.Undefined)
            {
                json.Append("null");
            }

            hasPrevious = true;
        }

        if (!hasPrevious)
        {
            ExitContainer();
            _indent = stepback;
            json.Append("[]");
            return;
        }

        if (_gap.Length > 0)
        {
            json.Append('\n');
            json.Append(stepback);
        }
        json.Append(']');

        ExitContainer();
        _indent = stepback;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-serializejsonobject
    /// </summary>
    private void SerializeJSONObject(ObjectInstance value, ref ValueStringBuilder json)
    {
        // Shape-mode fast arm, valid only when serializing ALL string-keyed own enumerable properties
        // (no replacer-array PropertyList overriding the key set/order). May refuse — having written
        // nothing — when slot order cannot express own-key order; the generic path below then handles it.
        if (_propertyList is null
            && (value._type & InternalTypes.ShapeMode) != InternalTypes.Empty
            && TrySerializeShapedJSONObject(Unsafe.As<JsObject>(value), ref json))
        {
            return;
        }

        PropertyEnumeration enumeration;
        if (_propertyList is null)
        {
            var keys = value.GetOwnPropertyKeys(Types.String);
            CountProperties(keys.Count);
            RemoveUnserializableProperties(value, keys);
            enumeration = PropertyEnumeration.FromList(keys);
        }
        else
        {
            CountProperties(_propertyList.Count);
            enumeration = PropertyEnumeration.FromList(_propertyList);
        }

        if (enumeration.IsEmpty)
        {
            EnterContainer(value);
            ExitContainer();
            json.Append("{}");
            return;
        }

        EnterContainer(value);
        var stepback = _indent;
        if (_gap.Length > 0)
        {
            _indent += _gap;
        }

        const char separator = ',';
        var hasPrevious = false;
        for (var i = 0; i < enumeration.Keys.Count; i++)
        {
            if (i > 0 && i % ConstraintCheckInterval == 0)
            {
                _engine.Constraints.Check();
            }

            // Checked before the key's own position is taken: a member with no JSON representation
            // rewinds the document to that position, so only what precedes it is certainly final.
            ThrowIfDocumentLengthExceeded(json.Length);

            var p = enumeration.Keys[i];
            int position = json.Length;

            if (hasPrevious)
            {
                json.Append(separator);
            }
            else
            {
                json.Append('{');
            }

            if (_gap.Length > 0)
            {
                json.Append('\n');
                json.Append(_indent);
            }

            ThrowIfStringLengthExceeded(((JsString) p).Length);
            var propertyName = p.ToString();
            ThrowIfQuotedStringLengthExceeded(json.Length, propertyName);
            QuoteJSONString(propertyName, ref json);
            json.Append(':');
            if (_gap.Length > 0)
            {
                json.Append(' ');
            }

            if (SerializeJSONProperty(p, value, ref json) == SerializeResult.Undefined)
            {
                json.Length = position;
            }
            else
            {
                hasPrevious = true;
            }
        }

        if (!hasPrevious)
        {
            ExitContainer();
            _indent = stepback;
            json.Append("{}");
            return;
        }

        if (_gap.Length > 0)
        {
            json.Append('\n');
            json.Append(stepback);
        }
        json.Append('}');

        ExitContainer();
        _indent = stepback;
    }

    /// <summary>
    /// Serializes a shape-mode plain object by walking its interned shape directly: keys come from
    /// the shape in slot (= insertion) order and are written from <see cref="Key.Name"/> without
    /// materializing per-key JsStrings or a key list, the enumerability probe is skipped (every shape
    /// property is an enumerable CEW data property), and values are read straight from the slots.
    /// Only key enumeration, own-property probing and the value read are replaced — the per-value
    /// pipeline (toJSON, replacer function, wrapper unwrapping, the value switch) is shared with the
    /// generic path. Returns <c>false</c>, before writing anything, when a digit-leading key is
    /// present: own-key order then places integer indices first, which slot order cannot express, so
    /// the caller falls back to the generic (sorting) path.
    /// </summary>
    private bool TrySerializeShapedJSONObject(JsObject value, ref ValueStringBuilder json)
    {
        var shape = value.ShapeOf;
        var keys = shape.OrderedKeys;

        for (var i = 0; i < keys.Length; i++)
        {
            var name = keys[i].Name;
            if (name.Length > 0 && char.IsDigit(name[0]))
            {
                return false;
            }
        }

        if (keys.Length == 0)
        {
            EnterContainer(value);
            ExitContainer();
            json.Append("{}");
            return true;
        }

        CountProperties(keys.Length);
        EnterContainer(value);
        var stepback = _indent;
        if (_gap.Length > 0)
        {
            _indent += _gap;
        }

        const char separator = ',';
        var hasPrevious = false;
        for (var i = 0; i < keys.Length; i++)
        {
            if (i > 0 && i % ConstraintCheckInterval == 0)
            {
                _engine.Constraints.Check();
            }

            // Same reasoning as the generic path: only what precedes this member's position is
            // certainly final, so the check goes before the position is taken.
            ThrowIfDocumentLengthExceeded(json.Length);

            int position = json.Length;

            if (hasPrevious)
            {
                json.Append(separator);
            }
            else
            {
                json.Append('{');
            }

            if (_gap.Length > 0)
            {
                json.Append('\n');
                json.Append(_indent);
            }

            ThrowIfStringLengthExceeded(keys[i].Name.Length);
            ThrowIfQuotedStringLengthExceeded(json.Length, keys[i].Name);
            QuoteJSONString(keys[i].Name, ref json);
            json.Append(':');
            if (_gap.Length > 0)
            {
                json.Append(' ');
            }

            // A previous member's toJSON or the replacer function may have mutated this object
            // (deopting it or transitioning its shape); the keys array is an immutable snapshot, so
            // remaining members are then read through the generic Get — exactly how the generic path
            // reads its snapshotted key list live. Slot reads are valid only while the object still
            // has the captured shape.
            JsValue member;
            if (ReferenceEquals(value.ShapeOf, shape))
            {
                // Serializing a member is a value observation, so a lazy layout slot materializes here.
                member = value.GetSlotForRead(i);
                if (member._type > InternalTypes.Integer || _hasReplacerFunction)
                {
                    // the key is observable (toJSON/replacer argument); materialize it only now
                    member = UnwrapValueSlow(new JsString(keys[i].Name), value, member);
                }
            }
            else
            {
                member = ReadUnwrappedValue(new JsString(keys[i].Name), value);
            }

            if (SerializeJSONValue(member, ref json) == SerializeResult.Undefined)
            {
                json.Length = position;
            }
            else
            {
                hasPrevious = true;
            }
        }

        if (!hasPrevious)
        {
            ExitContainer();
            _indent = stepback;
            json.Append("{}");
            return true;
        }

        if (_gap.Length > 0)
        {
            json.Append('\n');
            json.Append(stepback);
        }
        json.Append('}');

        ExitContainer();
        _indent = stepback;
        return true;
    }

    private enum SerializeResult
    {
        NotUndefined,
        Undefined,
    }

    private readonly struct PropertyEnumeration
    {
        private PropertyEnumeration(List<JsValue> keys, bool isEmpty)
        {
            Keys = keys;
            IsEmpty = isEmpty;
        }

        public static PropertyEnumeration FromList(List<JsValue> keys)
            => new PropertyEnumeration(keys, keys.Count == 0);

        public readonly List<JsValue> Keys;
        public readonly bool IsEmpty;
    }

    private void RemoveUnserializableProperties(ObjectInstance instance, List<JsValue> keys)
    {
        for (var i = 0; i < keys.Count; i++)
        {
            if (i > 0 && i % ConstraintCheckInterval == 0)
            {
                _engine.Constraints.Check();
            }

            var key = keys[i];
            if (instance.ProbeOwnPropertyChecked(key) != OwnPropertyProbe.Enumerable)
            {
                keys.RemoveAt(i);
                i--;
            }
        }
    }
}
