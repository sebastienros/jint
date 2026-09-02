using System.Buffers;
using System.Globalization;
using System.Text;
using System.Text.Json;
using Jint.Diagnostics;
using Jint.DevTools.Protocol.Runtime;
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;

namespace Jint.DevTools.Domains;

/// <summary>
/// Turns one <see cref="JsValue"/> into the protocol's <c>Runtime.RemoteObject</c>.
/// </summary>
/// <remarks>
/// <para>
/// Runs on the engine thread, like everything else that touches a value, and answers a record of strings and
/// JSON that may then cross to a transport thread.
/// </para>
/// <para>
/// <b>Nothing here mints an <c>objectId</c>.</b> A handle is a promise to keep the value alive until the
/// client releases it, and the table that keeps that promise arrives with the rest of the <c>Runtime</c>
/// domain. Until then a value that cannot be sent by value is described — type, subtype, class name and a
/// one-line description — which is what a client shows anyway, and no client is handed a handle it could
/// later find dangling.
/// </para>
/// <para>
/// The description comes from <see cref="ValueInspector"/>, which is the engine's own getter-free, trap-free
/// describer: describing a value here executes none of that value's code. Sending a value <i>by value</i> is
/// the deliberate exception — that is <c>JSON.stringify</c>'s contract and a client asking for it has asked
/// for the getters to run.
/// </para>
/// </remarks>
internal static class RemoteValues
{
    /// <summary>How deep a by-value serialization descends before it refuses.</summary>
    private const int MaxByValueDepth = 20;

    /// <summary>How many values one by-value serialization may write.</summary>
    private const int MaxByValueNodes = 10_000;

    private static readonly RemoteObject UndefinedObject = new() { Type = RemoteObjectTypeValues.Undefined };

#pragma warning disable JINT0002 // ValueInspector is the engine's preview describer; this is what it is for
    /// <summary>
    /// The header alone: no entries, because a preview is the object table's business and this package has
    /// none yet, and a walk nobody reads is work the engine thread should not do.
    /// </summary>
    private static readonly ValueInspectorOptions DescribeOnly = new() { MaxDepth = 0, MaxEntries = 0, MaxStringLength = 256 };
#pragma warning restore JINT0002

    /// <summary>Describes <paramref name="value"/> as the protocol's remote object.</summary>
    /// <param name="value">The value to describe.</param>
    /// <param name="byValue">Whether the client asked for the value itself rather than a description of it.</param>
    internal static RemoteObject Describe(JsValue value, bool byValue)
    {
        if (value.IsUndefined())
        {
            return UndefinedObject;
        }

        if (value.IsNull())
        {
            return new RemoteObject
            {
                Type = RemoteObjectTypeValues.Object,
                Subtype = RemoteObjectSubtypeValues.Null,
                Value = Json("null"),
            };
        }

        if (value.IsBoolean())
        {
            return new RemoteObject
            {
                Type = RemoteObjectTypeValues.Boolean,
                Value = Json(value.AsBoolean() ? "true" : "false"),
            };
        }

        if (value.IsString())
        {
            return new RemoteObject
            {
                Type = RemoteObjectTypeValues.String,
                Value = Json(JsonString(value.AsString())),
            };
        }

        if (value.IsNumber())
        {
            return Number(value);
        }

        if (value.IsBigInt())
        {
            var text = TypeConverter.ToString(value) + "n";
            return new RemoteObject
            {
                Type = RemoteObjectTypeValues.Bigint,
                UnserializableValue = text,
                Description = text,
            };
        }

        if (value.IsSymbol())
        {
            return new RemoteObject
            {
                Type = RemoteObjectTypeValues.Symbol,
                Description = value.ToString(),
            };
        }

        return byValue ? ByValue(value) : Described(value);
    }

    /// <summary>
    /// Describes what a thrown <see cref="JavaScriptException"/> becomes, in the shape the front end reads.
    /// </summary>
    internal static ExceptionDetails Exception(JavaScriptException exception, int exceptionId, int executionContextId)
    {
        var location = exception.Location;

        return new ExceptionDetails
        {
            ExceptionId = exceptionId,

            // V8's word for "this came out of an evaluation rather than out of a compile". The front end
            // prints it in front of the exception's own description.
            Text = "Uncaught",

            // The protocol counts lines from zero and Acornima counts them from one; its columns are
            // already zero-based. A location the engine never filled in reads as the start of the script.
            LineNumber = Math.Max(0, location.Start.Line - 1),
            ColumnNumber = Math.Max(0, location.Start.Column),
            Url = string.IsNullOrEmpty(location.SourceFile) ? null : location.SourceFile,
            ExecutionContextId = executionContextId,
            Exception = ThrownValue(exception),
        };
    }

    /// <summary>
    /// Describes a promise rejection, which carries no location because nothing threw where the client is
    /// standing.
    /// </summary>
    internal static ExceptionDetails Rejection(JsValue reason, int exceptionId, int executionContextId)
    {
        return new ExceptionDetails
        {
            ExceptionId = exceptionId,

            // V8's wording for a rejection reported through a command rather than through a throw, and what
            // the front end prints in front of the reason.
            Text = "Uncaught (in promise)",
            LineNumber = 0,
            ColumnNumber = 0,
            ExecutionContextId = executionContextId,
            Exception = Describe(reason, byValue: false),
        };
    }

    private static RemoteObject ThrownValue(JavaScriptException exception)
    {
        var described = Describe(exception.Error, byValue: false);

        // The description a client renders for an error is the message plus the stack, which is what the
        // engine already renders for its own exception text; the inspector's one-liner is the class name.
        return described with { Description = exception.GetJavaScriptErrorString() };
    }

    private static RemoteObject Number(JsValue value)
    {
        var number = value.AsNumber();

        if (double.IsNaN(number))
        {
            return Unserializable("NaN");
        }

        if (double.IsPositiveInfinity(number))
        {
            return Unserializable("Infinity");
        }

        if (double.IsNegativeInfinity(number))
        {
            return Unserializable("-Infinity");
        }

        if (number == 0 && double.IsNegative(number))
        {
            // JSON has no negative zero, and a client that read 0 back would have been told something false.
            return Unserializable("-0");
        }

        return new RemoteObject
        {
            Type = RemoteObjectTypeValues.Number,
            Value = Json(number.ToString("R", CultureInfo.InvariantCulture)),

            // The engine's own number-to-string, so what a client shows is what the script would print.
            Description = TypeConverter.ToString(value),
        };

        static RemoteObject Unserializable(string text) => new()
        {
            Type = RemoteObjectTypeValues.Number,
            UnserializableValue = text,
            Description = text,
        };
    }

#pragma warning disable JINT0002 // ValueInspector is the engine's preview describer; this is what it is for
    private static RemoteObject Described(JsValue value)
    {
        var description = ValueInspector.Describe(value, DescribeOnly);
        var (type, subtype) = Kind(description.Kind);

        return new RemoteObject
        {
            Type = type,
            Subtype = subtype,
            ClassName = description.ClassName,
            Description = description.Description,
        };
    }

    private static (string Type, string? Subtype) Kind(ValueKind kind) => kind switch
    {
        ValueKind.Function => (RemoteObjectTypeValues.Function, null),
        ValueKind.Array => (RemoteObjectTypeValues.Object, RemoteObjectSubtypeValues.Array),
        ValueKind.TypedArray => (RemoteObjectTypeValues.Object, RemoteObjectSubtypeValues.Typedarray),
        ValueKind.ArrayBuffer => (RemoteObjectTypeValues.Object, RemoteObjectSubtypeValues.Arraybuffer),
        ValueKind.DataView => (RemoteObjectTypeValues.Object, RemoteObjectSubtypeValues.Dataview),
        ValueKind.Map => (RemoteObjectTypeValues.Object, RemoteObjectSubtypeValues.Map),
        ValueKind.Set => (RemoteObjectTypeValues.Object, RemoteObjectSubtypeValues.Set),
        ValueKind.WeakMap => (RemoteObjectTypeValues.Object, RemoteObjectSubtypeValues.Weakmap),
        ValueKind.WeakSet => (RemoteObjectTypeValues.Object, RemoteObjectSubtypeValues.Weakset),
        ValueKind.Promise => (RemoteObjectTypeValues.Object, RemoteObjectSubtypeValues.Promise),
        ValueKind.Error => (RemoteObjectTypeValues.Object, RemoteObjectSubtypeValues.Error),
        ValueKind.Date => (RemoteObjectTypeValues.Object, RemoteObjectSubtypeValues.Date),
        ValueKind.RegExp => (RemoteObjectTypeValues.Object, RemoteObjectSubtypeValues.Regexp),
        ValueKind.Proxy => (RemoteObjectTypeValues.Object, RemoteObjectSubtypeValues.Proxy),
        ValueKind.Generator => (RemoteObjectTypeValues.Object, RemoteObjectSubtypeValues.Generator),
        ValueKind.Iterator => (RemoteObjectTypeValues.Object, RemoteObjectSubtypeValues.Iterator),

        // A WeakRef, an arguments object, a CLR value projected into script and everything ordinary: the
        // protocol has no subtype for any of them, and inventing one would be a lie a client acts on.
        _ => (RemoteObjectTypeValues.Object, null),
    };
#pragma warning restore JINT0002

    private static RemoteObject ByValue(JsValue value)
    {
        // A function has no JSON form -- JSON.stringify answers undefined for one -- so the client gets the
        // description it would have got anyway rather than a null it cannot tell from a real one.
        if (value.IsCallable())
        {
            return Described(value);
        }

        var buffer = new ArrayBufferWriter<byte>(256);
        string? refusal;

        using (var writer = new Utf8JsonWriter(buffer))
        {
            var state = new ByValueWriter(writer);
            var written = state.Write(value, depth: 0);
            refusal = written ? null : state.Refusal;
        }

        if (refusal is not null)
        {
            return Throw.ServerError<RemoteObject>("Object couldn't be returned by value", refusal);
        }

        return new RemoteObject
        {
            Type = RemoteObjectTypeValues.Object,
            Subtype = value.IsArray() ? RemoteObjectSubtypeValues.Array : null,
            Value = Json(Encoding.UTF8.GetString(buffer.WrittenSpan)),
        };
    }

    private static string JsonString(string text)
    {
        var buffer = new ArrayBufferWriter<byte>(text.Length + 16);
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStringValue(text);
        }

        return Encoding.UTF8.GetString(buffer.WrittenSpan);
    }

    private static JsonElement Json(string text)
    {
        using var document = JsonDocument.Parse(text);
        return document.RootElement.Clone();
    }

    /// <summary>
    /// One by-value serialization: <c>JSON.stringify</c>'s shape, bounded in depth and in how much it writes,
    /// and refusing a cycle rather than following it.
    /// </summary>
    private struct ByValueWriter(Utf8JsonWriter writer)
    {
        private readonly Utf8JsonWriter _writer = writer;
        private List<ObjectInstance>? _path;
        private int _nodes;

        /// <summary>Gets why the walk stopped, once <see cref="Write"/> has answered false.</summary>
        internal string? Refusal { get; private set; }

        internal bool Write(JsValue value, int depth)
        {
            if (++_nodes > MaxByValueNodes)
            {
                Refusal = "the value has more members than one by-value result may carry";
                return false;
            }

            if (value.IsNull() || value.IsUndefined())
            {
                // JSON.stringify writes undefined members as absent; a top-level undefined is answered as
                // null, which is what a client's own deserializer expects to find in the value member.
                _writer.WriteNullValue();
                return true;
            }

            if (value.IsBoolean())
            {
                _writer.WriteBooleanValue(value.AsBoolean());
                return true;
            }

            if (value.IsNumber())
            {
                var number = value.AsNumber();
                if (double.IsNaN(number) || double.IsInfinity(number))
                {
                    _writer.WriteNullValue();
                    return true;
                }

                _writer.WriteNumberValue(number);
                return true;
            }

            if (value.IsString())
            {
                _writer.WriteStringValue(value.AsString());
                return true;
            }

            if (value.IsBigInt() || value.IsSymbol())
            {
                Refusal = value.IsBigInt()
                    ? "a BigInt has no JSON representation"
                    : "a symbol has no JSON representation";
                return false;
            }

            return WriteObject(value.AsObject(), depth);
        }

        private bool WriteObject(ObjectInstance instance, int depth)
        {
            if (depth >= MaxByValueDepth)
            {
                Refusal = "the value is nested deeper than one by-value result may carry";
                return false;
            }

            var path = _path ??= [];
            for (var i = 0; i < path.Count; i++)
            {
                if (ReferenceEquals(path[i], instance))
                {
                    Refusal = "the value refers to itself";
                    return false;
                }
            }

            path.Add(instance);
            try
            {
                return instance.IsArray() ? WriteArray(instance, depth) : WriteMembers(instance, depth);
            }
            finally
            {
                path.RemoveAt(path.Count - 1);
            }
        }

        private bool WriteArray(ObjectInstance instance, int depth)
        {
            var length = instance.Get("length");
            var count = length.IsNumber() ? (long) length.AsNumber() : 0;

            _writer.WriteStartArray();
            for (long index = 0; index < count; index++)
            {
                if (!Write(instance.Get(index.ToString(CultureInfo.InvariantCulture)), depth + 1))
                {
                    return false;
                }
            }

            _writer.WriteEndArray();
            return true;
        }

        private bool WriteMembers(ObjectInstance instance, int depth)
        {
            _writer.WriteStartObject();
            foreach (var property in instance.GetOwnProperties())
            {
                if (!property.Value.Enumerable || !property.Key.IsString())
                {
                    continue;
                }

                var member = instance.Get(property.Key);
                if (member.IsUndefined() || member.IsCallable())
                {
                    // JSON.stringify omits both rather than writing them, and a client reading this back
                    // through its own deserializer expects the same.
                    continue;
                }

                _writer.WritePropertyName(property.Key.AsString());
                if (!Write(member, depth + 1))
                {
                    return false;
                }
            }

            _writer.WriteEndObject();
            return true;
        }
    }
}
