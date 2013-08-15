using System;

namespace Jint.Parser.Ast
{
    [Flags]
    public enum PropertyKind
    {
        Data = 1,
        Get = 2,
        Set = 4
    };


    public class Property : Expression
    {
        public PropertyKind Kind;
        public IPropertyKeyExpression Key;
        public Expression Value;
    }
}