namespace Jint.Parser.Ast
{
    public class UpdateExpression : Expression
    {
        public string Operator;
        public Expression Argument;
        public bool Prefix;
    }
}