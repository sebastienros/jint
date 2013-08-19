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

                var result = engine.GetValue(engine.Execute(input));
                var str = engine.JSON.Stringify(engine.JSON, Arguments.From(result, Undefined.Instance, "\t"));
                Console.ForegroundColor = ConsoleColor.Magenta;
                Console.WriteLine("=> {0}", str);
            }
        }
    }
}
