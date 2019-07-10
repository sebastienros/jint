using System;
using System.Runtime.CompilerServices;
using Esprima.Ast;
using Jint.Native.Symbol;
using Jint.Runtime;
using Jint.Runtime.Interpreter.Expressions;

namespace Jint
{
    public static class EsprimaExtensions
    {
        public static string GetKey<T>(this T expression, Engine engine) where T : class, Expression
        {
            if (expression is Literal literal)
            {
                return literal.Value as string ?? Convert.ToString(literal.Value, provider: null);
            }

            if (expression is Esprima.Ast.Identifier identifier)
            {
                return identifier.Name;
            }

            if (expression is StaticMemberExpression staticMemberExpression)
            {
                var obj = staticMemberExpression.Object.GetKey(engine);
                var property = staticMemberExpression.Property.GetKey(engine);

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

            if (expression.Type == Nodes.CallExpression
                || expression.Type == Nodes.BinaryExpression
                || expression.Type == Nodes.UpdateExpression)
            {
                return Convert.ToString(JintExpression.Build(engine, expression).GetValue());
            }

            return ExceptionHelper.ThrowArgumentException<string>("Unable to extract correct key, node type: " + expression.Type);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool IsFunctionWithName<T>(this T node) where T : class, INode
        {
            var type = node.Type;
            return type == Nodes.FunctionExpression || type == Nodes.ArrowFunctionExpression || type == Nodes.ArrowParameterPlaceHolder;
        }
    }
}