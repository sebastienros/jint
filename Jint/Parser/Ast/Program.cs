using System.Collections.Generic;

namespace Jint.Parser.Ast
{
    public class Program : Statement, IVariableScope, IFunctionScope
    {
        public Program()
        {
            VariableDeclarations = new List<VariableDeclaration>();
        }
        public ICollection<Statement> Body;

        public List<Comment> Comments;
        public List<Token> Tokens;
        public List<ParserException> Errors;
        public bool Strict;

        public IList<VariableDeclaration> VariableDeclarations { get; set; }
        public IList<FunctionDeclaration> FunctionDeclarations { get; set; }
    }
}