using System.Collections.Generic;

namespace Jint.Parser.Ast
{
    public class BlockStatement : Statement
    {
        public IEnumerable<Statement> Body;
    }
}