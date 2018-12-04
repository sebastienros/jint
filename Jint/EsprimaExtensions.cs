using System;
using Esprima.Ast;
using Jint.Native.Symbol;
using Jint.Runtime;

namespace Jint
{
    internal static class EsprimaExtensions
    {
        public static string GetKey<T>(this T expression) where T : Expression
        {
            if (expression is Literal literal)
            {
                return literal.Value as string ?? Convert.ToString(literal.Value, provider: null);
            }

            if (expression is Identifier identifier)
            {
                return identifier.Name;
            }

            if (expression is StaticMemberExpression staticMemberExpression)
            {
                var obj = staticMemberExpression.Object.GetKey();
                var property = staticMemberExpression.Property.GetKey();

                if (obj == "Symbol")
                {
                    if (property == "iterator")
                    {
                        return GlobalSymbolRegistry.Iterator._value;
                    }
                    if (property == "toPrimitive")
                    {
                        return GlobalSymbolRegistry.ToPrimitive._value;
                    }
                }
            }

            return ExceptionHelper.ThrowArgumentException<string>("Unable to extract correct key");
        }
    }
}