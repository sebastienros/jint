using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Jint.Parser
{
    public class Script
    {
        public string Source { get; }
        public string Code { get; }

        public Script(string source, string code)
        {
            Source = source;
            Code = code;
        }
    }
}
