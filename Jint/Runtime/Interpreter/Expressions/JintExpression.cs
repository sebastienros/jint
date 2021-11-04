#nullable enable

using System.Diagnostics;
using System.Runtime.CompilerServices;
using Esprima;
using Esprima.Ast;
using Jint.Native;
using Jint.Native.Array;
using Jint.Native.Iterator;
using Jint.Native.Number;
using Jint.Runtime.References;

namespace Jint.Runtime.Interpreter.Expressions
{
    /// <summary>
    /// Adapter to get different types of results, including Reference which is not a JsValue.
    /// </summary>
    internal readonly struct ExpressionResult
    {
        public readonly ExpressionCompletionType Type;
        public readonly Location Location;
        public readonly object Value;

        public ExpressionResult(ExpressionCompletionType type, object value, in Location location)
        {
            Type = type;
            Value = value;
            Location = location;
        }

        public bool IsAbrupt() => Type != ExpressionCompletionType.Normal && Type != ExpressionCompletionType.Reference;

        public static implicit operator ExpressionResult(in Completion result)
        {
            return new ExpressionResult((ExpressionCompletionType) result.Type, result.Value!, result.Location);
        }
    }

    internal enum ExpressionCompletionType : byte
    {
        Normal = 0,
        Return = 1,
        Throw = 2,
        Reference
    }

    internal abstract class JintExpression
    {
        // require sub-classes to set to false explicitly to skip virtual call
        protected bool _initialized = true;

        protected internal readonly Expression _expression;

        protected JintExpression(Expression expression)
        {
            _expression = expression;
        }

        /// <summary>
        /// Resolves the underlying value for this expression.
        /// By default uses the Engine for resolving.
        /// </summary>
        /// <param name="context"></param>
        /// <seealso cref="JintLiteralExpression"/>
        public virtual Completion GetValue(EvaluationContext context)
        {
            var result = Evaluate(context);
            if (result.Type != ExpressionCompletionType.Reference)
            {
                return new Completion((CompletionType) result.Type, (JsValue) result.Value, result.Location);
            }

            var jsValue = context.Engine.GetValue((Reference) result.Value, true);
            return new Completion(CompletionType.Normal, jsValue, null, _expression.Location);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ExpressionResult Evaluate(EvaluationContext context)
        {
            context.LastSyntaxNode = _expression;
            if (!_initialized)
            {
                Initialize(context);
                _initialized = true;
            }
            return EvaluateInternal(context);
        }

        /// <summary>
        /// Opportunity to build one-time structures and caching based on lexical context.
        /// </summary>
        /// <param name="context"></param>
        protected virtual void Initialize(EvaluationContext context)
        {
        }

        protected abstract ExpressionResult EvaluateInternal(EvaluationContext context);

        /// <summary>
        /// https://tc39.es/ecma262/#sec-normalcompletion
        /// </summary>
        /// <remarks>
        /// We use custom type that is translated to Completion later on.
        /// </remarks>
        [DebuggerStepThrough]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected ExpressionResult NormalCompletion(JsValue value)
        {
            return new ExpressionResult(ExpressionCompletionType.Normal, value, _expression.Location);
        }

        protected ExpressionResult NormalCompletion(Reference value)
        {
            return new ExpressionResult(ExpressionCompletionType.Reference, value, _expression.Location);
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-throwcompletion
        /// </summary>
        /// <remarks>
        /// We use custom type that is translated to Completion later on.
        /// </remarks>
        [DebuggerStepThrough]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected ExpressionResult ThrowCompletion(JsValue value)
        {
            return new ExpressionResult(ExpressionCompletionType.Throw, value, _expression.Location);
        }

        /// <summary>
        /// If we'd get Esprima source, we would just refer to it, but this makes error messages easier to decipher.
        /// </summary>
        internal string SourceText => ToString(_expression) ?? "*unknown*";

        internal static string? ToString(Expression expression)
        {
            while (true)
            {
                if (expression is Literal literal)
                {
                    return EsprimaExtensions.LiteralKeyToString(literal);
                }

                if (expression is Identifier identifier)
                {
                    return identifier.Name;
                }

                if (expression is MemberExpression memberExpression)
                {
                    return ToString(memberExpression.Object) + "." + ToString(memberExpression.Property);
                }

                if (expression is CallExpression callExpression)
                {
                    expression = callExpression.Callee;
                    continue;
                }

                return null;
            }
        }

        protected internal static JintExpression Build(Engine engine, Expression expression)
        {
            var result = expression.Type switch
            {
                Nodes.AssignmentExpression => JintAssignmentExpression.Build(engine, (AssignmentExpression) expression),
                Nodes.ArrayExpression => new JintArrayExpression((ArrayExpression) expression),
                Nodes.ArrowFunctionExpression => new JintArrowFunctionExpression(engine, (ArrowFunctionExpression) expression),
                Nodes.BinaryExpression => JintBinaryExpression.Build(engine, (BinaryExpression) expression),
                Nodes.CallExpression => new JintCallExpression((CallExpression) expression),
                Nodes.ConditionalExpression => new JintConditionalExpression(engine, (ConditionalExpression) expression),
                Nodes.FunctionExpression => new JintFunctionExpression(engine, (FunctionExpression) expression),
                Nodes.Identifier => new JintIdentifierExpression((Identifier) expression),
                Nodes.Literal => JintLiteralExpression.Build((Literal) expression),
                Nodes.LogicalExpression => ((BinaryExpression) expression).Operator switch
                {
                    BinaryOperator.LogicalAnd => new JintLogicalAndExpression((BinaryExpression) expression),
                    BinaryOperator.LogicalOr => new JintLogicalOrExpression(engine, (BinaryExpression) expression),
                    BinaryOperator.NullishCoalescing => new NullishCoalescingExpression(engine, (BinaryExpression) expression),
                    _ => null
                },
                Nodes.MemberExpression => new JintMemberExpression((MemberExpression) expression),
                Nodes.NewExpression => new JintNewExpression((NewExpression) expression),
                Nodes.ObjectExpression => new JintObjectExpression((ObjectExpression) expression),
                Nodes.SequenceExpression => new JintSequenceExpression((SequenceExpression) expression),
                Nodes.ThisExpression => new JintThisExpression((ThisExpression) expression),
                Nodes.UpdateExpression => new JintUpdateExpression((UpdateExpression) expression),
                Nodes.UnaryExpression => JintUnaryExpression.Build(engine, (UnaryExpression) expression),
                Nodes.SpreadElement => new JintSpreadExpression(engine, (SpreadElement) expression),
                Nodes.TemplateLiteral => new JintTemplateLiteralExpression((TemplateLiteral) expression),
                Nodes.TaggedTemplateExpression => new JintTaggedTemplateExpression((TaggedTemplateExpression) expression),
                Nodes.ClassExpression => new JintClassExpression((ClassExpression) expression),
                Nodes.Super => new JintSuperExpression((Super) expression),
                Nodes.MetaProperty => new JintMetaPropertyExpression((MetaProperty) expression),
                Nodes.ChainExpression => ((ChainExpression) expression).Expression.Type == Nodes.CallExpression
                    ? new JintCallExpression((CallExpression) ((ChainExpression) expression).Expression)
                    : new JintMemberExpression((MemberExpression) ((ChainExpression) expression).Expression),
                _ =>  null
            };

            if (result is null)
            {
                ExceptionHelper.ThrowArgumentOutOfRangeException(nameof(expression), $"unsupported expression type '{expression.Type}'");
            }

            return result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected static JsValue Divide(JsValue lval, JsValue rval)
        {
            return AreIntegerOperands(lval, rval)
                ? DivideInteger(lval, rval)
                : DivideComplex(lval, rval);
        }

        private static JsValue DivideInteger(JsValue lval, JsValue rval)
        {
            var lN = lval.AsInteger();
            var rN = rval.AsInteger();

            if (lN == 0 && rN == 0)
            {
                return JsNumber.DoubleNaN;
            }

            if (rN == 0)
            {
                return lN > 0 ? double.PositiveInfinity : double.NegativeInfinity;
            }

            if (lN % rN == 0)
            {
                return lN / rN;
            }

            return (double) lN / rN;
        }

        private static JsValue DivideComplex(JsValue lval, JsValue rval)
        {
            if (lval.IsUndefined() || rval.IsUndefined())
            {
                return Undefined.Instance;
            }
            else
            {
                var lN = TypeConverter.ToNumber(lval);
                var rN = TypeConverter.ToNumber(rval);

                if (double.IsNaN(rN) || double.IsNaN(lN))
                {
                    return JsNumber.DoubleNaN;
                }

                if (double.IsInfinity(lN) && double.IsInfinity(rN))
                {
                    return JsNumber.DoubleNaN;
                }

                if (double.IsInfinity(lN) && rN == 0)
                {
                    if (NumberInstance.IsNegativeZero(rN))
                    {
                        return -lN;
                    }

                    return lN;
                }

                if (lN == 0 && rN == 0)
                {
                    return JsNumber.DoubleNaN;
                }

                if (rN == 0)
                {
                    if (NumberInstance.IsNegativeZero(rN))
                    {
                        return lN > 0 ? -double.PositiveInfinity : -double.NegativeInfinity;
                    }

                    return lN > 0 ? double.PositiveInfinity : double.NegativeInfinity;
                }

                return lN / rN;
            }

        }

        protected static bool Equal(JsValue x, JsValue y)
        {
            return x.Type == y.Type
                ? JintBinaryExpression.StrictlyEqual(x, y)
                : EqualUnlikely(x, y);
        }

        private static bool EqualUnlikely(JsValue x, JsValue y)
        {
            if (x._type == InternalTypes.Null && y._type == InternalTypes.Undefined)
            {
                return true;
            }

            if (x._type == InternalTypes.Undefined && y._type == InternalTypes.Null)
            {
                return true;
            }

            if (x.IsNumber() && y.IsString())
            {
                return Equal(x, TypeConverter.ToNumber(y));
            }

            if (x.IsString() && y.IsNumber())
            {
                return Equal(TypeConverter.ToNumber(x), y);
            }

            if (x.IsBoolean())
            {
                return Equal(TypeConverter.ToNumber(x), y);
            }

            if (y.IsBoolean())
            {
                return Equal(x, TypeConverter.ToNumber(y));
            }

            const InternalTypes stringOrNumber = InternalTypes.String | InternalTypes.Integer | InternalTypes.Number;

            if (y.IsObject() && (x._type & stringOrNumber) != 0)
            {
                return Equal(x, TypeConverter.ToPrimitive(y));
            }

            if (x.IsObject() && ((y._type & stringOrNumber) != 0))
            {
                return Equal(TypeConverter.ToPrimitive(x), y);
            }

            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected static JsValue Compare(JsValue x, JsValue y, bool leftFirst = true) =>
            x._type == y._type && x._type == InternalTypes.Integer
                ? CompareInteger(x, y, leftFirst)
                : CompareComplex(x, y, leftFirst);

        private static JsValue CompareInteger(JsValue x, JsValue y, bool leftFirst)
        {
            int nx, ny;
            if (leftFirst)
            {
                nx = x.AsInteger();
                ny = y.AsInteger();
            }
            else
            {
                ny = y.AsInteger();
                nx = x.AsInteger();
            }

            return nx < ny;
        }

        private static  JsValue CompareComplex(JsValue x, JsValue y, bool leftFirst)
        {
            JsValue px, py;
            if (leftFirst)
            {
                px = TypeConverter.ToPrimitive(x, Types.Number);
                py = TypeConverter.ToPrimitive(y, Types.Number);
            }
            else
            {
                py = TypeConverter.ToPrimitive(y, Types.Number);
                px = TypeConverter.ToPrimitive(x, Types.Number);
            }

            var typea = px.Type;
            var typeb = py.Type;

            if (typea != Types.String || typeb != Types.String)
            {
                var nx = TypeConverter.ToNumber(px);
                var ny = TypeConverter.ToNumber(py);

                if (double.IsNaN(nx) || double.IsNaN(ny))
                {
                    return Undefined.Instance;
                }

                if (nx == ny)
                {
                    return false;
                }

                if (double.IsPositiveInfinity(nx))
                {
                    return false;
                }

                if (double.IsPositiveInfinity(ny))
                {
                    return true;
                }

                if (double.IsNegativeInfinity(ny))
                {
                    return false;
                }

                if (double.IsNegativeInfinity(nx))
                {
                    return true;
                }

                return nx < ny;
            }

            return string.CompareOrdinal(TypeConverter.ToString(x), TypeConverter.ToString(y)) < 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected static void BuildArguments(EvaluationContext context, JintExpression[] jintExpressions, JsValue[] targetArray)
        {
            for (var i = 0; i < jintExpressions.Length; i++)
            {
                var completion = jintExpressions[i].GetValue(context);
                targetArray[i] = completion.Value!.Clone();
            }
        }

        protected JsValue[] BuildArgumentsWithSpreads(EvaluationContext context, JintExpression[] jintExpressions)
        {
            var args = new System.Collections.Generic.List<JsValue>(jintExpressions.Length);
            for (var i = 0; i < jintExpressions.Length; i++)
            {
                var jintExpression = jintExpressions[i];
                if (jintExpression is JintSpreadExpression jse)
                {
                    jse.GetValueAndCheckIterator(context, out var objectInstance, out var iterator);
                    // optimize for array unless someone has touched the iterator
                    if (objectInstance is ArrayInstance ai && ai.HasOriginalIterator)
                    {
                        var length = ai.GetLength();
                        for (uint j = 0; j < length; ++j)
                        {
                            if (ai.TryGetValue(j, out var value))
                            {
                                args.Add(value);
                            }
                        }
                    }
                    else
                    {
                        var protocol = new ArraySpreadProtocol(context.Engine, args, iterator);
                        protocol.Execute();
                    }
                }
                else
                {
                    var completion = jintExpression.GetValue(context);
                    args.Add(completion.Value!.Clone());
                }
            }

            return args.ToArray();
        }

        private sealed class ArraySpreadProtocol : IteratorProtocol
        {
            private readonly System.Collections.Generic.List<JsValue> _instance;

            public ArraySpreadProtocol(
                Engine engine,
                System.Collections.Generic.List<JsValue> instance,
                IteratorInstance iterator) : base(engine, iterator, 0)
            {
                _instance = instance;
            }

            protected override void ProcessItem(JsValue[] args, JsValue currentValue)
            {
                _instance.Add(currentValue);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected static bool AreIntegerOperands(JsValue left, JsValue right)
        {
            return left._type == right._type && left._type == InternalTypes.Integer;
        }
    }
}