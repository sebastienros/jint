namespace Jint.Runtime.Debugger
{
    public sealed class BreakPoint
    {
        public BreakPoint(int line, int column)
        {
            Line = line;
            Column = column;
        }

        public BreakPoint(int line, int column, string condition)
            : this(line, column)
        {
            Condition = condition;
        }

        public BreakPoint(string source, int line, int column) : this(line, column)
        {
            Source = source;
        }

        public BreakPoint(string source, int line, int column, string condition) : this(source, line, column)
        {
            Condition = condition;
        }

        public string Source { get; }
        public int Line { get; }
        public int Column { get; }
        public string Condition { get; }
    }
}
