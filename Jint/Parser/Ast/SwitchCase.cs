using System.Collections.Generic;

namespace Jint.Parser.Ast
{
    public class SwitchCase : SyntaxNode
    {
        public Expression Test;
        public IList<Statement> Consequent;
    }
}