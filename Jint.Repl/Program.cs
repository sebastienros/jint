using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using Esprima;
using Jint.Native;
using Jint.Runtime;

namespace Jint.Repl
{
    internal static class Program
    {
        private static void Main(string[] args)
        {
            var engine = new Engine(cfg => cfg
                .AllowClr()
            );

            engine
                .SetValue("print", new Action<object>(Console.WriteLine))
                .SetValue("load", new Func<string, object>(
                    path => engine.Execute(File.ReadAllText(path)).GetCompletionValue())
                );

            var filename = args.Length > 0 ? args[0] : "";
            if (!string.IsNullOrEmpty(filename))
            {
                if (!File.Exists(filename))
                {
                    Console.WriteLine("Could not find file: {0}", filename);
                }

                var script = File.ReadAllText(filename);
                var result = engine.GetValue(engine.Execute(script).GetCompletionValue());
                return;
            }

            var assembly = Assembly.GetExecutingAssembly();
            var fvi = FileVersionInfo.GetVersionInfo(assembly.Location);
            var version = fvi.FileVersion;

            Console.WriteLine("Welcome to Jint ({0})", version);
            Console.WriteLine("Type 'exit' to leave, " +
                              "'print()' to write on the console, " +
                              "'load()' to load scripts.");
            Console.WriteLine();

            var defaultColor = Console.ForegroundColor;
            var parserOptions = new ParserOptions("repl")
            {
                Loc = true,
                Range = true,
                Tolerant = true,
                AdaptRegexp = true
            };

            while (true)
            {
                Console.ForegroundColor = defaultColor;
                Console.Write("jint> ");
                var input = Console.ReadLine();
                if (input == "exit")
                {
                    return;
                }

                try
                {
                    var result = engine.GetValue(engine.Execute(input, parserOptions).GetCompletionValue());
                    if (result.Type != Types.None && result.Type != Types.Null && result.Type != Types.Undefined)
                    {
                        var str = TypeConverter.ToString(engine.Json.Stringify(engine.Json, Arguments.From(result, Undefined.Instance, "  ")));
                        Console.WriteLine("=> {0}", str);
                    }
                }
                catch (JavaScriptException je)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine(je.ToString());
                }
                catch (Exception e)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine(e.Message);
                }
            }
        }
    }
}