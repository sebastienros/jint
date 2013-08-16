using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
                Console.ForegroundColor = ConsoleColor.Magenta;
                Console.WriteLine("=> {0}", result);
            }
        }
    }
}
