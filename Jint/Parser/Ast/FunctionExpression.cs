using System.Collections.Generic;

namespace Jint.Parser.Ast
{
    public class FunctionExpression : Expression
    {
        public Identifier Id;
        public IEnumerable<Identifier> Parameters;
        public Statement Body;

        #region ECMA6
        public IEnumerable<Expression> Defaults;
        public SyntaxNode Rest;
        public bool Generator;
        public bool Expression;
        #endregion
    }
}