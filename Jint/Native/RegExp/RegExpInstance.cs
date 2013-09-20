using System;
using System.Text.RegularExpressions;
using Jint.Native.Object;
using Jint.Runtime;

namespace Jint.Native.RegExp
{
    public class RegExpInstance : ObjectInstance
    {
        private readonly Engine _engine;

        public RegExpInstance(Engine engine)
            : base(engine)
        {
            _engine = engine;
        }

        public override string Class
        {
            get
            {
                return "RegExp";
            }
        }

        public Regex Value { get; set; }
        public string Pattern { get; set; }
        public string Source { get; set; }
        public string Flags { get; set; }
        
        public bool Global { get; set; }
        public bool IgnoreCase { get; set; }
        public bool Multiline { get; set; }

        public Match Match(string input, double start)
        {
            return Value.Match(input, (int) start);
        }
    }
}
