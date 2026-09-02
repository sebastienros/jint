#if NET8_0_OR_GREATER
using System.Diagnostics;
using System.Globalization;
using System.Text;
using Jint.Native;
using Jint.Native.Error;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.CallStack;
using Jint.Runtime.Descriptors;

namespace Jint.WebApi.Console;

/// <summary>
/// The <c>console</c> namespace object.
/// <para>
/// https://console.spec.whatwg.org/
/// </para>
/// </summary>
/// <remarks>
/// <para>
/// The engine does all of the specification's <i>Formatter</i> work and the parts of <i>Printer</i> that a
/// non-interactive console can do — group indentation, counter and timer labels — and hands
/// <see cref="ConsoleSink"/> one <see cref="ConsoleRecord"/> per call. Exactly one
/// <see cref="ConsoleSink.Write(in ConsoleRecord)"/> call is made per invocation, except for the two the
/// specification abandons at its first step (a logging method with no arguments,
/// <see href="https://console.spec.whatwg.org/#logger">Logger</see> step 1, and <c>assert</c> on a truthy
/// condition), which make none. A method that acts without printing — <c>groupEnd</c>, <c>countReset</c> on
/// an existing counter, <c>time</c> on a new label — reports a record whose message is
/// <see langword="null"/>, so the default forwarding never reaches
/// <see cref="ConsoleSink.Write(ConsoleLogLevel, string)"/> and a string-only sink sees exactly what it saw
/// before the record overload existed.
/// </para>
/// <para>
/// Counter, timer and group state is per instance, and the instance is per realm, so <c>console === console</c>
/// holds and two engines never share a counter.
/// </para>
/// <para>
/// Its <c>[[Prototype]]</c> is a private empty object rather than <c>%Object.prototype%</c>, which
/// https://console.spec.whatwg.org/#console-namespace requires of this namespace object and of no other:
/// "For historical web-compatibility reasons, the namespace object for console must have as its
/// <c>[[Prototype]]</c> an empty object, created as if by <c>ObjectCreate(%ObjectPrototype%)</c>, instead of
/// <c>%ObjectPrototype%</c>." The rule exists because libraries decorate logging by patching
/// <c>console.__proto__</c>, and it is a containment boundary as much as a conformance one: with
/// <c>%ObjectPrototype%</c> there, <c>console.__proto__.foo = …</c> would define <c>foo</c> on every object
/// in the realm. The members stay own properties of this object, exactly as they are in a browser and in
/// Node, so the empty object above it is only ever a landing pad.
/// </para>
/// <para>
/// The methods carry a WebIDL namespace object's property attributes —
/// https://webidl.spec.whatwg.org/#es-namespaces defines them with the same "define the operations" steps an
/// interface's, so <c>{ writable: true, enumerable: true, configurable: true }</c> — which is why
/// <c>Object.keys(console)</c> is non-empty here exactly as it is in Node. One documented simplification
/// remains: the object is installed as an ordinary enumerable data property of the global rather than
/// through an accessor pair, which nothing but code inspecting property attributes can observe.
/// </para>
/// <para>
/// Not implemented: <c>clear</c>, <c>dirxml</c>, <c>profile</c> and <c>profileEnd</c>, none of which acts on
/// anything a string sink has.
/// </para>
/// </remarks>
[JsObject]
internal sealed partial class ConsoleInstance : BuiltinShapeObject
{
    private const string DefaultLabel = "default";
    private const string AssertionFailed = "Assertion failed";

    [JsSymbol("ToStringTag", Flags = PropertyFlag.Configurable)]
    private static readonly JsString ConsoleToStringTag = new("console");

    private readonly Realm _realm;

    /// <summary>The Console Standard's "count map", https://console.spec.whatwg.org/#count-map.</summary>
    private Dictionary<string, int>? _counts;

    /// <summary>The Console Standard's "timer table", https://console.spec.whatwg.org/#timer-table.</summary>
    private Dictionary<string, long>? _timers;

    /// <summary>The Console Standard's "group stack" depth, https://console.spec.whatwg.org/#group-stack.</summary>
    private int _groupDepth;

    /// <summary>
    /// The <c>trace</c> dispatcher itself, so the stack it renders can leave out its own frame the same way
    /// <c>new Error().stack</c> leaves out the <c>Error</c> constructor's.
    /// </summary>
    private Native.Function.Function _traceFunction = null!;

    internal ConsoleInstance(Engine engine, Realm realm, ObjectPrototype objectPrototype) : base(engine)
    {
        _realm = realm;

        // https://console.spec.whatwg.org/#console-namespace — the namespace object's [[Prototype]] is an
        // empty object created as if by ObjectCreate(%ObjectPrototype%), not %ObjectPrototype% itself. It is
        // built here rather than in the intrinsics because it belongs to this object and to nothing else: an
        // engine whose script never names `console` never reaches this constructor, so the extra object costs
        // an engine that does not use the feature nothing at all.
        _prototype = OrdinaryObjectCreate(engine, objectPrototype);
    }

    protected override void Initialize()
    {
        CreateProperties_Generated();
        CreateSymbols_Generated();
    }

    /// <summary>
    /// https://console.spec.whatwg.org/#log
    /// </summary>
    [JsFunction(Name = "log", Length = 0, Flags = PropertyFlag.ConfigurableEnumerableWritable)]
    private JsValue Log(JsValue thisObject, [Rest] ReadOnlySpan<JsValue> data)
    {
        Logger(ConsoleMethod.Log, ConsoleLogLevel.Log, data);
        return Undefined;
    }

    /// <summary>
    /// https://console.spec.whatwg.org/#info
    /// </summary>
    [JsFunction(Name = "info", Length = 0, Flags = PropertyFlag.ConfigurableEnumerableWritable)]
    private JsValue Info(JsValue thisObject, [Rest] ReadOnlySpan<JsValue> data)
    {
        Logger(ConsoleMethod.Info, ConsoleLogLevel.Info, data);
        return Undefined;
    }

    /// <summary>
    /// https://console.spec.whatwg.org/#warn
    /// </summary>
    [JsFunction(Name = "warn", Length = 0, Flags = PropertyFlag.ConfigurableEnumerableWritable)]
    private JsValue Warn(JsValue thisObject, [Rest] ReadOnlySpan<JsValue> data)
    {
        Logger(ConsoleMethod.Warn, ConsoleLogLevel.Warn, data);
        return Undefined;
    }

    /// <summary>
    /// https://console.spec.whatwg.org/#error
    /// </summary>
    [JsFunction(Name = "error", Length = 0, Flags = PropertyFlag.ConfigurableEnumerableWritable)]
    private JsValue Error(JsValue thisObject, [Rest] ReadOnlySpan<JsValue> data)
    {
        Logger(ConsoleMethod.Error, ConsoleLogLevel.Error, data);
        return Undefined;
    }

    /// <summary>
    /// https://console.spec.whatwg.org/#debug
    /// </summary>
    [JsFunction(Name = "debug", Length = 0, Flags = PropertyFlag.ConfigurableEnumerableWritable)]
    private JsValue Debug(JsValue thisObject, [Rest] ReadOnlySpan<JsValue> data)
    {
        Logger(ConsoleMethod.Debug, ConsoleLogLevel.Debug, data);
        return Undefined;
    }

    /// <summary>
    /// https://console.spec.whatwg.org/#trace
    /// </summary>
    [JsFunction(Name = "trace", Length = 0, CaptureField = nameof(_traceFunction), Flags = PropertyFlag.ConfigurableEnumerableWritable)]
    private JsValue Trace(JsValue thisObject, [Rest] ReadOnlySpan<JsValue> data)
    {
        var header = data.Length == 0 ? "Trace" : "Trace: " + ConsoleFormatter.Format(data);

        // The same machinery Error uses for `stack`, with this dispatcher's own frame excluded so the trace
        // starts at the call site.
        var capture = ErrorConstructor.BuildStackTraceCapture(_engine, _traceFunction);
        var stack = capture?.Render() ?? string.Empty;

        Emit(
            ConsoleMethod.Trace,
            ConsoleLogLevel.Error,
            data,
            stack.Length == 0 ? header : header + System.Environment.NewLine + stack,
            StackFrames(capture));
        return Undefined;
    }

    /// <summary>
    /// https://console.spec.whatwg.org/#assert
    /// </summary>
    [JsFunction(Name = "assert", Length = 0, Flags = PropertyFlag.ConfigurableEnumerableWritable)]
    private JsValue Assert(JsValue thisObject, JsValue condition, [Rest] ReadOnlySpan<JsValue> data)
    {
        if (TypeConverter.ToBoolean(condition))
        {
            return Undefined;
        }

        if (data.Length == 0)
        {
            Emit(ConsoleMethod.Assert, ConsoleLogLevel.Error, data, AssertionFailed);
            return Undefined;
        }

        if (data[0] is JsString first)
        {
            // Step 4: the label is folded into the format string, so its specifiers still substitute.
            var args = new JsValue[data.Length];
            data.CopyTo(args);
            args[0] = JsString.Create(AssertionFailed + ": " + first.ToString());
            Emit(ConsoleMethod.Assert, ConsoleLogLevel.Error, data, ConsoleFormatter.Format(args));
            return Undefined;
        }

        // Step 3: a non-string first argument cannot be a format string, so the label is prepended instead.
        var prefixed = new JsValue[data.Length + 1];
        prefixed[0] = JsString.Create(AssertionFailed);
        data.CopyTo(prefixed.AsSpan(1));
        Emit(ConsoleMethod.Assert, ConsoleLogLevel.Error, data, ConsoleFormatter.Format(prefixed));
        return Undefined;
    }

    /// <summary>
    /// https://console.spec.whatwg.org/#dir
    /// </summary>
    [JsFunction(Name = "dir", Length = 0, Flags = PropertyFlag.ConfigurableEnumerableWritable)]
    private JsValue Dir(JsValue thisObject, JsValue item, JsValue options, [ArgCount] int argumentCount)
    {
        // `options` is part of the IDL signature and has no member a string sink could honour.
        Emit(ConsoleMethod.Dir, ConsoleLogLevel.Log, Given(argumentCount, item, options), ConsoleFormatter.Inspect(item));
        return Undefined;
    }

    /// <summary>
    /// https://console.spec.whatwg.org/#table — "Try to construct a table with the columns of the properties
    /// of tabularData (or use properties) and rows of tabularData and log it with a logLevel of 'log'. Fall
    /// back to just logging the argument if it can't be parsed as tabular."
    /// </summary>
    /// <remarks>
    /// That sentence, followed by "TODO: This will need a good algorithm", is the entire normative text; the
    /// table itself is drawn by <see cref="ConsoleFormatter.TryFormatTable"/>, whose documentation states the
    /// interpretation. Either way it is one <see cref="ConsoleSink.Write(ConsoleLogLevel, string)"/> call at
    /// <see cref="ConsoleLogLevel.Log"/> — a table is a single record however many lines it occupies, and
    /// group indentation applies to all of them.
    /// </remarks>
    [JsFunction(Name = "table", Length = 0, Flags = PropertyFlag.ConfigurableEnumerableWritable)]
    private JsValue Table(JsValue thisObject, JsValue tabularData, JsValue properties, [ArgCount] int argumentCount)
    {
        // The sequence conversion runs first and can raise a TypeError, exactly as WebIDL specifies, so a
        // non-iterable second argument fails before anything is logged.
        var columns = ReadProperties(properties);
        var arguments = Given(argumentCount, tabularData, properties);

        if (ConsoleFormatter.TryFormatTable(tabularData, columns, out var table))
        {
            Emit(ConsoleMethod.Table, ConsoleLogLevel.Log, arguments, table);
            return Undefined;
        }

        Emit(ConsoleMethod.Table, ConsoleLogLevel.Log, arguments, ConsoleFormatter.Format(new[] { tabularData }));
        return Undefined;
    }

    /// <summary>
    /// <c>console.timeStamp(label)</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Not part of the Console Standard.</b> https://console.spec.whatwg.org/ specifies <c>time</c>,
    /// <c>timeLog</c> and <c>timeEnd</c> and nothing else about timing; <c>timeStamp</c> is a de-facto member
    /// every browser and Node exposes, where it drops a marker onto a profiler's timeline and prints nothing.
    /// There is no timeline here, so the only thing a string sink can do with the marker is emit it, which is
    /// also the only behaviour a script can verify.
    /// </para>
    /// <para>
    /// The label defaults to <c>"default"</c>, which is the IDL default every labelled method in the standard
    /// carries and therefore the least surprising choice for one that has no IDL.
    /// </para>
    /// </remarks>
    [JsFunction(Name = "timeStamp", Length = 0, Flags = PropertyFlag.ConfigurableEnumerableWritable)]
    private JsValue TimeStamp(JsValue thisObject, JsValue label, [ArgCount] int argumentCount)
    {
        Emit(ConsoleMethod.TimeStamp, ConsoleLogLevel.Log, Given(argumentCount, label), Label(label));
        return Undefined;
    }

    /// <summary>
    /// https://console.spec.whatwg.org/#group
    /// </summary>
    [JsFunction(Name = "group", Length = 0, Flags = PropertyFlag.ConfigurableEnumerableWritable)]
    private JsValue Group(JsValue thisObject, [Rest] ReadOnlySpan<JsValue> data)
    {
        return BeginGroup(ConsoleMethod.Group, data);
    }

    /// <summary>
    /// https://console.spec.whatwg.org/#groupcollapsed
    /// <para>
    /// A string sink cannot collapse anything, so this behaves exactly like <c>group</c> — which is what the
    /// specification says a non-interactive printer should do.
    /// </para>
    /// </summary>
    [JsFunction(Name = "groupCollapsed", Length = 0, Flags = PropertyFlag.ConfigurableEnumerableWritable)]
    private JsValue GroupCollapsed(JsValue thisObject, [Rest] ReadOnlySpan<JsValue> data)
    {
        return BeginGroup(ConsoleMethod.GroupCollapsed, data);
    }

    /// <summary>
    /// https://console.spec.whatwg.org/#groupend
    /// </summary>
    [JsFunction(Name = "groupEnd", Length = 0, Flags = PropertyFlag.ConfigurableEnumerableWritable)]
    private JsValue GroupEnd(JsValue thisObject)
    {
        if (_groupDepth > 0)
        {
            _groupDepth--;
        }

        // Nothing is printed, so no string sink hears it -- but a structured one does, and without it a
        // consumer tracking the group stack could never close one.
        Emit(ConsoleMethod.GroupEnd, ConsoleLogLevel.Log, ReadOnlySpan<JsValue>.Empty, message: null);
        return Undefined;
    }

    /// <summary>
    /// https://console.spec.whatwg.org/#count
    /// </summary>
    [JsFunction(Name = "count", Length = 0, Flags = PropertyFlag.ConfigurableEnumerableWritable)]
    private JsValue Count(JsValue thisObject, JsValue label, [ArgCount] int argumentCount)
    {
        var key = Label(label);
        _counts ??= new Dictionary<string, int>(StringComparer.Ordinal);
        _counts.TryGetValue(key, out var count);
        count++;
        _counts[key] = count;

        Emit(
            ConsoleMethod.Count,
            ConsoleLogLevel.Log,
            Given(argumentCount, label),
            key + ": " + count.ToString(CultureInfo.InvariantCulture));
        return Undefined;
    }

    /// <summary>
    /// https://console.spec.whatwg.org/#countreset
    /// </summary>
    [JsFunction(Name = "countReset", Length = 0, Flags = PropertyFlag.ConfigurableEnumerableWritable)]
    private JsValue CountReset(JsValue thisObject, JsValue label, [ArgCount] int argumentCount)
    {
        var key = Label(label);
        var arguments = Given(argumentCount, label);
        if (_counts is not null && _counts.ContainsKey(key))
        {
            _counts[key] = 0;
            Emit(ConsoleMethod.CountReset, ConsoleLogLevel.Log, arguments, message: null);
        }
        else
        {
            Emit(ConsoleMethod.CountReset, ConsoleLogLevel.Warn, arguments, "Count for '" + key + "' does not exist");
        }

        return Undefined;
    }

    /// <summary>
    /// https://console.spec.whatwg.org/#time
    /// </summary>
    [JsFunction(Name = "time", Length = 0, Flags = PropertyFlag.ConfigurableEnumerableWritable)]
    private JsValue Time(JsValue thisObject, JsValue label, [ArgCount] int argumentCount)
    {
        var key = Label(label);
        var arguments = Given(argumentCount, label);
        _timers ??= new Dictionary<string, long>(StringComparer.Ordinal);
        if (_timers.ContainsKey(key))
        {
            Emit(ConsoleMethod.Time, ConsoleLogLevel.Warn, arguments, "Timer '" + key + "' already exists");
            return Undefined;
        }

        _timers[key] = Stopwatch.GetTimestamp();
        Emit(ConsoleMethod.Time, ConsoleLogLevel.Log, arguments, message: null);
        return Undefined;
    }

    /// <summary>
    /// https://console.spec.whatwg.org/#timelog
    /// </summary>
    [JsFunction(Name = "timeLog", Length = 0, Flags = PropertyFlag.ConfigurableEnumerableWritable)]
    private JsValue TimeLog(JsValue thisObject, JsValue label, [ArgCount] int argumentCount, [Rest] ReadOnlySpan<JsValue> data)
    {
        var key = Label(label);
        var arguments = Given(argumentCount, label, data);
        if (_timers is null || !_timers.TryGetValue(key, out var start))
        {
            Emit(ConsoleMethod.TimeLog, ConsoleLogLevel.Warn, arguments, "Timer '" + key + "' does not exist");
            return Undefined;
        }

        var concat = key + ": " + FormatDuration(start);
        if (data.Length == 0)
        {
            Emit(ConsoleMethod.TimeLog, ConsoleLogLevel.Log, arguments, concat);
            return Undefined;
        }

        var prefixed = new JsValue[data.Length + 1];
        prefixed[0] = JsString.Create(concat);
        data.CopyTo(prefixed.AsSpan(1));
        Emit(ConsoleMethod.TimeLog, ConsoleLogLevel.Log, arguments, ConsoleFormatter.Format(prefixed));
        return Undefined;
    }

    /// <summary>
    /// https://console.spec.whatwg.org/#timeend
    /// </summary>
    [JsFunction(Name = "timeEnd", Length = 0, Flags = PropertyFlag.ConfigurableEnumerableWritable)]
    private JsValue TimeEnd(JsValue thisObject, JsValue label, [ArgCount] int argumentCount)
    {
        var key = Label(label);
        var arguments = Given(argumentCount, label);
        if (_timers is null || !_timers.Remove(key, out var start))
        {
            Emit(ConsoleMethod.TimeEnd, ConsoleLogLevel.Warn, arguments, "Timer '" + key + "' does not exist");
            return Undefined;
        }

        Emit(ConsoleMethod.TimeEnd, ConsoleLogLevel.Log, arguments, key + ": " + FormatDuration(start));
        return Undefined;
    }

    private JsValue BeginGroup(ConsoleMethod method, ReadOnlySpan<JsValue> data)
    {
        // The label is printed at the current indentation; only what follows it is indented further.
        Emit(method, ConsoleLogLevel.Log, data, data.Length > 0 ? ConsoleFormatter.Format(data) : null);

        _groupDepth++;
        return Undefined;
    }

    /// <summary>
    /// https://console.spec.whatwg.org/#logger — including step 1, which is why a console method called with
    /// no arguments emits nothing at all rather than an empty line.
    /// </summary>
    private void Logger(ConsoleMethod method, ConsoleLogLevel level, ReadOnlySpan<JsValue> data)
    {
        if (data.Length == 0)
        {
            return;
        }

        Emit(method, level, data, ConsoleFormatter.Format(data));
    }

    /// <summary>
    /// The one place a record reaches the host: applies the group indentation and makes exactly one
    /// <see cref="ConsoleSink.Write(in ConsoleRecord)"/> call, whose default implementation forwards a
    /// printed record on to the string overload. The sink is read afresh every time, so a host may swap it
    /// between evaluations.
    /// </summary>
    /// <param name="method">Which console method is reporting.</param>
    /// <param name="level">The severity that method implies.</param>
    /// <param name="arguments">The arguments the caller passed, control arguments already removed.</param>
    /// <param name="message">The finished line, or <see langword="null"/> when the method printed nothing.</param>
    /// <param name="stackTrace">The captured frames, for <c>console.trace</c> alone.</param>
    private void Emit(
        ConsoleMethod method,
        ConsoleLogLevel level,
        ReadOnlySpan<JsValue> arguments,
        string? message,
        ConsoleStackFrame[]? stackTrace = null)
    {
        var materialized = arguments.Length == 0 ? [] : arguments.ToArray();
        Emit(method, level, materialized, message, stackTrace);
    }

    /// <inheritdoc cref="Emit(ConsoleMethod, ConsoleLogLevel, ReadOnlySpan{JsValue}, string, ConsoleStackFrame[])"/>
    private void Emit(
        ConsoleMethod method,
        ConsoleLogLevel level,
        JsValue[] arguments,
        string? message,
        ConsoleStackFrame[]? stackTrace = null)
    {
        var sink = _engine.Options.WebApi.Console.Sink ?? ConsoleSink.Null;
        var record = new ConsoleRecord(
            method,
            level,
            arguments,
            message is null ? null : Indent(message),
            _groupDepth,
            stackTrace);

        sink.Write(in record);
    }

    /// <summary>
    /// The prefix of <paramref name="candidates"/> the caller actually passed, so an argument nobody wrote
    /// is absent from a record rather than an <c>undefined</c> that looks written.
    /// </summary>
    private static JsValue[] Given(int argumentCount, params JsValue[] candidates)
    {
        var given = System.Math.Min(argumentCount, candidates.Length);
        if (given == candidates.Length)
        {
            return candidates;
        }

        return given <= 0 ? [] : candidates.AsSpan(0, given).ToArray();
    }

    /// <summary>The label a caller passed, if any, followed by the rest arguments.</summary>
    private static JsValue[] Given(int argumentCount, JsValue label, ReadOnlySpan<JsValue> data)
    {
        if (argumentCount <= 0)
        {
            return [];
        }

        var arguments = new JsValue[data.Length + 1];
        arguments[0] = label;
        data.CopyTo(arguments.AsSpan(1));
        return arguments;
    }

    /// <summary>
    /// The captured call stack as frames, taken from the very capture the rendered trace was rendered from.
    /// </summary>
    private static ConsoleStackFrame[]? StackFrames(ErrorStackCapture? capture)
    {
        if (capture is null)
        {
            return null;
        }

        var collected = capture.CollectFrames();
        var frames = new ConsoleStackFrame[collected.Count];
        for (var i = 0; i < frames.Length; i++)
        {
            var frame = collected[i];
            frames[i] = new ConsoleStackFrame(frame.FunctionName, frame.Source, frame.Line, frame.Column);
        }

        return frames;
    }

    private string Indent(string message)
    {
        if (_groupDepth == 0)
        {
            return message;
        }

        var prefix = new string(' ', _groupDepth * 2);
        if (message.Length == 0)
        {
            return prefix;
        }

        if (message.IndexOf('\n') < 0)
        {
            return prefix + message;
        }

        // A multi-line record (a trace, an inspected object containing newlines) is indented line by line, so
        // the group's shape survives.
        var builder = new StringBuilder(message.Length + prefix.Length * 2);
        builder.Append(prefix);
        foreach (var c in message)
        {
            builder.Append(c);
            if (c == '\n')
            {
                builder.Append(prefix);
            }
        }

        return builder.ToString();
    }

    private static string Label(JsValue label)
    {
        return label.IsUndefined() ? DefaultLabel : TypeConverter.ToString(label);
    }

    /// <summary>
    /// The <c>sequence&lt;DOMString&gt;</c> conversion of <c>table</c>'s second argument,
    /// https://webidl.spec.whatwg.org/#es-sequence: the value is iterated with the iterator protocol and each
    /// element is stringified. The argument is optional with no default value, so an omitted or explicitly
    /// <see langword="undefined"/> one is <see langword="null"/> — "not given" — rather than an empty column
    /// list, which would render a table with no columns at all.
    /// </summary>
    private List<string>? ReadProperties(JsValue properties)
    {
        if (properties.IsUndefined())
        {
            return null;
        }

        var result = new List<string>();
        var iterator = properties.GetIterator(_realm);

        try
        {
            while (iterator.TryIteratorStepValue(out var value))
            {
                result.Add(TypeConverter.ToString(value));
            }
        }
        catch
        {
            iterator.CloseIfNotDone(CompletionType.Throw);
            throw;
        }

        return result;
    }

    private static string FormatDuration(long startTimestamp)
    {
        var elapsed = Stopwatch.GetElapsedTime(startTimestamp);
        return elapsed.TotalMilliseconds.ToString("0.###", CultureInfo.InvariantCulture) + "ms";
    }
}
#endif
