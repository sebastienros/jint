using System;
using Jint.Parser.Ast;

namespace Jint.Runtime.Debugger
{
    public class SourceInformation : EventArgs
    {
        public string Name { get; }
        public string Source { get; }
        public Statement[] Statements { get; }

        public SourceInformation(string name, string source, Statement[] statements)
        {
            Name = name?? $"<Unnamed> ({source.GetHashCode():X8})";
            Source = source;
            Statements = statements;
        }
    }
}