namespace Jint.Parser.Ast
{
    public class LabeledStatement : Statement
    {
        public Identifier Label;
        public Statement Body;
    }
}