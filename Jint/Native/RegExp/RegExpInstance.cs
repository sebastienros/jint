using System.Text.RegularExpressions;
using Jint.Native.Object;

namespace Jint.Native.RegExp
{
    public class RegExpInstance : ObjectInstance
    {
        public RegExpInstance(Engine engine)
            : base(engine)
        {
        }

        public override string Class
        {
            get
            {
                return "RegExp";
            }
        }

        public Regex Value { get; set; }
        //public string Pattern { get; set; }
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
