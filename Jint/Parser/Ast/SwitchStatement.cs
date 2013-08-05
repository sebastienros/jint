using System.Collections.Generic;

namespace Jint.Parser.Ast
{
    public class SwitchStatement : Statement
    {
        public SyntaxNode Discriminant;
        public IEnumerable<SwitchCase> Cases;
    }
}