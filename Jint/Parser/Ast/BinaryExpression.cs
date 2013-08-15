namespace Jint.Parser.Ast
{
    public class BinaryExpression : Expression
    {
        public string Operator;
        public Expression Left;
        public Expression Right;
    }
}