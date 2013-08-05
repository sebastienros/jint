using System.Collections.Generic;

namespace Jint.Parser.Ast
{
    public class TryStatement : Statement
    {
        public Statement Block;
        public IEnumerable<Statement> GuardedHandlers;
        public IEnumerable<Statement> Handlers;
        public Statement Finalizer;
    }
}