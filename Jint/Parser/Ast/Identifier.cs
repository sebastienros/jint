namespace Jint.Parser.Ast
{
    public class Identifier : Expression, IPropertyKeyExpression
    {
        public string Name;
        
        public string GetKey()
        {
            return Name;
        }
    }
}