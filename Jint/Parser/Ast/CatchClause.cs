namespace Jint.Parser.Ast
{
    public class CatchClause : Statement
    {
        public Identifier Param;
        public BlockStatement Body;
    }
}