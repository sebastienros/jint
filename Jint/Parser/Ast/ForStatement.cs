namespace Jint.Parser.Ast
{
    public class ForStatement : Statement
    {
        // can be a Statement (var i) or an Expression (i=0)
        public SyntaxNode Init;
        public Expression Test;
        public Expression Update;
        public Statement Body;
    }
}