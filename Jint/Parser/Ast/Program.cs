using System.Collections.Generic;

namespace Jint.Parser.Ast
{
    public class Program : Statement
    {
        public ICollection<Statement> Body;

        public List<Comment> Comments;
        public List<Token> Tokens;
        public List<ParserError> Errors;

    }
}