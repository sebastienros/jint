using System.Numerics;
using System.Runtime.CompilerServices;
using Jint.Native;
using Jint.Native.Iterator;
using Jint.Native.Number;

namespace Jint.Runtime.Interpreter.Expressions;

internal abstract class JintExpression
{
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
    public virtual JsValue GetValue(EvaluationContext context)
    {
        var result = Evaluate(context);
        if (result is not Reference reference)
        {
            return (JsValue) result;
        }

        return context.Engine.GetValue(reference, returnReferenceToPool: true);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | (MethodImplOptions) 512)]
    public object Evaluate(EvaluationContext context)
    {
        var oldSyntaxElement = context.LastSyntaxElement;
        context.PrepareFor(_expression);

        var result = EvaluateInternal(context);

        context.LastSyntaxElement = oldSyntaxElement;

        return result;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal object EvaluateWithoutNodeTracking(EvaluationContext context)
    {
        return EvaluateInternal(context);
    }

    protected abstract object EvaluateInternal(EvaluationContext context);

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
                return AstExtensions.LiteralKeyToString(literal);
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

    protected internal static JintExpression Build(Expression expression)
    {
        if (expression.UserData is JintExpression preparedExpression)
        {
            return preparedExpression;
        }

        var result = expression.Type switch
        {
            NodeType.AssignmentExpression => JintAssignmentExpression.Build((AssignmentExpression) expression),
            NodeType.ArrayExpression => JintArrayExpression.Build((ArrayExpression) expression),
            NodeType.ArrowFunctionExpression => new JintArrowFunctionExpression((ArrowFunctionExpression) expression),
            NodeType.BinaryExpression => JintBinaryExpression.Build((NonLogicalBinaryExpression) expression),
            NodeType.CallExpression => new JintCallExpression((CallExpression) expression),
            NodeType.ConditionalExpression => new JintConditionalExpression((ConditionalExpression) expression),
            NodeType.FunctionExpression => new JintFunctionExpression((FunctionExpression) expression),
            NodeType.Identifier => new JintIdentifierExpression((Identifier) expression),
            NodeType.PrivateIdentifier => new JintPrivateIdentifierExpression((PrivateIdentifier) expression),
            NodeType.Literal => JintLiteralExpression.Build((Literal) expression),
            NodeType.LogicalExpression => ((LogicalExpression) expression).Operator switch
            {
                Operator.LogicalAnd => new JintLogicalAndExpression((LogicalExpression) expression),
                Operator.LogicalOr => new JintLogicalOrExpression((LogicalExpression) expression),
                Operator.NullishCoalescing => new NullishCoalescingExpression((LogicalExpression) expression),
                _ => null
            },
            NodeType.MemberExpression => new JintMemberExpression((MemberExpression) expression),
            NodeType.NewExpression => new JintNewExpression((NewExpression) expression),
            NodeType.ObjectExpression => JintObjectExpression.Build((ObjectExpression) expression),
            NodeType.SequenceExpression => new JintSequenceExpression((SequenceExpression) expression),
            NodeType.ThisExpression => new JintThisExpression((ThisExpression) expression),
            NodeType.UpdateExpression => new JintUpdateExpression((UpdateExpression) expression),
            NodeType.UnaryExpression => JintUnaryExpression.Build((NonUpdateUnaryExpression) expression),
            NodeType.SpreadElement => new JintSpreadExpression((SpreadElement) expression),
            NodeType.TemplateLiteral => new JintTemplateLiteralExpression((TemplateLiteral) expression),
            NodeType.TaggedTemplateExpression => new JintTaggedTemplateExpression((TaggedTemplateExpression) expression),
            NodeType.ClassExpression => new JintClassExpression((ClassExpression) expression),
            NodeType.ImportExpression => new JintImportExpression((ImportExpression) expression),
            NodeType.Super => new JintSuperExpression((Super) expression),
            NodeType.MetaProperty => new JintMetaPropertyExpression((MetaProperty) expression),
            NodeType.ChainExpression => ((ChainExpression) expression).Expression.Type == NodeType.CallExpression
                ? new JintCallExpression((CallExpression) ((ChainExpression) expression).Expression)
                : new JintMemberExpression((MemberExpression) ((ChainExpression) expression).Expression),
            NodeType.AwaitExpression => new JintAwaitExpression((AwaitExpression) expression),
            NodeType.YieldExpression => new JintYieldExpression((YieldExpression) expression),
            _ => null
        };

        if (result is null)
        {
            ExceptionHelper.ThrowArgumentOutOfRangeException(nameof(expression), $"unsupported expression type '{expression.Type}'");
        }

        return result;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected static JsValue Divide(EvaluationContext context, JsValue left, JsValue right)
    {
        JsValue result;
        if (AreIntegerOperands(left, right))
        {
            result = DivideInteger(left, right);
        }
        else if (JintBinaryExpression.AreNonBigIntOperands(left, right))
        {
            result = DivideComplex(left, right);
        }
        else
        {
            JintBinaryExpression.AssertValidBigIntArithmeticOperands(left, right);
            var x = TypeConverter.ToBigInt(left);
            var y = TypeConverter.ToBigInt(right);

            if (y == 0)
            {
                ExceptionHelper.ThrowRangeError(context.Engine.Realm, "Division by zero");
            }

            result = JsBigInt.Create(x / y);
        }

        return result;
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

        if (lN % rN == 0 && (lN != 0 || rN > 0))
        {
            return JsNumber.Create(lN / rN);
        }

        return (double) lN / rN;
    }

    private static JsValue DivideComplex(JsValue lval, JsValue rval)
    {
        if (lval.IsUndefined() || rval.IsUndefined())
        {
            return JsValue.Undefined;
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected static JsValue Compare(JsValue x, JsValue y, bool leftFirst = true) =>
        x.IsNumber() && y.IsNumber()
            ? CompareNumber(x, y, leftFirst)
            : CompareComplex(x, y, leftFirst);

    private static JsValue CompareNumber(JsValue x, JsValue y, bool leftFirst)
    {
        double nx, ny;
        if (leftFirst)
        {
            nx = x.AsNumber();
            ny = y.AsNumber();
        }
        else
        {
            ny = y.AsNumber();
            nx = x.AsNumber();
        }

        if (x.IsInteger() && y.IsInteger())
        {
            return (int) nx < (int) ny ? JsBoolean.True : JsBoolean.False;
        }

        if (!double.IsInfinity(nx) && !double.IsInfinity(ny) && !double.IsNaN(nx) && !double.IsNaN(ny))
        {
            return nx < ny ? JsBoolean.True : JsBoolean.False;
        }

        return CompareComplex(x, y, leftFirst);
    }

    private static JsValue CompareComplex(JsValue x, JsValue y, bool leftFirst)
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
            if (typea == Types.BigInt || typeb == Types.BigInt)
            {
                if (typea == typeb)
                {
                    return TypeConverter.ToBigInt(px) < TypeConverter.ToBigInt(py) ? JsBoolean.True : JsBoolean.False;
                }

                if (typea == Types.BigInt)
                {
                    if (py is JsString jsStringY)
                    {
                        if (!TypeConverter.TryStringToBigInt(jsStringY.ToString(), out var temp))
                        {
                            return JsValue.Undefined;
                        }
                        return TypeConverter.ToBigInt(px) < temp ? JsBoolean.True : JsBoolean.False;
                    }

                    var numberB = TypeConverter.ToNumber(py);
                    if (double.IsNaN(numberB))
                    {
                        return JsValue.Undefined;
                    }

                    if (double.IsPositiveInfinity(numberB))
                    {
                        return JsBoolean.True;
                    }

                    if (double.IsNegativeInfinity(numberB))
                    {
                        return JsBoolean.False;
                    }

                    var normalized = new BigInteger(Math.Ceiling(numberB));
                    return TypeConverter.ToBigInt(px) < normalized ? JsBoolean.True : JsBoolean.False;
                }

                if (px is JsString jsStringX)
                {
                    if (!TypeConverter.TryStringToBigInt(jsStringX.ToString(), out var temp))
                    {
                        return JsValue.Undefined;
                    }
                    return temp < TypeConverter.ToBigInt(py) ? JsBoolean.True : JsBoolean.False;
                }

                var numberA = TypeConverter.ToNumber(px);
                if (double.IsNaN(numberA))
                {
                    return JsValue.Undefined;
                }

                if (double.IsPositiveInfinity(numberA))
                {
                    return JsBoolean.False;
                }

                if (double.IsNegativeInfinity(numberA))
                {
                    return JsBoolean.True;
                }

                var normalizedA = new BigInteger(Math.Floor(numberA));
                return normalizedA < TypeConverter.ToBigInt(py);
            }

            var nx = TypeConverter.ToNumber(px);
            var ny = TypeConverter.ToNumber(py);

            if (double.IsNaN(nx) || double.IsNaN(ny))
            {
                return JsValue.Undefined;
            }

            if (nx == ny)
            {
                return JsBoolean.False;
            }

            if (double.IsPositiveInfinity(nx))
            {
                return JsBoolean.False;
            }

            if (double.IsPositiveInfinity(ny))
            {
                return JsBoolean.True;
            }

            if (double.IsNegativeInfinity(ny))
            {
                return JsBoolean.False;
            }

            if (double.IsNegativeInfinity(nx))
            {
                return JsBoolean.True;
            }

            return nx < ny ? JsBoolean.True : JsBoolean.False;
        }

        return string.CompareOrdinal(TypeConverter.ToString(x), TypeConverter.ToString(y)) < 0 ? JsBoolean.True : JsBoolean.False;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected static bool AreIntegerOperands(JsValue left, JsValue right)
    {
        return left._type == right._type && left._type == InternalTypes.Integer;
    }
}
