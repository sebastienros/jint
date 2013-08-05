namespace Jint.Parser.Ast
{
    public class MemberExpression : Expression
    {
        public Expression Object;
        public Expression Property;

        // true if an indexer is used and the property to be evaluated
        public bool Computed;
    }
}