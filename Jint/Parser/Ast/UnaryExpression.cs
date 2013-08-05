namespace Jint.Parser.Ast
{
    public class UnaryExpression : Expression
    {
        public string Operator;
        public SyntaxNode Argument;
        public bool Prefix;
    }
}