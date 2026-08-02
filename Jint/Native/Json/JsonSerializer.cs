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
/// <b>Lifetime.</b> An instance is bound to the <see cref="Engine"/> it was constructed with: that engine
/// supplies the wrapper object each call serializes out of, the realm the <c>toJSON</c> lookup resolves
/// against, and the execution constraints checked while walking large objects and arrays. It is reusable
/// across calls — every field the per-call prologue only conditionally assigns (the replacer function, the
/// replacer-array property list, the indentation and the cycle-detection stack) is reset at the start of
/// each <c>Serialize</c> call, so a previous call cannot leak state into the next, including a call that
/// threw part-way through on a circular structure. It is not thread-safe: that per-call state is instance
/// state, so two concurrent calls on one instance corrupt each other's output. Hold one per engine (and
/// serialize access to the engine anyway, which is itself single-threaded), or construct one per call as
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
    private ObjectTraverseStack _stack = null!;
    private string? _indent;
    private string _gap = string.Empty;
    private List<JsValue>? _propertyList;
    private bool _hasReplacerFunction;

    // Declared last: this is the only field that is not read on the per-key hot path (only
    // UnwrapValueSlow touches it, and only when _hasReplacerFunction says there is one), and embedding
    // the invoker by value is what keeps the replacer call array-free. It grows the instance by about
    // one cache line, which is one allocation per Serialize call against a walk of the whole graph —
    // unlike ArrayPrototype.ArrayComparer, where the same embedding cost ~4% because that object is
    // dereferenced once per comparison of an n log n sort with almost no work in between.
    private CallbackInvoker _replacerInvoker;

    private static readonly JsString toJsonProperty = new("toJSON");

    public JsonSerializer(Engine engine)
    {
        _engine = engine;
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
        if (!TryCreateHolder(value, replacer, space, out var wrapper))
        {
            return JsValue.Undefined;
        }

        string result;
        var json = new ValueStringBuilder();
        try
        {
            if (SerializeJSONProperty(JsString.Empty, wrapper, ref json) == SerializeResult.Undefined)
            {
                return JsValue.Undefined;
            }
        }
        finally
        {
            result = json.ToString();
        }
        return new JsString(result);
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

        if (!TryCreateHolder(value, replacer, space, out var wrapper))
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

            WriteUtf8(json.AsSpan(), writer);
            return true;
        }
        finally
        {
            json.Dispose();
        }
    }

    /// <summary>
    /// The shared prologue of https://tc39.es/ecma262/#sec-json.stringify: configures the replacer and the
    /// gap, and builds the wrapper object the value is serialized out of. Returns <see langword="false"/>
    /// for the inputs that produce no output at all.
    /// </summary>
    private bool TryCreateHolder(JsValue value, JsValue replacer, JsValue space, out ObjectInstance wrapper)
    {
        _stack = new ObjectTraverseStack(_engine);

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
        if (value.IsCallable && ReferenceEquals(replacer, JsValue.Undefined))
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

        if (oi.IsCallable)
        {
            // Built once here rather than per key: the replacer is invoked for every key of the whole
            // graph, and how it must be invoked depends only on the callback — which no key, and no
            // mutation a replacer performs, can change. Create() rather than Rent() because the
            // invoker lives for the whole recursive walk, which can exit by exception (a cycle, a
            // BigInt, an execution constraint) from any depth, so there is no one place that could
            // reliably hand a pooled array back. On the register lane there is no array at all.
            _replacerInvoker = CallbackInvoker.Create(_engine, (ICallable) oi, 2);
            _hasReplacerFunction = true;
        }
        else
        {
            if (oi.IsArray())
            {
                _propertyList = new List<JsValue>();
                var len = oi.GetLength();
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

                    if (!item.IsUndefined() && !_propertyList.Contains(item))
                    {
                        _propertyList.Add(item);
                    }

                    k++;
                }
            }
        }
    }

    private static string BuildSpacingGap(JsValue space)
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
            QuoteJSONString(value.ToString(), ref json);
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

        if (value is ObjectInstance { IsCallable: false } objectInstance)
        {
            // Handle RawJSON objects - output rawJSON property directly
            if (objectInstance is JsRawJson rawJson)
            {
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
                json.Append(serialize(wrapper.Target, _gap, _indent));
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
        if (len == 0)
        {
            json.Append("[]");
            return;
        }

        _stack.Enter(value);
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
            _stack.Exit();
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

        _stack.Exit();
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

        var enumeration = _propertyList is null
            ? PropertyEnumeration.FromObjectInstance(value)
            : PropertyEnumeration.FromList(_propertyList);
        if (enumeration.IsEmpty)
        {
            json.Append("{}");
            return;
        }

        _stack.Enter(value);
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

            QuoteJSONString(p.ToString(), ref json);
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
            _stack.Exit();
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

        _stack.Exit();
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
            json.Append("{}");
            return true;
        }

        _stack.Enter(value);
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
            _stack.Exit();
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

        _stack.Exit();
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

        public static PropertyEnumeration FromObjectInstance(ObjectInstance instance)
        {
            var allKeys = instance.GetOwnPropertyKeys(Types.String);
            RemoveUnserializableProperties(instance, allKeys);
            return new PropertyEnumeration(allKeys, allKeys.Count == 0);
        }

        private static void RemoveUnserializableProperties(ObjectInstance instance, List<JsValue> keys)
        {
            for (var i = 0; i < keys.Count; i++)
            {
                var key = keys[i];
                if (instance.ProbeOwnPropertyChecked(key) != OwnPropertyProbe.Enumerable)
                {
                    keys.RemoveAt(i);
                    i--;
                }
            }
        }

        public readonly List<JsValue> Keys;
        public readonly bool IsEmpty;
    }
}
