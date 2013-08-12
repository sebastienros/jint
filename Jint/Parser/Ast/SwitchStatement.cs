using System.Collections.Generic;

namespace Jint.Parser.Ast
{
    public class SwitchStatement : Statement
    {
        public Expression Discriminant;
        public IEnumerable<SwitchCase> Cases;
    }
}