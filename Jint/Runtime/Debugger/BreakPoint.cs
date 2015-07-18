namespace Jint.Runtime.Debugger
{
    public class BreakPoint
    {
        public int Line { get; set; }
        public int Char { get; set; }
        public string Condition { get; set; }

        public BreakPoint(int line, int character)
        {
            Line = line;
            Char = character;
        }

        public BreakPoint(int line, int character, string condition)
            : this(line, character)
        {
            Condition = condition;
        }
    }
}
