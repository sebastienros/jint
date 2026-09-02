using Jint.Diagnostics;
using Jint.Native;
using Jint.Runtime;
using Jint.WebApi;

namespace Jint.Browser.Runtime;

/// <summary>
/// What a page keeps of what its scripts reported and printed, rendered on the page loop and bounded.
/// </summary>
/// <remarks>
/// <para>
/// Both sinks are called on the page loop, holding values that belong to the page's engine, so both render
/// to text before returning. What a caller of <see cref="Page.Errors"/> or <see cref="Page.ConsoleMessages"/>
/// receives has no reference into the engine at all, which is what makes reading them from another thread
/// safe without a mailbox round trip.
/// </para>
/// <para>
/// Each recording is a bounded ring: a page in a loop can print without limit, and a host that asked for a
/// page is not asking for its memory.
/// </para>
/// </remarks>
internal sealed class PageRecorder
{
    private readonly object _gate = new();
    private readonly Queue<PageError> _errors = new();
    private readonly Queue<string> _console = new();
    private readonly int _max;

    internal PageRecorder(int max)
    {
        _max = max;
    }

    /// <summary>A snapshot of the errors recorded so far, oldest first.</summary>
    internal IReadOnlyList<PageError> Errors
    {
        get
        {
            lock (_gate)
            {
                return _errors.ToArray();
            }
        }
    }

    /// <summary>A snapshot of the console messages recorded so far, oldest first.</summary>
    internal IReadOnlyList<string> ConsoleMessages
    {
        get
        {
            lock (_gate)
            {
                return _console.ToArray();
            }
        }
    }

    internal void Add(PageError error)
    {
        lock (_gate)
        {
            _errors.Enqueue(error);
            while (_errors.Count > _max)
            {
                _errors.Dequeue();
            }
        }
    }

    internal void AddConsole(string message)
    {
        lock (_gate)
        {
            _console.Enqueue(message);
            while (_console.Count > _max)
            {
                _console.Dequeue();
            }
        }
    }

    /// <summary>The diagnostics sink a page installs, which is also what keeps a bad callback from stopping it.</summary>
    internal sealed class Diagnostics : DiagnosticsSink
    {
        private readonly PageRecorder _recorder;

        internal Diagnostics(PageRecorder recorder)
        {
            _recorder = recorder;
        }

        public override void Report(DiagnosticEvent report)
        {
            var kind = report.Kind switch
            {
                DiagnosticEventKind.UncaughtCallbackError => PageErrorKind.UncaughtCallbackError,
                DiagnosticEventKind.UnhandledPromiseRejection => PageErrorKind.UnhandledPromiseRejection,
                DiagnosticEventKind.WorkerError => PageErrorKind.WorkerError,
                _ => PageErrorKind.ReportedError,
            };

            _recorder.Add(new PageError(kind, Describe(report.Value, report.Exception), report.CallbackSource?.ToString()));
        }

        /// <summary>
        /// Renders the reported value without running any of the page's code.
        /// </summary>
        /// <remarks>
        /// Through the value inspector rather than through <c>ToString</c> or the exception's message,
        /// because a rejected promise's reason is whatever the page chose and a script can make <c>name</c>,
        /// <c>message</c> and <c>toString</c> accessors that run when read. The inspector reads slots and
        /// descriptors and never calls a function, so recording an error cannot itself run script.
        /// </remarks>
        internal static string Describe(JsValue value, JavaScriptException? exception)
        {
#pragma warning disable JINT0002 // The inspector's capability is settled; only its shape is provisional.
            if (!value.IsUndefined())
            {
                var description = ValueInspector.Describe(value, DescribeOptions).Description;
                if (description.Length > 0)
                {
                    return description;
                }
            }
#pragma warning restore JINT0002

            return exception?.Message ?? "undefined";
        }

#pragma warning disable JINT0002
        private static readonly ValueInspectorOptions DescribeOptions = new() { MaxEntries = 0, MaxStringLength = 400 };
#pragma warning restore JINT0002
    }

    /// <summary>The console sink a page installs, taking the record overload so that group depth survives.</summary>
    internal sealed class Console : ConsoleSink
    {
        private readonly PageRecorder _recorder;

        internal Console(PageRecorder recorder)
        {
            _recorder = recorder;
        }

        public override void Write(ConsoleLogLevel level, string message) => _recorder.AddConsole(message);

        public override void Write(in ConsoleRecord record)
        {
            // Null exactly when the call printed nothing — console.groupEnd(), console.time() and their kind.
            // A browser's console shows no line for those either.
            if (record.Message is { } message)
            {
                _recorder.AddConsole(message);
            }
        }
    }
}
