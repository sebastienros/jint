namespace Jint.Runtime.Debugger
{
    public sealed class BreakPoint
    {
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

        public int Line { get; }
        public int Char { get; }
        public string Condition { get; }
    }
}
