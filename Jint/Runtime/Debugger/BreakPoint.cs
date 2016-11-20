using Jint.Parser.Ast;

namespace Jint.Runtime.Debugger
{
    public class BreakPoint
    {
        public int Line { get; }
        public int Char { get; }
        public string Condition { get; }
        public Statement Statement { get; }

        public BreakPoint(int line, int character, string condition=null)
        {
            Line = line;
            Char = character;
            Condition = condition;
        }

        public BreakPoint(Statement statement, string condition = null)
        {
            this.Statement = statement;
            this.Line = statement.Location.Start.Line;
            this.Char = statement.Location.Start.Column;
            this.Condition = condition;
        }
    }
}
