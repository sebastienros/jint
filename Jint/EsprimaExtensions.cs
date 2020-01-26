using System;
using System.Globalization;
using System.Runtime.CompilerServices;
using Esprima.Ast;
using Jint.Native;
using Jint.Runtime;
using Jint.Runtime.Interpreter.Expressions;

namespace Jint
{
    public static class EsprimaExtensions
    {
        public static JsValue GetKey(this Property property, Engine engine) => GetKey(property.Key, engine, property.Computed);

        internal static JsValue GetKey<T>(this T expression, Engine engine, bool computed) where T : class, Expression
        {
            if (expression is Literal literal)
            {
                return LiteralKeyToString(literal);
            }

            if (!computed && expression is Identifier identifier)
            {
                return identifier.Name;
            }

            if (!TryGetComputedPropertyKey(expression, engine, out var propertyKey))
            {
                ExceptionHelper.ThrowArgumentException("Unable to extract correct key, node type: " + expression.Type);
            }

            return propertyKey;
        }

        private static bool TryGetComputedPropertyKey<T>(T expression, Engine engine, out JsValue propertyKey)
            where T : class, Expression
        {
            if (expression.Type == Nodes.Identifier
                || expression.Type == Nodes.CallExpression
                || expression.Type == Nodes.BinaryExpression
                || expression.Type == Nodes.UpdateExpression
                || expression is StaticMemberExpression)
            {
                propertyKey = TypeConverter.ToPropertyKey(JintExpression.Build(engine, expression).GetValue());
                return true;
            }

            propertyKey = string.Empty;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool IsFunctionWithName<T>(this T node) where T : class, INode
        {
            var type = node.Type;
            return type == Nodes.FunctionExpression || type == Nodes.ArrowFunctionExpression || type == Nodes.ArrowParameterPlaceHolder;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static string LiteralKeyToString(Literal literal)
        {
            // prevent conversion to scientific notation
            if (literal.Value is double d)
            {
                return DoubleToString(d);
            }
            return literal.Value as string ?? Convert.ToString(literal.Value, provider: null);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static string DoubleToString(double d)
        {
            return (d - (long) d) == 0 ? ((long) d).ToString() : d.ToString(CultureInfo.InvariantCulture);
        }
    }
}