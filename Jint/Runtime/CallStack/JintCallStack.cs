#nullable enable

using System.Collections.Generic;
using System.Linq;
using System.Text;
using Esprima;
using Esprima.Ast;
using Jint.Collections;
using Jint.Pooling;

namespace Jint.Runtime.CallStack
{
    internal class JintCallStack
    {
        private readonly RefStack<CallStackElement> _stack = new();
        private readonly Dictionary<CallStackElement, int>? _statistics;

        public JintCallStack(bool trackRecursionDepth)
        {
            if (trackRecursionDepth)
            {
                _statistics = new Dictionary<CallStackElement, int>(CallStackElementComparer.Instance);
            }
        }

        public int Push(in CallStackElement item)
        {
            _stack.Push(item);
            if (_statistics is not null)
            {
                if (_statistics.ContainsKey(item))
                {
                    return ++_statistics[item];
                }
                else
                {
                    _statistics.Add(item, 0);
                    return 0;
                }
            }

            return -1;
        }

        public CallStackElement Pop()
        {
            ref readonly var item = ref _stack.Pop();
            if (_statistics is not null)
            {
                if (_statistics[item] == 0)
                {
                    _statistics.Remove(item);
                }
                else
                {
                    _statistics[item]--;
                }
            }

            return item;
        }

        public void Clear()
        {
            _stack.Clear();
            _statistics?.Clear();
        }

        public override string ToString()
        {
            return string.Join("->", _stack.Select(cse => cse.ToString()).Reverse());
        }

        internal string BuildCallStackString(Location location)
        {
            static void AppendLocation(
                StringBuilder sb,
                string shortDescription,
                Location loc,
                in NodeList<Expression>? arguments)
            {
                sb
                    .Append("   at");

                if (!string.IsNullOrWhiteSpace(shortDescription))
                {
                    sb
                        .Append(" ")
                        .Append(shortDescription);
                }

                if (arguments is not null)
                {
                    // it's a function
                    sb.Append(" (");
                    for (var index = 0; index < arguments.Value.Count; index++)
                    {
                        if (index != 0)
                        {
                            sb.Append(", ");
                        }

                        var arg = arguments.Value[index];
                        sb.Append(GetPropertyKey(arg));
                    }
                    sb.Append(")");
                }

                sb
                    .Append(" ")
                    .Append(loc.Source)
                    .Append(":")
                    .Append(loc.Start.Line)
                    .Append(":")
                    .Append(loc.Start.Column + 1) // report column number instead of index
                    .AppendLine();
            }

            using var sb = StringBuilderPool.Rent();

            // stack is one frame behind function-wise when we start to process it from expression level
            var index = _stack._size - 1;
            var element = index >= 0 ? _stack[index] : (CallStackElement?) null;
            var shortDescription = element?.ToString() ?? "";

            AppendLocation(sb.Builder, shortDescription, location, element?.Arguments);

            location = element?.Location ?? default;
            index--;

            while (index >= -1)
            {
                element = index >= 0 ? _stack[index] : null;
                shortDescription = element?.ToString() ?? "";

                AppendLocation(sb.Builder, shortDescription, location, element?.Arguments);

                location = element?.Location ?? default;
                index--;
            }

            return sb.ToString().TrimEnd();
        }

        /// <summary>
        /// A version of <see cref="EsprimaExtensions.GetKey"/> that cannot get into loop as we are already building a stack.
        /// </summary>
        private static string GetPropertyKey(Expression expression)
        {
            if (expression is Literal literal)
            {
                return EsprimaExtensions.LiteralKeyToString(literal);
            }

            if (expression is Identifier identifier)
            {
                return identifier.Name ?? "";
            }

            if (expression is StaticMemberExpression staticMemberExpression)
            {
                return GetPropertyKey(staticMemberExpression.Object) + "." +
                       GetPropertyKey(staticMemberExpression.Property);
            }

            return "?";
        }
    }
}