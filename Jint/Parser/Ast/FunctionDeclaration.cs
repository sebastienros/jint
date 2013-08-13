using System.Collections.Generic;

namespace Jint.Parser.Ast
{
    public class FunctionDeclaration : Statement, IVariableScope
    {
        public FunctionDeclaration()
        {
            VariableDeclarations = new List<VariableDeclaration>();
        }

        public Identifier Id;
        public IEnumerable<Identifier> Parameters;
        public Statement Body;
        public bool Strict;

        public IList<VariableDeclaration> VariableDeclarations { get; set; }

        #region ECMA6
        
        public IEnumerable<Expression> Defaults;
        public SyntaxNode Rest;
        public bool Generator;
        public bool Expression;
        
        #endregion
    }
}