namespace Jint.Parser.Ast
{
    public class Literal : Expression, IPropertyKeyExpression
    {
        public object Value;
        public string Raw;

        public string GetKey()
        {
            return Value.ToString();
        }
    }
}