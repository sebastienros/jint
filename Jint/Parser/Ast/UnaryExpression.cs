namespace Jint.Parser.Ast
{
    public class UnaryExpression : Expression
    {
        public string Operator;
        public Expression Argument;
        public bool Prefix;
    }
}