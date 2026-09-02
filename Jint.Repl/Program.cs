#nullable enable

using System.Globalization;
using System.Reflection;
using Jint;
using Jint.DevTools;
using Jint.Native;
using Jint.Native.Json;
using Jint.Runtime;
using Jint.Runtime.Modules;

// ReSharper disable LocalizableElement

#pragma warning disable IL2026
#pragma warning disable IL2111

// Parse command line arguments
string? inputFile = null;
int? timeoutSeconds = null;
bool runAsModule = false;
bool enableFetch = false;
bool inspect = false;
bool inspectBreak = false;
string inspectHost = "127.0.0.1";
int inspectPort = 9229;

for (int i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "-f" or "--file":
            if (i + 1 < args.Length)
            {
                inputFile = args[++i];
                if (inputFile.EndsWith(".mjs", StringComparison.OrdinalIgnoreCase))
                {
                    runAsModule = true;
                }
            }
            else
            {
                Console.Error.WriteLine("Error: -f requires a file path");
                return 1;
            }
            break;
        case "-t" or "--timeout":
            if (i + 1 < args.Length && int.TryParse(args[++i], out var t))
            {
                timeoutSeconds = t;
            }
            else
            {
                Console.Error.WriteLine("Error: -t requires a timeout value in seconds");
                return 1;
            }
            break;
        case "-m" or "--module":
            runAsModule = true;
            break;
        case "--fetch":
            enableFetch = true;
            break;
        case "-h" or "--help":
            PrintHelp();
            return 0;
        default:
            // --inspect and --inspect-brk carry their endpoint in the same argument, the way Node spells it,
            // so they are matched by prefix rather than by a case label.
            if (args[i] is "--inspect" or "--inspect-brk" || args[i].StartsWith("--inspect=", StringComparison.Ordinal) || args[i].StartsWith("--inspect-brk=", StringComparison.Ordinal))
            {
                inspect = true;
                inspectBreak = args[i].StartsWith("--inspect-brk", StringComparison.Ordinal);

                var equals = args[i].IndexOf('=');
                if (equals >= 0 && !TryReadEndpoint(args[i].Substring(equals + 1), ref inspectHost, ref inspectPort))
                {
                    Console.Error.WriteLine($"Error: {args[i]} is not [host:]port");
                    return 1;
                }

                break;
            }

            // For backwards compatibility, treat first positional arg as filename
            if (!args[i].StartsWith("-") && inputFile == null)
            {
                inputFile = args[i];
                if (inputFile.EndsWith(".mjs", StringComparison.OrdinalIgnoreCase))
                {
                    runAsModule = true;
                }
            }
            break;
    }
}

var engine = new Engine(cfg =>
{
    cfg.AllowClr();
    cfg.UseConsole(Console.Out);

    // The whole non-network web-API surface, so a script pasted from the web behaves. Note that the REPL
    // pumps the event loop only as part of evaluating each input, so a timer fires on the evaluation it is
    // already due for or on a later one — a `setTimeout(f, 1000)` typed at the prompt runs f on the next line
    // you enter, and one scheduled by a `-f script.js` run that outlives the script never runs at all. That
    // is the engine's contract, not a REPL limitation: nothing anywhere in Jint pumps an engine on its own.
    cfg.UseWebApis(WebApiFeatures.Default);

    // Outbound network access is a named grant even here. `WebApiFeatures.Default` never includes fetch, and
    // a demo tool is no reason to hand a pasted script the host process's network position, so it takes
    // `--fetch` on the command line and nothing less.
    if (enableFetch)
    {
        cfg.UseFetch();
    }

    if (timeoutSeconds.HasValue)
    {
        cfg.LimitExecutionTime(TimeSpan.FromSeconds(timeoutSeconds.Value));
    }

    // An attachable engine is built for it: debugging, retained source text, profiling and coverage are all
    // construction-time, so --inspect has to be known here rather than when a client connects. Last of the
    // web-API calls, because the console sink it wraps is the one UseConsole installed above.
    if (inspect)
    {
        cfg.UseDevTools(devTools => devTools.Coverage = true);
    }

    // Even if the script is not a module, modules still need to be enabled
    // for dynamic import to work.
    var basePath = inputFile != null
        ? Path.GetDirectoryName(Path.GetFullPath(inputFile))!
        : Directory.GetCurrentDirectory();
    cfg.UseModules(basePath, restrictToBasePath: false);
});

engine
    .SetValue("print", new Action<object>(Console.WriteLine))
    .SetValue("load", new Func<string, object>(
        path => engine.Evaluate(File.ReadAllText(path)))
    );

var test262 = Test262Object.Install(engine);

var agentManager = new Test262AgentManager();
agentManager.InstallAgent(engine, test262);

DevToolsServer? inspector = null;
EngineTarget? inspected = null;

if (inspect)
{
    try
    {
        inspector = new DevToolsServer(new DevToolsServerOptions { Host = inspectHost, Port = inspectPort, Product = $"Jint/{Assembly.GetExecutingAssembly().GetName().Version}" });
        inspector.Start();
    }
    catch (Exception e)
    {
        // A port already in use and an address that is not one are the two ways this fails, and both are
        // the command line's fault rather than the script's. Node says so and stops; so does this.
        Console.Error.WriteLine($"Error: cannot listen on {inspectHost}:{inspectPort}: {e.Message}");
        return 1;
    }

    inspected = new EngineTarget(engine, new EngineTargetOptions
    {
        Title = inputFile is null ? "Jint REPL" : Path.GetFileName(inputFile),
        // The absolute path, which the target publishes as the same file:// URL it publishes the script's
        // own source name as — so /json/list and Debugger.scriptParsed name one location, not two.
        Url = inputFile is null ? "jint://repl" : Path.GetFullPath(inputFile),

        // The REPL owns its loop: it evaluates on this thread, so this thread is also the one that answers
        // the protocol. A LibraryOwned target would be a second thread in the same engine.
        ThreadMode = ThreadMode.HostOwned,
        WaitForDebuggerOnStart = inspectBreak,
    });

    inspector.AddTarget(inspected);
    PrintInspectorBanner(inspectHost, inspector.BoundPort, inspected.TargetId, inspectBreak);

    if (inspectBreak)
    {
        // Node's --inspect-brk: nothing the host queued runs until a client has attached and sent
        // Runtime.runIfWaitingForDebugger. The wait pumps rather than blocks, because that very command is
        // answered on this thread.
        inspected.WaitForDebugger(Timeout.InfiniteTimeSpan);
    }
}

// Execute file if provided via -f
if (!string.IsNullOrEmpty(inputFile))
{
    if (!File.Exists(inputFile))
    {
        Console.Error.WriteLine($"Error: Could not find file: {inputFile}");
        return 1;
    }

    try
    {
        if (runAsModule)
        {
            var absolutePath = Path.GetFullPath(inputFile);
            engine.Modules.Import(new Uri(absolutePath).AbsoluteUri);
        }
        else
        {
            var script = File.ReadAllText(inputFile);
            var result = engine.Evaluate(script, inputFile);
            if (!result.IsUndefined())
            {
                Console.WriteLine(result);
            }
        }
        return KeepInspecting(inspected);
    }
    catch (JavaScriptException je)
    {
        // An attached client hears the throw the way it hears one from a timer: as Runtime.exceptionThrown,
        // so the Console panel shows it rather than only this terminal.
        inspected?.ReportUncaughtException(je);
        Console.Error.WriteLine(FormatJavaScriptException(je));
        Console.Error.WriteLine(je.JavaScriptStackTrace);
        return KeepInspecting(inspected, 1);
    }
    catch (ModuleResolutionException mre)
    {
        Console.Error.WriteLine($"Error: {mre.Message}");
        return 1;
    }
    catch (TimeoutException)
    {
        Console.Error.WriteLine("Error: Script execution timed out");
        return 1;
    }
    catch (Exception e)
    {
        Console.Error.WriteLine($"Error: {e.Message}");
        return 1;
    }
}

// Check if input is being piped via STDIN
if (Console.IsInputRedirected)
{
    try
    {
        var script = Console.In.ReadToEnd();
        if (runAsModule)
        {
            engine.Modules.Add("<stdin>", script);
            engine.Modules.Import("<stdin>");
        }
        else
        {
            var result = engine.Evaluate(script, "stdin");
            if (!result.IsUndefined())
            {
                Console.WriteLine(result);
            }
        }
        return KeepInspecting(inspected);
    }
    catch (JavaScriptException je)
    {
        inspected?.ReportUncaughtException(je);
        Console.Error.WriteLine(FormatJavaScriptException(je));
        Console.Error.WriteLine(je.JavaScriptStackTrace);
        return KeepInspecting(inspected, 1);
    }
    catch (ModuleResolutionException mre)
    {
        Console.Error.WriteLine($"Error: {mre.Message}");
        return 1;
    }
    catch (TimeoutException)
    {
        Console.Error.WriteLine("Error: Script execution timed out");
        return 1;
    }
    catch (Exception e)
    {
        Console.Error.WriteLine($"Error: {e.Message}");
        return 1;
    }
}

// Interactive REPL mode
var assembly = Assembly.GetExecutingAssembly();
var version = assembly.GetName().Version?.ToString();

Console.WriteLine($"Welcome to Jint ({version})");
Console.WriteLine("Type 'exit' to leave, 'print()' to write on the console, 'load()' to load scripts.");
Console.WriteLine();

var defaultColor = Console.ForegroundColor;
var parsingOptions = new ScriptParsingOptions
{
    Tolerant = true,
};

var serializer = new JsonSerializer(engine);

while (true)
{
    Console.ForegroundColor = defaultColor;
    Console.Write("jint> ");
    var input = inspected is null ? Console.ReadLine() : ReadLineWhilePumping(inspected);
    if (input is null or "exit" or ".exit")
    {
        return 0;
    }

    try
    {
        var result = engine.Evaluate(input, parsingOptions: parsingOptions);
        JsValue str = result;
        if (!result.IsPrimitive() && result is not IJsPrimitive)
        {
            if (result is JsRegExp jsRegExp)
            {
                str = result.ToString();
            }
            else
            {
                str = serializer.Serialize(result, JsValue.Undefined, "  ");
                if (str == JsValue.Undefined)
                {
                    str = result;
                }
            }
        }
        else if (result.IsString())
        {
            str = serializer.Serialize(result, JsValue.Undefined, JsValue.Undefined);
        }
        Console.WriteLine(str);
    }
    catch (JavaScriptException je)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine(FormatJavaScriptException(je));
        Console.Error.WriteLine(je.JavaScriptStackTrace);
    }
    catch (TimeoutException)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine("Error: Script execution timed out");
    }
    catch (Exception e)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine(e.Message);
    }

    // Between inputs, which is what a HostOwned target means: the commands a client sent while the prompt
    // was waiting were already answered by the read above, and this runs whatever the evaluation queued.
    if (inspected is not null)
    {
        PumpInspector(inspected);
    }
}

/// <summary>Reads one line while the engine keeps answering an attached client.</summary>
static string? ReadLineWhilePumping(EngineTarget target)
{
    // The console read is the one thing that leaves this thread. It touches no engine state, and moving it
    // off is what makes a client's commands answered while the prompt waits rather than only after the next
    // line is typed.
    var line = Task.Run(Console.ReadLine);
    while (!line.IsCompleted)
    {
        PumpInspector(target);
        ((IAsyncResult) line).AsyncWaitHandle.WaitOne(25);
    }

    // A read that failed rather than returned is end of input, which is what the caller does with a null
    // anyway. Waiting on the task instead would throw the console's exception out of the REPL's loop.
    return line.IsCompletedSuccessfully ? line.Result : null;
}

/// <summary>Gives the engine one turn, and reports whatever the turn threw rather than dying of it.</summary>
static void PumpInspector(EngineTarget target)
{
    try
    {
        target.Pump();
    }
    catch (JavaScriptException je)
    {
        target.ReportUncaughtException(je);
        Console.Error.WriteLine(FormatJavaScriptException(je));
    }
    catch (Exception e)
    {
        Console.Error.WriteLine($"Error: {e.Message}");
    }
}

/// <summary>
/// Keeps a --inspect engine attachable after the script it was given has finished, so a client can still
/// read it, and so a timer the script scheduled still fires.
/// </summary>
static int KeepInspecting(EngineTarget? target, int exitCode = 0)
{
    if (target is null)
    {
        return exitCode;
    }

    Console.WriteLine("The script has finished; the engine stays attached. Press Ctrl+C to exit.");

    var stopping = 0;
    Console.CancelKeyPress += (_, e) =>
    {
        e.Cancel = true;
        Interlocked.Exchange(ref stopping, 1);
    };

    while (Volatile.Read(ref stopping) == 0)
    {
        PumpInspector(target);

        // The mailbox wakes this through Engine.Tasks.Post, so a command a client sends is answered on the
        // next turn rather than after the wait.
        target.Engine.Tasks.WaitForScheduledWork(TimeSpan.FromMilliseconds(50));
    }

    return exitCode;
}

/// <summary>Reads a <c>--inspect=</c> endpoint, which is a port or a host and a port.</summary>
static bool TryReadEndpoint(string endpoint, ref string host, ref int port)
{
    var colon = endpoint.LastIndexOf(':');
    if (colon >= 0)
    {
        host = endpoint.Substring(0, colon);
        endpoint = endpoint.Substring(colon + 1);
    }

    return host.Length > 0 && int.TryParse(endpoint, NumberStyles.None, CultureInfo.InvariantCulture, out port) && port <= 65535;
}

/// <summary>Says where a client attaches, in the three forms one is reached from.</summary>
static void PrintInspectorBanner(string host, int port, string targetId, bool waiting)
{
    var endpoint = string.Create(CultureInfo.InvariantCulture, $"{host}:{port}/devtools/page/{targetId}");

    Console.WriteLine($"Debugger listening on ws://{endpoint}");
    Console.WriteLine($"  front end: devtools://devtools/bundled/js_app.html?experiments=true&v8only=true&ws={endpoint}");
    Console.WriteLine($"  or open chrome://inspect, click Configure..., add {host}:{port}, and the target appears under Remote Target");

    if (waiting)
    {
        Console.WriteLine("Waiting for the debugger to attach; nothing runs until a client sends Runtime.runIfWaitingForDebugger.");
    }

    Console.WriteLine();
}

static string FormatJavaScriptException(JavaScriptException je)
{
    if (!je.Error.IsObject())
    {
        return $"Uncaught exception: {je.Error}";
    }

    var obj = je.Error.AsObject();
    var name = obj.Get(new JsString("name"), je.Error);
    if (name.IsUndefined())
    {
        var ctor = obj.Get(new JsString("constructor"), je.Error);
        if (ctor.IsObject())
        {
            name = ctor.AsObject().Get(new JsString("name"), ctor);
        }
    }

    var errorName = name.IsUndefined() ? "Error" : name.ToString();
    var message = obj.Get(new JsString("message"), je.Error);
    return message.IsUndefined() || message.ToString().Length == 0
        ? $"Uncaught exception: {errorName}"
        : $"Uncaught exception: {errorName}: {message}";
}

static void PrintHelp()
{
    Console.WriteLine("Jint REPL - JavaScript interpreter");
    Console.WriteLine();
    Console.WriteLine("Usage: jint [options] [file]");
    Console.WriteLine();
    Console.WriteLine("Options:");
    Console.WriteLine("  -f, --file <path>     Execute JavaScript file");
    Console.WriteLine("  -m, --module          Run script as ES6 module");
    Console.WriteLine("  -t, --timeout <secs>  Set execution timeout in seconds");
    Console.WriteLine("      --fetch           Allow the script to make network requests");
    Console.WriteLine("      --inspect[=[host:]port]      Serve the Chrome DevTools protocol (default 127.0.0.1:9229)");
    Console.WriteLine("      --inspect-brk[=[host:]port]  The same, but run nothing until a client attaches");
    Console.WriteLine("  -h, --help            Show this help message");
    Console.WriteLine();
    Console.WriteLine("Inspecting:");
    Console.WriteLine("  --inspect prints the endpoint, the devtools:// front-end address and what to type into");
    Console.WriteLine("  chrome://inspect. The engine is the one this process runs, so it is pumped between inputs:");
    Console.WriteLine("  a command sent while the prompt is waiting is answered there and then, and a script run with");
    Console.WriteLine("  -f keeps the engine attached after it finishes so a timer still fires and the client can still");
    Console.WriteLine("  read it - Ctrl+C exits. Port 0 asks the operating system for one and the banner says which.");
    Console.WriteLine("  It also turns on coverage counting, so the front end's Coverage panel answers.");
    Console.WriteLine("  -t still bounds execution, and it keeps counting while the debugger has the engine paused,");
    Console.WriteLine("  so a timeout short enough to matter and a breakpoint do not go together.");
    Console.WriteLine();
    Console.WriteLine("Web APIs:");
    Console.WriteLine("  console, timers, TextEncoder/TextDecoder, atob/btoa, structuredClone, crypto,");
    Console.WriteLine("  performance, Event/AbortController, URL, Blob/File/FormData, navigator, streams,");
    Console.WriteLine("  compression, scheduler, requestIdleCallback, messaging, reportError and the global");
    Console.WriteLine("  error events are all on - WebApiFeatures.Default, the same set UseWebApis() gives.");
    Console.WriteLine("  fetch is NOT: outbound network access stays a grant you name, even in a demo tool,");
    Console.WriteLine("  because it hands a pasted script this process's network position. Pass --fetch to");
    Console.WriteLine("  turn it on. localStorage and caches are off too - they outlive the evaluation.");
    Console.WriteLine();
    Console.WriteLine("Examples:");
    Console.WriteLine("  jint                          Start interactive REPL");
    Console.WriteLine("  jint script.js                Execute script.js");
    Console.WriteLine("  jint -m module.js             Execute module.js as ES6 module");
    Console.WriteLine("  jint -f script.js -t 10       Execute with 10 second timeout");
    Console.WriteLine("  jint -f script.js --fetch     Execute with network access enabled");
    Console.WriteLine("  jint --inspect                Start the REPL with DevTools on 127.0.0.1:9229");
    Console.WriteLine("  jint -f app.js --inspect-brk=0  Attach before app.js runs, on a port the banner names");
    Console.WriteLine("  echo \"1+1\" | jint             Execute from stdin");
    Console.WriteLine("  echo \"1+1\" | jint -t 5        Execute from stdin with timeout");
}
