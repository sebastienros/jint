using System.Runtime.CompilerServices;
using Esprima.Ast;
using Jint.Native;
using Jint.Native.Array;
using Jint.Native.Iterator;
using Jint.Native.Number;
using Jint.Runtime.Environments;

namespace Jint.Runtime.Interpreter.Expressions
{
    internal abstract class JintExpression
    {
        // require sub-classes to set to false explicitly to skip virtual call
        protected bool _initialized = true;

        protected readonly Engine _engine;
        protected internal readonly INode _expression;

        protected JintExpression(Engine engine, INode expression)
        {
            _engine = engine;
            _expression = expression;
        }

        /// <summary>
        /// Resolves the underlying value for this expression.
        /// By default uses the Engine for resolving.
        /// </summary>
        /// <seealso cref="JintLiteralExpression"/>
        public virtual JsValue GetValue()
        {
            return _engine.GetValue(Evaluate(), true);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public object Evaluate()
        {
            _engine._lastSyntaxNode = _expression;
            if (!_initialized)
            {
                Initialize();
                _initialized = true;
            }
            return EvaluateInternal();
        }

        /// <summary>
        /// Opportunity to build one-time structures and caching based on lexical context.
        /// </summary>
        protected virtual void Initialize()
        {
        }

        protected abstract object EvaluateInternal();

        protected internal static JintExpression Build(Engine engine, Expression expression)
        {
            switch (expression.Type)
            {
                case Nodes.AssignmentExpression:
                    return JintAssignmentExpression.Build(engine, (AssignmentExpression) expression);

                case Nodes.ArrayExpression:
                    return new JintArrayExpression(engine, (ArrayExpression) expression);

                case Nodes.ArrowFunctionExpression:
                    return new JintArrowFunctionExpression(engine, (IFunction) expression);

                case Nodes.BinaryExpression:
                    return JintBinaryExpression.Build(engine, (BinaryExpression) expression);

                case Nodes.CallExpression:
                    return new JintCallExpression(engine, (CallExpression) expression);

                case Nodes.ConditionalExpression:
                    return new JintConditionalExpression(engine, (ConditionalExpression) expression);

                case Nodes.FunctionExpression:
                    return new JintFunctionExpression(engine, (IFunction) expression);

                case Nodes.Identifier:
                    return new JintIdentifierExpression(engine, (Identifier) expression);

                case Nodes.Literal:
                    return JintLiteralExpression.Build(engine, (Literal) expression);

                case Nodes.LogicalExpression:
                    var binaryExpression = (BinaryExpression) expression;
                    switch (binaryExpression.Operator)
                    {
                        case BinaryOperator.LogicalAnd:
                            return new JintLogicalAndExpression(engine, binaryExpression);
                        case BinaryOperator.LogicalOr:
                            return new JintLogicalOrExpression(engine, binaryExpression);
                        default:
                            return ExceptionHelper.ThrowArgumentOutOfRangeException<JintExpression>();
                    }

                case Nodes.MemberExpression:
                    return new JintMemberExpression(engine, (MemberExpression) expression);

                case Nodes.NewExpression:
                    return new JintNewExpression(engine, (NewExpression) expression);

                case Nodes.ObjectExpression:
                    return new JintObjectExpression(engine, (ObjectExpression) expression);

                case Nodes.SequenceExpression:
                    return new JintSequenceExpression(engine, (SequenceExpression) expression);

                case Nodes.ThisExpression:
                    return new JintThisExpression(engine, (ThisExpression) expression);

                case Nodes.UpdateExpression:
                    return new JintUpdateExpression(engine, (UpdateExpression) expression);

                case Nodes.UnaryExpression:
                    return JintUnaryExpression.Build(engine, (UnaryExpression) expression);

                case Nodes.SpreadElement:
                    return new JintSpreadExpression(engine, (SpreadElement) expression);

                case Nodes.TemplateLiteral:
                    return new JintTemplateLiteralExpression(engine, (TemplateLiteral) expression);

                case Nodes.TaggedTemplateExpression:
                    return new JintTaggedTemplateExpression(engine, (TaggedTemplateExpression) expression);

                default:
                    ExceptionHelper.ThrowArgumentOutOfRangeException(nameof(expression), $"unsupported language element '{expression.Type}'");
                    return null;
            }
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
            if (x._type == y._type || x.IsNumber() && y.IsNumber())
            {
                return JintBinaryExpression.StrictlyEqual(x, y);
            }

            return EqualUnlikely(x, y);
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

            if (x.IsNumber() && y._type == InternalTypes.String)
            {
                return Equal(x, TypeConverter.ToNumber(y));
            }

            if (x._type == InternalTypes.String && y.IsNumber())
            {
                return Equal(TypeConverter.ToNumber(x), y);
            }

            if (x._type == InternalTypes.Boolean)
            {
                return Equal(TypeConverter.ToNumber(x), y);
            }

            if (y._type == InternalTypes.Boolean)
            {
                return Equal(x, TypeConverter.ToNumber(y));
            }

            if (y._type == InternalTypes.Object && (x._type == InternalTypes.String || x.IsNumber()))
            {
                return Equal(x, TypeConverter.ToPrimitive(y));
            }

            if (x._type == InternalTypes.Object && (y._type == InternalTypes.String || y.IsNumber()))
            {
                return Equal(TypeConverter.ToPrimitive(x), y);
            }

            return false;
        }

        protected internal static bool SameValue(JsValue x, JsValue y)
        {
            var typea = TypeConverter.GetInternalPrimitiveType(x);
            var typeb = TypeConverter.GetInternalPrimitiveType(y);

            if (typea != typeb)
            {
                if (typea == InternalTypes.Integer)
                {
                    typea = InternalTypes.Number;
                }
                if (typeb == InternalTypes.Integer)
                {
                    typeb = InternalTypes.Number;
                }

                if (typea != typeb)
                {
                    return false;
                }
            }

            switch (typea)
            {
                case InternalTypes.None:
                    return true;

                case InternalTypes.Integer:
                    return x.AsInteger() == y.AsInteger();

                case InternalTypes.Number:
                    var nx = TypeConverter.ToNumber(x);
                    var ny = TypeConverter.ToNumber(y);

                    if (double.IsNaN(nx) && double.IsNaN(ny))
                    {
                        return true;
                    }

                    if (nx == ny)
                    {
                        if (nx == 0)
                        {
                            // +0 !== -0
                            return NumberInstance.IsNegativeZero(nx) == NumberInstance.IsNegativeZero(ny);
                        }

                        return true;
                    }

                    return false;
                case InternalTypes.String:
                    return TypeConverter.ToString(x) == TypeConverter.ToString(y);
                case InternalTypes.Boolean:
                    return TypeConverter.ToBoolean(x) == TypeConverter.ToBoolean(y);
                default:
                    return x == y;
            }
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
        protected static void BuildArguments(JintExpression[] jintExpressions, JsValue[] targetArray)
        {
            for (var i = 0; i < jintExpressions.Length; i++)
            {
                targetArray[i] = jintExpressions[i].GetValue();
            }
        }

        protected JsValue[] BuildArgumentsWithSpreads(JintExpression[] jintExpressions)
        {
            var args = new System.Collections.Generic.List<JsValue>(jintExpressions.Length);
            for (var i = 0; i < jintExpressions.Length; i++)
            {
                var jintExpression = jintExpressions[i];
                if (jintExpression is JintSpreadExpression jse)
                {
                    jse.GetValueAndCheckIterator(out var objectInstance, out var iterator);
                    // optimize for array
                    if (objectInstance is ArrayInstance ai)
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
                        var protocol = new ArraySpreadProtocol(_engine, args, iterator);
                        protocol.Execute();
                    }
                }
                else
                {
                    args.Add(jintExpression.GetValue());
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
                IIterator iterator) : base(engine, iterator, 0)
            {
                _instance = instance;
            }

            protected override void ProcessItem(JsValue[] args, JsValue currentValue)
            {
                var jsValue = ExtractValueFromIteratorInstance(currentValue);
                _instance.Add(jsValue);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected bool TryGetIdentifierEnvironmentWithBindingValue(
            in Key expressionName,
            out EnvironmentRecord record,
            out JsValue value)
        {
            var env = _engine.ExecutionContext.LexicalEnvironment;
            var strict = StrictModeScope.IsStrictModeCode;
            return LexicalEnvironment.TryGetIdentifierEnvironmentWithBindingValue(
                env,
                expressionName,
                strict,
                out record,
                out value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected bool TryGetIdentifierEnvironmentWithBindingValue(
            bool strict,
            in Key expressionName,
            out EnvironmentRecord record,
            out JsValue value)
        {
            var env = _engine.ExecutionContext.LexicalEnvironment;
            return LexicalEnvironment.TryGetIdentifierEnvironmentWithBindingValue(
                env,
                expressionName,
                strict,
                out record,
                out value);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected static bool AreIntegerOperands(JsValue left, JsValue right)
        {
            return left._type == right._type && left._type == InternalTypes.Integer;
        }
    }
}