using System.Collections.Generic;

namespace Jint.Parser.Ast
{
    public class VariableDeclaration : Statement
    {
        public IEnumerable<VariableDeclarator> Declarations;
        public string Kind;
    }
}