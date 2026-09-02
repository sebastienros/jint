using System.Buffers;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using Jint.Diagnostics;
using Jint.DevTools.Protocol.Runtime;
using Jint.Native;
using Jint.Runtime;
using JsJsonSerializer = Jint.Native.Json.JsonSerializer;

namespace Jint.DevTools.Domains;

/// <summary>
/// Turns one <see cref="JsValue"/> into the protocol's <c>Runtime.RemoteObject</c>, minting the handle a
/// client comes back with.
/// </summary>
/// <remarks>
/// <para>
/// One mapper per attachment, because a handle belongs to the attachment that asked for it: the identifiers
/// come from the target's <see cref="RemoteObjectTable"/>, and detaching releases everything this mapper
/// registered.
/// </para>
/// <para>
/// Runs on the engine thread, like everything else that touches a value, and answers a record of strings and
/// JSON that may then cross to a transport thread.
/// </para>
/// <para>
/// <b>Describing a value runs none of that value's code.</b> The type, subtype, class name, one-line
/// description and preview all come from <see cref="ValueInspector"/>, the engine's own getter-free,
/// trap-free describer: an accessor is reported rather than called, a proxy is named by its kind because
/// every trap on it is script, and a CLR value is named rather than read.
/// </para>
/// <para>
/// <b>Sending a value <i>by value</i> is the deliberate exception.</b> That is <c>JSON.stringify</c>'s
/// contract — <c>toJSON</c> hooks and getters both — and a client that asked for <c>returnByValue</c> has
/// asked for them to run. V8 does the same thing for the same reason.
/// </para>
/// </remarks>
internal sealed class RemoteObjectMapper
{
    private static readonly RemoteObject UndefinedObject = new() { Type = RemoteObjectTypeValues.Undefined };

    private static readonly RemoteObject NullObject = new()
    {
        Type = RemoteObjectTypeValues.Object,
        Subtype = RemoteObjectSubtypeValues.Null,
        Value = Json("null"),
    };

#pragma warning disable JINT0002 // ValueInspector is the engine's preview describer; this is what it is for
    /// <summary>The header alone: no entries, because a walk nobody reads is work the engine thread should not do.</summary>
    private static readonly ValueInspectorOptions DescribeOnly = new() { MaxDepth = 0, MaxEntries = 0, MaxStringLength = 256 };

    /// <summary>
    /// What a preview is allowed to cost, which is the engine inspector's own default rather than a number
    /// invented here: a hundred entries, two levels, a hundred characters of any one string.
    /// </summary>
    private static readonly ValueInspectorOptions PreviewBounds = new();

    /// <summary>
    /// A function's declaration, which is what the front end parses <c>description</c> as. Unbounded in
    /// length on purpose — a truncated declaration is one the client cannot read the signature out of — and
    /// entry-free, because a function has none.
    /// </summary>
    private static readonly ValueInspectorOptions FunctionSource =
        new() { MaxDepth = 0, MaxEntries = 0, MaxStringLength = int.MaxValue, FunctionSourceText = true };
#pragma warning restore JINT0002

    private readonly DevToolsTarget _target;
    private readonly object _owner;

    internal RemoteObjectMapper(DevToolsTarget target, object owner)
    {
        _target = target;
        _owner = owner;
    }

    /// <summary>Gets the table this mapper mints handles from.</summary>
    internal RemoteObjectTable Table => _target.Runtime.RemoteObjects;

    /// <summary>Describes <paramref name="value"/> as the protocol's remote object.</summary>
    /// <param name="value">The value to describe.</param>
    /// <param name="request">Whether the client asked for the value itself, a preview, and which group to bill.</param>
    internal RemoteObject Describe(JsValue value, in RemoteObjectRequest request)
    {
        if (value.IsUndefined())
        {
            return UndefinedObject;
        }

        if (value.IsNull())
        {
            return NullObject;
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
            if (request.ByValue)
            {
                Throw.ServerError("Object couldn't be returned by value", "a BigInt has no JSON representation");
            }

            return new RemoteObject
            {
                Type = RemoteObjectTypeValues.Bigint,
                UnserializableValue = text,
                Description = text,
                ObjectId = Register(value, request),
            };
        }

        if (value.IsSymbol())
        {
            if (request.ByValue)
            {
                Throw.ServerError("Object couldn't be returned by value", "a symbol has no JSON representation");
            }

            return new RemoteObject
            {
                Type = RemoteObjectTypeValues.Symbol,
                Description = value.ToString(),
                ObjectId = Register(value, request),
            };
        }

        return request.ByValue ? ByValue(value) : Handle(value, request);
    }

    /// <summary>
    /// Describes what a thrown <see cref="JavaScriptException"/> becomes, in the shape the front end reads.
    /// </summary>
    internal ExceptionDetails Exception(JavaScriptException exception, int exceptionId, int executionContextId)
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
            Url = ScriptUrl.From(location.SourceFile) is { Length: > 0 } url ? url : null,
            ExecutionContextId = executionContextId,
            Exception = ThrownValue(exception),
        };
    }

    /// <summary>
    /// Describes an error <i>object</i> — one nobody threw here — the way <c>Runtime.getExceptionDetails</c>
    /// answers for it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The front end asks this of any handle whose subtype is <c>error</c>, which is how it draws an
    /// expandable stack under a value that was logged rather than thrown. So the details are reconstructed
    /// from the object rather than remembered from a report: <c>text</c> is the error's
    /// <c>name: message</c>, read as descriptors, and the frames come from its own <c>stack</c>.
    /// </para>
    /// <para>
    /// Reading <c>stack</c> is the same deliberate exception <see cref="ThrownValue"/> makes and is asked for
    /// under the same <see cref="ResultLimits"/>. An error whose stack a host's
    /// <c>Options.Interop.BuildCallStackHandler</c> renders in some other shape produces no
    /// <c>stackTrace</c> rather than a wrong one; the rendered text is in <c>exception.description</c>
    /// either way, which is where the front end reads it from when there are no frames.
    /// </para>
    /// </remarks>
    /// <param name="error">The value the client's <c>errorObjectId</c> resolved to.</param>
    /// <param name="exceptionId">The identifier this report is given.</param>
    /// <param name="executionContextId">The context the error belongs to.</param>
    /// <param name="scripts">The registry a frame's script identifier is resolved against, if there is one.</param>
    internal ExceptionDetails ErrorDetails(
        JsValue error,
        int exceptionId,
        int executionContextId,
        ScriptRegistry? scripts)
    {
#pragma warning disable JINT0002 // ValueInspector is the engine's preview describer; this is what it is for
        var described = ValueInspector.Describe(error, DescribeOnly);
#pragma warning restore JINT0002

        var frames = StackFrames(error, scripts);
        var top = frames is { Length: > 0 } ? frames[0] : null;

        return new ExceptionDetails
        {
            ExceptionId = exceptionId,

            // Chrome answers the error's own "Error: boom" here rather than the "Uncaught" it prefixes a
            // thrown one with: nothing was thrown, and the client is asking about a value it is holding.
            Text = described.Description,
            LineNumber = top?.LineNumber ?? 0,
            ColumnNumber = top?.ColumnNumber ?? 0,
            ScriptId = top?.ScriptId,
            Url = top is { Url.Length: > 0 } ? top.Url : null,
            StackTrace = frames is { Length: > 0 } ? new StackTrace { CallFrames = frames } : null,
            ExecutionContextId = executionContextId,
            Exception = ErrorValue(error),
        };
    }

    /// <summary>Whether a value is a JavaScript error object, which is what <c>getExceptionDetails</c> takes.</summary>
    internal static bool IsError(JsValue value)
    {
#pragma warning disable JINT0002 // ValueInspector is the engine's preview describer; this is what it is for
        return value.IsObject() && ValueInspector.Describe(value, DescribeOnly).Kind == ValueKind.Error;
#pragma warning restore JINT0002
    }

    /// <summary>
    /// The error's own <c>stack</c>, parsed into the protocol's frames, or <see langword="null"/> when there
    /// is nothing there this can read.
    /// </summary>
    /// <remarks>
    /// The engine renders a frame as <c>    at name (source:line:column)</c>, or without the name and its
    /// parentheses for one it cannot name. A line in any other shape ends the parse rather than being
    /// guessed at, so a host that replaced the rendering gets no frames instead of wrong ones.
    /// </remarks>
    private static CallFrame[]? StackFrames(JsValue error, ScriptRegistry? scripts)
    {
        string stack;
        try
        {
            var text = error.AsObject().Get("stack");
            if (!text.IsString())
            {
                return null;
            }

            stack = text.AsString();
        }
        catch (JavaScriptException)
        {
            // A script's own `stack` accessor that threw. The client still gets the error and its text.
            return null;
        }

        var frames = new List<CallFrame>();
        foreach (var line in stack.Split('\n'))
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0)
            {
                continue;
            }

            if (!TryReadFrame(trimmed, scripts, out var frame))
            {
                return null;
            }

            frames.Add(frame);
        }

        return frames.Count == 0 ? null : [.. frames];
    }

    private static bool TryReadFrame(string line, ScriptRegistry? scripts, out CallFrame frame)
    {
        frame = null!;

        if (!line.StartsWith("at ", StringComparison.Ordinal))
        {
            return false;
        }

        var rest = line.Substring(3);
        var name = "";

        if (rest.EndsWith(')'))
        {
            var open = rest.IndexOf(" (", StringComparison.Ordinal);
            if (open < 0)
            {
                return false;
            }

            name = rest.Substring(0, open);
            rest = rest.Substring(open + 2, rest.Length - open - 3);
        }

        // "source:line:column", where the source may itself hold colons — a Windows drive letter does.
        var lastColon = rest.LastIndexOf(':');
        if (lastColon <= 0)
        {
            return false;
        }

        var previousColon = rest.LastIndexOf(':', lastColon - 1);
        if (previousColon < 0)
        {
            return false;
        }

        if (!int.TryParse(rest.AsSpan(previousColon + 1, lastColon - previousColon - 1), NumberStyles.None, CultureInfo.InvariantCulture, out var lineNumber)
            || !int.TryParse(rest.AsSpan(lastColon + 1), NumberStyles.None, CultureInfo.InvariantCulture, out var column))
        {
            return false;
        }

        var source = rest.Substring(0, previousColon);
        var script = scripts?.At(source, lineNumber, Math.Max(0, column - 1));

        frame = new CallFrame
        {
            FunctionName = name,
            ScriptId = script?.ScriptId ?? "0",
            Url = script?.Url ?? ScriptUrl.From(source),

            // The engine counts both from one here; the protocol counts both from zero.
            LineNumber = Math.Max(0, lineNumber - 1),
            ColumnNumber = Math.Max(0, column - 1),
        };

        return true;
    }

    /// <summary>The error object itself, described with the message-and-stack text a front end prints.</summary>
    private RemoteObject ErrorValue(JsValue error)
    {
        var described = Describe(error, RemoteObjectRequest.Description);

        try
        {
            var stack = error.AsObject().Get("stack");
            if (stack.IsString() && stack.AsString().Length > 0)
            {
                return described with { Description = described.Description + System.Environment.NewLine + stack.AsString() };
            }
        }
        catch (JavaScriptException)
        {
            // The one-liner stands.
        }

        return described;
    }

    /// <summary>
    /// Describes a promise rejection, which carries no location because nothing threw where the client is
    /// standing.
    /// </summary>
    internal ExceptionDetails Rejection(JsValue reason, int exceptionId, int executionContextId)
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
            Exception = Describe(reason, RemoteObjectRequest.Description),
        };
    }

#pragma warning disable JINT0002 // ValueInspector is the engine's preview describer; this is what it is for
    /// <summary>
    /// Turns a description the engine already took into a remote object, for the values this package can see
    /// described but cannot hold.
    /// </summary>
    /// <remarks>
    /// One case, and it is a real gap rather than a shortcut: a settled promise's result. The engine
    /// publishes a promise's state and result as a <see cref="ValueDescription"/> and the value itself to
    /// nothing outside its own assembly, so <c>[[PromiseResult]]</c> is answered as what it is — type,
    /// subtype, class name and a one-line description — with no <c>value</c> and no handle.
    /// <c>Runtime.awaitPromise</c> is the command that hands the value itself over, and it does so by
    /// attaching reactions rather than by reading a slot.
    /// </remarks>
    internal static RemoteObject FromDescription(ValueDescription description)
    {
        var (type, subtype) = Kind(description.Kind);
        return new RemoteObject
        {
            Type = type,
            Subtype = subtype,
            ClassName = description.ClassName,
            Description = description.Description,
        };
    }
#pragma warning restore JINT0002

    /// <summary>Releases every handle this mapper minted, which is what detaching means.</summary>
    internal void ReleaseAll() => Table.ReleaseOwner(_owner);

    /// <summary>Releases every handle this mapper minted under <paramref name="objectGroup"/>.</summary>
    internal void ReleaseGroup(string objectGroup) => Table.ReleaseGroup(_owner, objectGroup);

    /// <summary>
    /// The thrown value, described with the message-and-stack text a front end prints in its console.
    /// </summary>
    /// <remarks>
    /// <b>This is the one place besides <c>returnByValue</c> where script may run.</b> Rendering an error's
    /// text reads its <c>stack</c>, which a script may have defined as an accessor — so the render is asked
    /// for under the engine's own <see cref="ResultLimits"/>, which bounds both what it may execute and how
    /// much it may produce. It is asked for at all because the stack is the whole reason a client is looking
    /// at the value; the inspector's getter-free one-liner is the class name alone.
    /// </remarks>
    private RemoteObject ThrownValue(JavaScriptException exception)
    {
        var described = Describe(exception.Error, RemoteObjectRequest.Description);
        return described with { Description = exception.GetJavaScriptErrorString(_target.Runtime.Engine.Options.ResultLimits) };
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
    private RemoteObject Handle(JsValue value, in RemoteObjectRequest request)
    {
        var description = ValueInspector.Describe(value, request.GeneratePreview ? PreviewBounds : DescribeOnly);
        var (type, subtype) = Kind(description.Kind);

        var className = description.ClassName;
        var text = description.Description;

        if (description.Kind == ValueKind.Function)
        {
            // The front end reads this field as Function.prototype.toString output and parses the name back
            // out of it, so a short label makes every function in a Scope pane render as "ƒ undefined()".
            // A second describe rather than one option set for both, because a function carries no entries:
            // the walk above already stopped, and asking every *nested* function in a preview for its
            // declaration would be building strings nothing sends.
            text = ValueInspector.Describe(value, FunctionSource).Description;
        }

        if (_target.Describer is { } describer && describer.TryDescribe(value, out var hint))
        {
            subtype = hint.Subtype ?? subtype;
            className = hint.ClassName ?? className;
            text = hint.Description ?? text;
        }

        return new RemoteObject
        {
            Type = type,
            Subtype = subtype,
            ClassName = className,
            Description = text,
            ObjectId = Register(value, request),

            // A function has no preview in Chrome either: what a client wants from one is its declaration,
            // which is already the description.
            Preview = request.GeneratePreview && type != RemoteObjectTypeValues.Function
                ? PreviewOf(description, type, subtype, text)
                : null,
        };
    }

    /// <summary>Builds the abbreviated form a front end renders inline, from a description already taken.</summary>
    private ObjectPreview PreviewOf(ValueDescription description, string type, string? subtype, string? text)
    {
        var isCollection = description.Kind is ValueKind.Map or ValueKind.Set;
        var entries = description.Entries;

        PropertyPreview[] properties;
        EntryPreview[]? previewEntries;

        if (isCollection)
        {
            properties = [];
            previewEntries = new EntryPreview[entries.Count];
            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                previewEntries[i] = new EntryPreview
                {
                    Key = entry.EntryKey is { } key ? Nested(key) : null,
                    Value = Nested(entry.Value),
                };
            }
        }
        else
        {
            previewEntries = null;
            properties = new PropertyPreview[entries.Count];
            for (var i = 0; i < entries.Count; i++)
            {
                properties[i] = PropertyPreviewOf(entries[i]);
            }
        }

        return new ObjectPreview
        {
            Type = type,
            Subtype = subtype,
            Description = text,
            Overflow = description.Overflow,
            Properties = properties,
            Entries = previewEntries,
        };
    }

    private PropertyPreview PropertyPreviewOf(ValueEntry entry)
    {
        if (entry.IsAccessor)
        {
            // The one property kind whose value nobody may ask for: reading it is a call, and this path
            // never calls. The front end renders the word and offers the client a button.
            return new PropertyPreview { Name = entry.Key, Type = PropertyPreviewTypeValues.Accessor };
        }

        var value = entry.Value;
        var (type, subtype) = PreviewKind(value.Kind);

        return new PropertyPreview
        {
            Name = entry.Key,
            Type = type,
            Subtype = subtype,

            // A function's preview value is the empty string, which is what Chrome sends: the front end
            // draws the ƒ from the type, and putting a declaration here would be a whole function body
            // inside an inline preview. Recorded from a real Chrome, not assumed.
            Value = value.Kind == ValueKind.Function ? "" : value.Description,
            ValuePreview = value.Entries.Count > 0 ? Nested(value) : null,
        };
    }

    private ObjectPreview Nested(ValueDescription description)
    {
        var (type, subtype) = PreviewKind(description.Kind);
        return PreviewOf(description, type, subtype, description.Description);
    }

    /// <summary>Serializes what a client asked for by value, exactly as <c>JSON.stringify</c> would.</summary>
    /// <remarks>
    /// <b>This runs script</b> — a <c>toJSON</c> hook, a getter, a proxy trap — and that is the contract the
    /// client asked for. Everything else on this path is getter-free; this one call is not, and V8's is not
    /// either. A value with no JSON form, and a cycle, are both refused in Chrome's wording.
    /// </remarks>
    private RemoteObject ByValue(JsValue value)
    {
        // A function has no JSON form -- JSON.stringify answers undefined for one -- so the client gets the
        // description it would have got anyway rather than a null it cannot tell from a real one.
        if (value.IsCallable())
        {
            return Handle(value, RemoteObjectRequest.Description with { Addressable = false });
        }

        var buffer = new ArrayBufferWriter<byte>(256);
        bool written;

        try
        {
            written = new JsJsonSerializer(_target.Runtime.Engine).Serialize(value, buffer);
        }
        catch (JavaScriptException exception)
        {
            // A cycle is the common one -- JSON.stringify raises a TypeError for it -- and a toJSON that
            // threw arrives the same way. Either is the client's answer to "send me this by value": no.
            return Throw.ServerError<RemoteObject>("Object couldn't be returned by value", exception.Message);
        }

        if (!written)
        {
            return Throw.ServerError<RemoteObject>("Object couldn't be returned by value", "the value has no JSON representation");
        }

        return new RemoteObject
        {
            Type = RemoteObjectTypeValues.Object,
            Subtype = value.IsArray() ? RemoteObjectSubtypeValues.Array : null,
            Value = Json(Encoding.UTF8.GetString(buffer.WrittenSpan)),
        };
    }

    private string? Register(JsValue value, in RemoteObjectRequest request)
        => request.Addressable ? Table.Register(_owner, value, request.ObjectGroup) : null;

    internal static (string Type, string? Subtype) Kind(ValueKind kind) => kind switch
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
        ValueKind.Null => (RemoteObjectTypeValues.Object, RemoteObjectSubtypeValues.Null),
        ValueKind.Undefined => (RemoteObjectTypeValues.Undefined, null),
        ValueKind.Boolean => (RemoteObjectTypeValues.Boolean, null),
        ValueKind.Number => (RemoteObjectTypeValues.Number, null),
        ValueKind.BigInt => (RemoteObjectTypeValues.Bigint, null),
        ValueKind.String => (RemoteObjectTypeValues.String, null),
        ValueKind.Symbol => (RemoteObjectTypeValues.Symbol, null),

        // A WeakRef, an arguments object, a CLR value projected into script and everything ordinary: the
        // protocol has no subtype for any of them, and inventing one would be a lie a client acts on.
        _ => (RemoteObjectTypeValues.Object, null),
    };
#pragma warning restore JINT0002

#pragma warning disable JINT0002 // ValueInspector is the engine's preview describer; this is what it is for
    /// <summary>
    /// The same mapping in the preview's own vocabulary, which differs in exactly one place: a property
    /// whose value is an accessor has a type of its own there and no equivalent as a remote object.
    /// </summary>
    private static (string Type, string? Subtype) PreviewKind(ValueKind kind)
    {
        return kind == ValueKind.Accessor
            ? (PropertyPreviewTypeValues.Accessor, null)
            : Kind(kind);
    }
#pragma warning restore JINT0002

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
}

/// <summary>
/// What one client asked for when it asked about a value: the value itself or a description of it, whether
/// to abbreviate what is inside, and which group releases the handle.
/// </summary>
[StructLayout(LayoutKind.Auto)]
internal readonly record struct RemoteObjectRequest
{
    /// <summary>A described value with a handle and no preview, which is what most commands want.</summary>
    internal static RemoteObjectRequest Description { get; } = new() { Addressable = true };

    /// <summary>Gets whether the client asked for the value itself rather than a description of it.</summary>
    internal bool ByValue { get; init; }

    /// <summary>Gets whether the client asked for an abbreviation of what is inside the value.</summary>
    internal bool GeneratePreview { get; init; }

    /// <summary>Gets whether a handle is minted, which every command a client can come back from wants.</summary>
    internal bool Addressable { get; init; }

    /// <summary>Gets the symbolic group <c>Runtime.releaseObjectGroup</c> frees, if the client named one.</summary>
    internal string? ObjectGroup { get; init; }

    /// <summary>Reads the three members every command spells the same way.</summary>
    internal static RemoteObjectRequest From(bool? byValue, bool? generatePreview, string? objectGroup) => new()
    {
        ByValue = byValue == true,
        GeneratePreview = generatePreview == true,
        Addressable = byValue != true,
        ObjectGroup = objectGroup,
    };
}
