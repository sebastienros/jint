#nullable enable

using System.Reflection;
using Jint;
using Jint.Native;
using Jint.Native.Json;
using Jint.Runtime;

// ReSharper disable LocalizableElement

#pragma warning disable IL2026
#pragma warning disable IL2111

// Parse command line arguments
string? inputFile = null;
int? timeoutSeconds = null;

for (int i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "-f" or "--file":
            if (i + 1 < args.Length)
            {
                inputFile = args[++i];
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
        case "-h" or "--help":
            PrintHelp();
            return 0;
        default:
            // For backwards compatibility, treat first positional arg as filename
            if (!args[i].StartsWith("-") && inputFile == null)
            {
                inputFile = args[i];
            }
            break;
    }
}

var engine = new Engine(cfg =>
{
    cfg.AllowClr();
    if (timeoutSeconds.HasValue)
    {
        cfg.TimeoutInterval(TimeSpan.FromSeconds(timeoutSeconds.Value));
    }
});

engine
    .SetValue("print", new Action<object>(Console.WriteLine))
    .SetValue("console", new JsConsole())
    .SetValue("load", new Func<string, object>(
        path => engine.Evaluate(File.ReadAllText(path)))
    );

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
        var script = File.ReadAllText(inputFile);
        var result = engine.Evaluate(script, inputFile);
        if (!result.IsUndefined())
        {
            Console.WriteLine(result);
        }
        return 0;
    }
    catch (JavaScriptException je)
    {
        Console.Error.WriteLine($"Error: {je.Message}");
        Console.Error.WriteLine(je.JavaScriptStackTrace);
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
        var result = engine.Evaluate(script, "stdin");
        if (!result.IsUndefined())
        {
            Console.WriteLine(result);
        }
        return 0;
    }
    catch (JavaScriptException je)
    {
        Console.Error.WriteLine($"Error: {je.Message}");
        Console.Error.WriteLine(je.JavaScriptStackTrace);
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
    var input = Console.ReadLine();
    if (input is null or "exit" or ".exit")
    {
        return 0;
    }

    try
    {
        var result = engine.Evaluate(input, parsingOptions);
        JsValue str = result;
        if (!result.IsPrimitive() && result is not IJsPrimitive)
        {
            str = serializer.Serialize(result, JsValue.Undefined, "  ");
            if (str == JsValue.Undefined)
            {
                str = result;
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
        Console.WriteLine(je.ToString());
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
}

static void PrintHelp()
{
    Console.WriteLine("Jint REPL - JavaScript interpreter");
    Console.WriteLine();
    Console.WriteLine("Usage: jint [options] [file]");
    Console.WriteLine();
    Console.WriteLine("Options:");
    Console.WriteLine("  -f, --file <path>     Execute JavaScript file");
    Console.WriteLine("  -t, --timeout <secs>  Set execution timeout in seconds");
    Console.WriteLine("  -h, --help            Show this help message");
    Console.WriteLine();
    Console.WriteLine("Examples:");
    Console.WriteLine("  jint                          Start interactive REPL");
    Console.WriteLine("  jint script.js                Execute script.js");
    Console.WriteLine("  jint -f script.js -t 10       Execute with 10 second timeout");
    Console.WriteLine("  echo \"1+1\" | jint             Execute from stdin");
    Console.WriteLine("  echo \"1+1\" | jint -t 5        Execute from stdin with timeout");
}

file sealed class JsConsole
{
    public void Log(object value)
    {
        Console.WriteLine(value?.ToString() ?? "null");
    }

    public void Error(object value)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine(value?.ToString() ?? "null");
        Console.ResetColor();
    }
}
