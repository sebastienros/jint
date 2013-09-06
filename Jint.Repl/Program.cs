using System;
using Jint.Native;
using Jint.Runtime;

namespace Jint.Repl
{
    class Program
    {
        static void Main(string[] args)
        {
            var engine = new Engine();
 
            while (true)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.Write(" > ");
                var input = Console.ReadLine();
                if (input == "exit")
                {
                    return;
                }

                try
                {
                    var result = engine.GetValue(engine.Execute(input));
                    var str = engine.Json.Stringify(engine.Json, Arguments.From(result, Undefined.Instance, "  "));
                    Console.ForegroundColor = ConsoleColor.Magenta;
                    Console.WriteLine("=> {0}", str);
                }
                catch (JavaScriptException je)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("Error => {0}", engine.Json.Stringify(engine.Json, Arguments.From(je.Error, Undefined.Instance, "  ")));
                }
                
            }
        }
    }
}
