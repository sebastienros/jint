#if NET8_0_OR_GREATER
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Reflection;
using Jint.Native;
using Expression = System.Linq.Expressions.Expression;
using ConditionalExpression = System.Linq.Expressions.ConditionalExpression;

// The delegate is compiled with System.Linq.Expressions; every member it references is public
// (JsValue implicit operators, JsValueExtensions accessors, JsValue.Null) so the generated
// dynamic method needs no reflection-visibility relaxation. IL2075/IL3050 cover the reflection +
// dynamic-code use, which is gated behind RuntimeFeature.IsDynamicCodeCompiled at the call site.
#pragma warning disable IL2075
#pragma warning disable IL3050

namespace Jint.Runtime.Interop;

/// <summary>
/// Builds a strongly-typed delegate that binds JavaScript arguments to a CLR method and invokes it
/// directly, for the dominant interop shape where every parameter is an exact-type primitive
/// (<see cref="int"/>, <see cref="long"/>, <see cref="double"/>, <see cref="bool"/>,
/// <see cref="string"/>) or a pass-through <see cref="JsValue"/>. This removes, per call, the
/// <c>object?[]</c> parameter array, the argument boxes, the boxed return value, and the
/// return-mapper dictionary lookup that the reflection path pays.
/// <para>
/// The delegate only handles exact-type hits (the hot case). Any argument that is not the exact
/// expected JavaScript type — including a fractional number bound to an integer parameter, or a
/// number outside the target integer range — makes it return <see langword="false"/> so the caller
/// falls back to the full <c>TryCall</c> machinery, preserving today's conversion behavior
/// bit-for-bit. Exceptions thrown by the target method propagate as-is; the caller normalizes them
/// exactly like the reflection path does.
/// </para>
/// </summary>
internal static class CompiledMethodInvoker
{
    /// <summary>
    /// Invokes the bound method. Returns <see langword="true"/> and sets <paramref name="result"/>
    /// when every argument was an exact-type match; returns <see langword="false"/> (declining) when
    /// any argument requires the fallback conversion path.
    /// </summary>
    internal delegate bool Invoker(object? target, JsCallArguments arguments, out JsValue result);

    private static readonly MethodInfo _asNumber = typeof(JsValueExtensions).GetMethod(nameof(JsValueExtensions.AsNumber), [typeof(JsValue)])!;
    private static readonly MethodInfo _asBoolean = typeof(JsValueExtensions).GetMethod(nameof(JsValueExtensions.AsBoolean), [typeof(JsValue)])!;
    private static readonly MethodInfo _objectToString = typeof(object).GetMethod(nameof(ToString), Type.EmptyTypes)!;
    private static readonly MethodInfo _doubleIsNaN = typeof(double).GetMethod(nameof(double.IsNaN), [typeof(double)])!;
    private static readonly MethodInfo _doubleIsInfinity = typeof(double).GetMethod(nameof(double.IsInfinity), [typeof(double)])!;

    // (double) long.MaxValue rounds up to 2^63 which overflows long, the bound must be exclusive -
    // this mirrors InteropHelper.TryConvertNumberFast exactly.
    private const double LongMaxValueExclusiveUpperBound = 9223372036854775808d;

    internal static bool TryBuild(MethodDescriptor descriptor, [NotNullWhen(true)] out Invoker? invoker)
    {
        invoker = null;

        // Only plain (non-generic, non-params, no optional args, non-extension) MethodInfo call
        // sites are eligible; anything else keeps the reflection path.
        if (descriptor.Method is not MethodInfo method
            || descriptor.HasParams
            || descriptor.ParameterDefaultValuesCount != 0
            || descriptor.IsGenericMethod
            || descriptor.IsExtensionMethod
            || !method.IsPublic)
        {
            return false;
        }

        var declaringType = method.DeclaringType;
        if (declaringType is null || !declaringType.IsVisible)
        {
            // the compiled expression can only bind a call to a publicly visible declaring type
            return false;
        }

        // Open-instance delegates over value-type receivers need a by-ref first parameter, which
        // this simple Func/Action binding does not model - leave structs to the reflection path.
        if (!method.IsStatic && declaringType.IsValueType)
        {
            return false;
        }

        var returnType = method.ReturnType;
        if (!IsSupportedReturnType(returnType))
        {
            return false;
        }

        var parameters = descriptor.Parameters;
        foreach (var parameter in parameters)
        {
            if (!IsSupportedParameterType(parameter.ParameterType))
            {
                return false;
            }
        }

        // The method is invoked through an open delegate (built once, embedded as a constant) rather
        // than a direct Expression.Call. A direct call lets the JIT inline a small host method into
        // the compiled lambda, which erases the host method's frame from a thrown exception's stack
        // trace - the reflection MethodInvoker path never inlines, so bubbling CLR exceptions would
        // no longer look identical. The delegate invoke keeps that frame while staying allocation
        // free and fully typed (no argument/return boxing).
        if (!TryCreateInvocationDelegate(method, declaringType, parameters, returnType, out var invocationDelegate, out var delegateType))
        {
            return false;
        }

        var targetParam = Expression.Parameter(typeof(object), "target");
        var argsParam = Expression.Parameter(typeof(JsCallArguments), "arguments");
        var resultParam = Expression.Parameter(typeof(JsValue).MakeByRefType(), "result");
        var returnLabel = Expression.Label(typeof(bool), "return");

        var locals = new List<ParameterExpression>();
        var body = new List<Expression>();

        // result is an out parameter: seed it so the decline paths satisfy definite assignment.
        // The caller ignores it whenever we return false.
        body.Add(Expression.Assign(resultParam, Expression.Constant(JsValue.Undefined, typeof(JsValue))));

        var boundCount = method.IsStatic ? parameters.Length : parameters.Length + 1;
        var invokeArguments = new Expression[boundCount];
        var offset = 0;
        if (!method.IsStatic)
        {
            invokeArguments[0] = Expression.Convert(targetParam, declaringType);
            offset = 1;
        }

        for (var i = 0; i < parameters.Length; i++)
        {
            invokeArguments[offset + i] = BuildArgumentBinding(i, parameters[i].ParameterType, argsParam, returnLabel, locals, body);
        }

        var call = Expression.Invoke(Expression.Constant(invocationDelegate, delegateType), invokeArguments);

        body.Add(BuildResultAssignment(resultParam, call, returnType));
        body.Add(Expression.Return(returnLabel, Expression.Constant(true)));
        body.Add(Expression.Label(returnLabel, Expression.Constant(false)));

        var lambda = Expression.Lambda<Invoker>(Expression.Block(typeof(bool), locals, body), targetParam, argsParam, resultParam);
        invoker = lambda.Compile();
        return true;
    }

    /// <summary>
    /// Builds the open <see cref="Func{T}"/>/<see cref="Action"/> delegate that invokes the method:
    /// an instance method's receiver is the delegate's first parameter. Declines when the signature
    /// exceeds the available Func/Action arities or the delegate cannot be bound.
    /// </summary>
    private static bool TryCreateInvocationDelegate(
        MethodInfo method,
        Type declaringType,
        ParameterInfo[] parameters,
        Type returnType,
        [NotNullWhen(true)] out Delegate? invocationDelegate,
        [NotNullWhen(true)] out Type? delegateType)
    {
        invocationDelegate = null;
        delegateType = null;

        var isVoid = returnType == typeof(void);
        var instanceSlot = method.IsStatic ? 0 : 1;
        var typeArgs = new Type[instanceSlot + parameters.Length + (isVoid ? 0 : 1)];

        var index = 0;
        if (!method.IsStatic)
        {
            typeArgs[index++] = declaringType;
        }

        foreach (var parameter in parameters)
        {
            typeArgs[index++] = parameter.ParameterType;
        }

        if (isVoid)
        {
            if (!Expression.TryGetActionType(typeArgs, out delegateType))
            {
                return false;
            }
        }
        else
        {
            typeArgs[index] = returnType;
            if (!Expression.TryGetFuncType(typeArgs, out delegateType))
            {
                return false;
            }
        }

        try
        {
            invocationDelegate = method.CreateDelegate(delegateType);
        }
        catch (Exception)
        {
            // an accessibility/binding quirk the eligibility checks did not anticipate
            invocationDelegate = null;
            delegateType = null;
            return false;
        }

        return true;
    }

    // Exactly JsValue is a safe pass-through: every argument already is a JsValue. More specific
    // JsValue subtypes are left to the fallback so a wrong subtype keeps today's behavior instead of
    // an InvalidCastException.
    private static bool IsSupportedParameterType(Type type)
    {
        return type == typeof(int)
               || type == typeof(long)
               || type == typeof(double)
               || type == typeof(bool)
               || type == typeof(string)
               || type == typeof(JsValue);
    }

    // JsValue or any subtype flows straight back (matching FromObjectWithType); a null result is
    // mapped to JsValue.Null in BuildResultAssignment.
    private static bool IsSupportedReturnType(Type type)
    {
        return type == typeof(void)
               || type == typeof(int)
               || type == typeof(long)
               || type == typeof(double)
               || type == typeof(bool)
               || type == typeof(string)
               || typeof(JsValue).IsAssignableFrom(type);
    }

    /// <summary>
    /// Emits the type test + typed read for one argument, returning an expression of the parameter
    /// type. A non-exact match jumps to <paramref name="returnLabel"/> with <see langword="false"/>.
    /// </summary>
    private static Expression BuildArgumentBinding(
        int index,
        Type parameterType,
        ParameterExpression argsParam,
        LabelTarget returnLabel,
        List<ParameterExpression> locals,
        List<Expression> body)
    {
        var arg = Expression.Variable(typeof(JsValue), "a" + index);
        locals.Add(arg);
        body.Add(Expression.Assign(arg, Expression.ArrayIndex(argsParam, Expression.Constant(index))));

        if (parameterType == typeof(JsValue))
        {
            return arg;
        }

        if (parameterType == typeof(string))
        {
            // accept only JsString; ToString() returns the underlying string without a copy
            body.Add(DeclineIfNotType(arg, typeof(JsString), returnLabel));
            return Expression.Call(arg, _objectToString);
        }

        if (parameterType == typeof(bool))
        {
            body.Add(DeclineIfNotType(arg, typeof(JsBoolean), returnLabel));
            return Expression.Call(_asBoolean, arg);
        }

        // int / long / double
        body.Add(DeclineIfNotType(arg, typeof(JsNumber), returnLabel));

        var value = Expression.Variable(typeof(double), "d" + index);
        locals.Add(value);
        body.Add(Expression.Assign(value, Expression.Call(_asNumber, arg)));

        if (parameterType == typeof(double))
        {
            return value;
        }

        // Replicate InteropHelper.TryConvertNumberFast exactly: only convert integral values within
        // the target range, otherwise decline so the fallback reproduces today's behavior.
        var isNaN = Expression.Call(_doubleIsNaN, value);
        var isInfinity = Expression.Call(_doubleIsInfinity, value);
        var notIntegral = Expression.NotEqual(Expression.Modulo(value, Expression.Constant(1d)), Expression.Constant(0d));

        Expression belowRange;
        Expression aboveRange;
        if (parameterType == typeof(int))
        {
            belowRange = Expression.LessThan(value, Expression.Constant((double) int.MinValue));
            aboveRange = Expression.GreaterThan(value, Expression.Constant((double) int.MaxValue));
        }
        else
        {
            belowRange = Expression.LessThan(value, Expression.Constant((double) long.MinValue));
            aboveRange = Expression.GreaterThanOrEqual(value, Expression.Constant(LongMaxValueExclusiveUpperBound));
        }

        var decline = Expression.OrElse(
            Expression.OrElse(Expression.OrElse(isNaN, isInfinity), notIntegral),
            Expression.OrElse(belowRange, aboveRange));

        body.Add(Expression.IfThen(decline, Expression.Return(returnLabel, Expression.Constant(false))));

        return Expression.Convert(value, parameterType);
    }

    private static ConditionalExpression DeclineIfNotType(Expression arg, Type jsType, LabelTarget returnLabel)
    {
        return Expression.IfThen(
            Expression.Not(Expression.TypeIs(arg, jsType)),
            Expression.Return(returnLabel, Expression.Constant(false)));
    }

    private static Expression BuildResultAssignment(ParameterExpression resultParam, Expression call, Type returnType)
    {
        var nullConstant = Expression.Constant(JsValue.Null, typeof(JsValue));

        if (returnType == typeof(void))
        {
            // CLR void is exposed to JS as null (matching FromObjectWithType(engine, null, ...))
            return Expression.Block(call, Expression.Assign(resultParam, nullConstant));
        }

        if (returnType == typeof(int)
            || returnType == typeof(long)
            || returnType == typeof(double)
            || returnType == typeof(bool)
            || returnType == typeof(string))
        {
            // the public JsValue implicit operators produce the exact same JsValue as
            // DefaultObjectConverter (and handle string null -> JsValue.Null internally)
            var op = typeof(JsValue).GetMethod("op_Implicit", [returnType])!;
            return Expression.Assign(resultParam, Expression.Call(op, call));
        }

        // JsValue or a subtype: flow straight back, coalescing a null result to JsValue.Null
        return Expression.Assign(resultParam, Expression.Coalesce(Expression.Convert(call, typeof(JsValue)), nullConstant));
    }
}
#endif
