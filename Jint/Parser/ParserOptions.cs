using System;

namespace Jint.Parser
{
    [Flags]
    public enum ParserOptionFlags
    {
        SuppressIllegalReturn = 0x1
    }

    public class ParserOptions
    {
        public string Source { get; set; }
        public bool Tokens { get; set; }
        public bool Comment { get; set; }
        public bool Tolerant { get; set; }

        public ParserOptionFlags Flags { get; set; }
    }
}
