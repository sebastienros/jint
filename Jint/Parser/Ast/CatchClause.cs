namespace Jint.Parser.Ast
{
    public class CatchClause : Statement
    {
        public SyntaxNode Param;
        public BlockStatement Body;
    }
}