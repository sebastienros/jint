#if NET8_0_OR_GREATER
using System.Diagnostics;
using System.Globalization;
using System.Text;
using Jint.Native;
using Jint.Native.Error;
using Jint.Native.Object;
using Jint.Runtime;
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
/// <see cref="ConsoleSink"/> one finished string per record. Exactly one
/// <see cref="ConsoleSink.Write(ConsoleLogLevel, string)"/> call is made per emitted record; a method that
/// emits nothing (<c>console.log()</c> with no arguments, <c>groupEnd</c>, <c>countReset</c> on an existing
/// counter, <c>assert</c> on a truthy condition) makes none.
/// </para>
/// <para>
/// Counter, timer and group state is per instance, and the instance is per realm, so <c>console === console</c>
/// holds and two engines never share a counter.
/// </para>
/// <para>
/// Two documented simplifications against WebIDL. The methods are non-enumerable, where a WebIDL namespace
/// object's operations are enumerable; and the object is installed as an ordinary enumerable data property
/// of the global rather than through an accessor pair. Neither is observable except to code inspecting
/// property attributes.
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
        _prototype = objectPrototype;
    }

    protected override void Initialize()
    {
        CreateProperties_Generated();
        CreateSymbols_Generated();
    }

    /// <summary>
    /// https://console.spec.whatwg.org/#log
    /// </summary>
    [JsFunction(Name = "log", Length = 0)]
    private JsValue Log(JsValue thisObject, [Rest] ReadOnlySpan<JsValue> data)
    {
        Logger(ConsoleLogLevel.Log, data);
        return Undefined;
    }

    /// <summary>
    /// https://console.spec.whatwg.org/#info
    /// </summary>
    [JsFunction(Name = "info", Length = 0)]
    private JsValue Info(JsValue thisObject, [Rest] ReadOnlySpan<JsValue> data)
    {
        Logger(ConsoleLogLevel.Info, data);
        return Undefined;
    }

    /// <summary>
    /// https://console.spec.whatwg.org/#warn
    /// </summary>
    [JsFunction(Name = "warn", Length = 0)]
    private JsValue Warn(JsValue thisObject, [Rest] ReadOnlySpan<JsValue> data)
    {
        Logger(ConsoleLogLevel.Warn, data);
        return Undefined;
    }

    /// <summary>
    /// https://console.spec.whatwg.org/#error
    /// </summary>
    [JsFunction(Name = "error", Length = 0)]
    private JsValue Error(JsValue thisObject, [Rest] ReadOnlySpan<JsValue> data)
    {
        Logger(ConsoleLogLevel.Error, data);
        return Undefined;
    }

    /// <summary>
    /// https://console.spec.whatwg.org/#debug
    /// </summary>
    [JsFunction(Name = "debug", Length = 0)]
    private JsValue Debug(JsValue thisObject, [Rest] ReadOnlySpan<JsValue> data)
    {
        Logger(ConsoleLogLevel.Debug, data);
        return Undefined;
    }

    /// <summary>
    /// https://console.spec.whatwg.org/#trace
    /// </summary>
    [JsFunction(Name = "trace", Length = 0, CaptureField = nameof(_traceFunction))]
    private JsValue Trace(JsValue thisObject, [Rest] ReadOnlySpan<JsValue> data)
    {
        var header = data.Length == 0 ? "Trace" : "Trace: " + ConsoleFormatter.Format(data);

        // The same machinery Error uses for `stack`, with this dispatcher's own frame excluded so the trace
        // starts at the call site.
        var capture = ErrorConstructor.BuildStackTraceCapture(_engine, _traceFunction);
        var stack = capture?.Render() ?? string.Empty;

        Emit(ConsoleLogLevel.Error, stack.Length == 0 ? header : header + System.Environment.NewLine + stack);
        return Undefined;
    }

    /// <summary>
    /// https://console.spec.whatwg.org/#assert
    /// </summary>
    [JsFunction(Name = "assert", Length = 0)]
    private JsValue Assert(JsValue thisObject, JsValue condition, [Rest] ReadOnlySpan<JsValue> data)
    {
        if (TypeConverter.ToBoolean(condition))
        {
            return Undefined;
        }

        if (data.Length == 0)
        {
            Emit(ConsoleLogLevel.Error, AssertionFailed);
            return Undefined;
        }

        if (data[0] is JsString first)
        {
            // Step 4: the label is folded into the format string, so its specifiers still substitute.
            var args = new JsValue[data.Length];
            data.CopyTo(args);
            args[0] = JsString.Create(AssertionFailed + ": " + first.ToString());
            Emit(ConsoleLogLevel.Error, ConsoleFormatter.Format(args));
            return Undefined;
        }

        // Step 3: a non-string first argument cannot be a format string, so the label is prepended instead.
        var prefixed = new JsValue[data.Length + 1];
        prefixed[0] = JsString.Create(AssertionFailed);
        data.CopyTo(prefixed.AsSpan(1));
        Emit(ConsoleLogLevel.Error, ConsoleFormatter.Format(prefixed));
        return Undefined;
    }

    /// <summary>
    /// https://console.spec.whatwg.org/#dir
    /// </summary>
    [JsFunction(Name = "dir", Length = 0)]
    private JsValue Dir(JsValue thisObject, JsValue item, JsValue options)
    {
        // `options` is part of the IDL signature and has no member a string sink could honour.
        Emit(ConsoleLogLevel.Log, ConsoleFormatter.Inspect(item));
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
    [JsFunction(Name = "table", Length = 0)]
    private JsValue Table(JsValue thisObject, JsValue tabularData, JsValue properties)
    {
        // The sequence conversion runs first and can raise a TypeError, exactly as WebIDL specifies, so a
        // non-iterable second argument fails before anything is logged.
        var columns = ReadProperties(properties);

        if (ConsoleFormatter.TryFormatTable(tabularData, columns, out var table))
        {
            Emit(ConsoleLogLevel.Log, table);
            return Undefined;
        }

        Emit(ConsoleLogLevel.Log, ConsoleFormatter.Format(new[] { tabularData }));
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
    [JsFunction(Name = "timeStamp", Length = 0)]
    private JsValue TimeStamp(JsValue thisObject, JsValue label)
    {
        Emit(ConsoleLogLevel.Log, Label(label));
        return Undefined;
    }

    /// <summary>
    /// https://console.spec.whatwg.org/#group
    /// </summary>
    [JsFunction(Name = "group", Length = 0)]
    private JsValue Group(JsValue thisObject, [Rest] ReadOnlySpan<JsValue> data)
    {
        return BeginGroup(data);
    }

    /// <summary>
    /// https://console.spec.whatwg.org/#groupcollapsed
    /// <para>
    /// A string sink cannot collapse anything, so this behaves exactly like <c>group</c> — which is what the
    /// specification says a non-interactive printer should do.
    /// </para>
    /// </summary>
    [JsFunction(Name = "groupCollapsed", Length = 0)]
    private JsValue GroupCollapsed(JsValue thisObject, [Rest] ReadOnlySpan<JsValue> data)
    {
        return BeginGroup(data);
    }

    /// <summary>
    /// https://console.spec.whatwg.org/#groupend
    /// </summary>
    [JsFunction(Name = "groupEnd", Length = 0)]
    private JsValue GroupEnd(JsValue thisObject)
    {
        if (_groupDepth > 0)
        {
            _groupDepth--;
        }

        return Undefined;
    }

    /// <summary>
    /// https://console.spec.whatwg.org/#count
    /// </summary>
    [JsFunction(Name = "count", Length = 0)]
    private JsValue Count(JsValue thisObject, JsValue label)
    {
        var key = Label(label);
        _counts ??= new Dictionary<string, int>(StringComparer.Ordinal);
        _counts.TryGetValue(key, out var count);
        count++;
        _counts[key] = count;

        Emit(ConsoleLogLevel.Log, key + ": " + count.ToString(CultureInfo.InvariantCulture));
        return Undefined;
    }

    /// <summary>
    /// https://console.spec.whatwg.org/#countreset
    /// </summary>
    [JsFunction(Name = "countReset", Length = 0)]
    private JsValue CountReset(JsValue thisObject, JsValue label)
    {
        var key = Label(label);
        if (_counts is not null && _counts.ContainsKey(key))
        {
            _counts[key] = 0;
        }
        else
        {
            Emit(ConsoleLogLevel.Warn, "Count for '" + key + "' does not exist");
        }

        return Undefined;
    }

    /// <summary>
    /// https://console.spec.whatwg.org/#time
    /// </summary>
    [JsFunction(Name = "time", Length = 0)]
    private JsValue Time(JsValue thisObject, JsValue label)
    {
        var key = Label(label);
        _timers ??= new Dictionary<string, long>(StringComparer.Ordinal);
        if (_timers.ContainsKey(key))
        {
            Emit(ConsoleLogLevel.Warn, "Timer '" + key + "' already exists");
            return Undefined;
        }

        _timers[key] = Stopwatch.GetTimestamp();
        return Undefined;
    }

    /// <summary>
    /// https://console.spec.whatwg.org/#timelog
    /// </summary>
    [JsFunction(Name = "timeLog", Length = 0)]
    private JsValue TimeLog(JsValue thisObject, JsValue label, [Rest] ReadOnlySpan<JsValue> data)
    {
        var key = Label(label);
        if (_timers is null || !_timers.TryGetValue(key, out var start))
        {
            Emit(ConsoleLogLevel.Warn, "Timer '" + key + "' does not exist");
            return Undefined;
        }

        var concat = key + ": " + FormatDuration(start);
        if (data.Length == 0)
        {
            Emit(ConsoleLogLevel.Log, concat);
            return Undefined;
        }

        var prefixed = new JsValue[data.Length + 1];
        prefixed[0] = JsString.Create(concat);
        data.CopyTo(prefixed.AsSpan(1));
        Emit(ConsoleLogLevel.Log, ConsoleFormatter.Format(prefixed));
        return Undefined;
    }

    /// <summary>
    /// https://console.spec.whatwg.org/#timeend
    /// </summary>
    [JsFunction(Name = "timeEnd", Length = 0)]
    private JsValue TimeEnd(JsValue thisObject, JsValue label)
    {
        var key = Label(label);
        if (_timers is null || !_timers.Remove(key, out var start))
        {
            Emit(ConsoleLogLevel.Warn, "Timer '" + key + "' does not exist");
            return Undefined;
        }

        Emit(ConsoleLogLevel.Log, key + ": " + FormatDuration(start));
        return Undefined;
    }

    private JsValue BeginGroup(ReadOnlySpan<JsValue> data)
    {
        // The label is printed at the current indentation; only what follows it is indented further.
        if (data.Length > 0)
        {
            Emit(ConsoleLogLevel.Log, ConsoleFormatter.Format(data));
        }

        _groupDepth++;
        return Undefined;
    }

    /// <summary>
    /// https://console.spec.whatwg.org/#logger — including step 1, which is why a console method called with
    /// no arguments emits nothing at all rather than an empty line.
    /// </summary>
    private void Logger(ConsoleLogLevel level, ReadOnlySpan<JsValue> data)
    {
        if (data.Length == 0)
        {
            return;
        }

        Emit(level, ConsoleFormatter.Format(data));
    }

    /// <summary>
    /// The one place a record reaches the host: applies the group indentation and makes exactly one
    /// <see cref="ConsoleSink.Write(ConsoleLogLevel, string)"/> call. The sink is read afresh every time, so
    /// a host may swap it between evaluations.
    /// </summary>
    private void Emit(ConsoleLogLevel level, string message)
    {
        var sink = _engine.Options.WebApi.Console.Sink ?? ConsoleSink.Null;
        sink.Write(level, Indent(message));
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
