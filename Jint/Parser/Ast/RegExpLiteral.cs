namespace Jint.Parser.Ast
{
    public class RegExpLiteral : Expression, IPropertyKeyExpression
    {
        public object Value;
        public string Raw;
        public string Flags;
        
        public string GetKey()
        {
            return Value.ToString();
        }
    }
}