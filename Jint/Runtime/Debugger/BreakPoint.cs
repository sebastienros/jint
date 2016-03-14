using Jint.Parser;

namespace Jint.Runtime.Debugger
{
    public class BreakPoint
    {
        public Script Source { get; set; }
        public int Line { get; set; }
        public int Char { get; set; }
        public string Condition { get; set; }

        public BreakPoint(Script source, int line, int character)
        {
            Source = source;
            Line = line;
            Char = character;
        }

        public BreakPoint(Script source, int line, int character, string condition)
            : this(source, line, character)
        {
            Condition = condition;
        }
    }
}
