using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using Jint.Extensions;
using Jint.Native;

namespace Jint.Runtime.Interop;

#pragma warning disable IL2072

internal sealed class InteropHelper
{
    internal const DynamicallyAccessedMemberTypes DefaultDynamicallyAccessedMemberTypes = DynamicallyAccessedMemberTypes.PublicConstructors
                                                                                          | DynamicallyAccessedMemberTypes.PublicProperties
                                                                                          | DynamicallyAccessedMemberTypes.PublicMethods
                                                                                          | DynamicallyAccessedMemberTypes.PublicFields
                                                                                          | DynamicallyAccessedMemberTypes.PublicEvents;

    internal readonly record struct AssignableResult(int Score, Type MatchingGivenType)
    {
        public bool IsAssignable => Score >= 0;
    }

    /// <summary>
    /// resources:
    /// https://docs.microsoft.com/en-us/dotnet/framework/reflection-and-codedom/how-to-examine-and-instantiate-generic-types-with-reflection
    /// https://stackoverflow.com/questions/74616/how-to-detect-if-type-is-another-generic-type/1075059#1075059
    /// https://docs.microsoft.com/en-us/dotnet/api/system.type.isconstructedgenerictype?view=net-6.0
    /// This can be improved upon - specifically as mentioned in the above MS document:
    /// GetGenericParameterConstraints()
    /// and array handling - i.e.
    /// GetElementType()
    /// </summary>
    internal static AssignableResult IsAssignableToGenericType(
        [DynamicallyAccessedMembers(DefaultDynamicallyAccessedMemberTypes | DynamicallyAccessedMemberTypes.Interfaces)]
        Type givenType,
        [DynamicallyAccessedMembers(DefaultDynamicallyAccessedMemberTypes | DynamicallyAccessedMemberTypes.Interfaces)]
        Type genericType)
    {
        if (givenType is null)
        {
            return new AssignableResult(-1, typeof(void));
        }

        if (!genericType.IsConstructedGenericType)
        {
            // as mentioned here:
            // https://docs.microsoft.com/en-us/dotnet/api/system.type.isconstructedgenerictype?view=net-6.0
            // this effectively means this generic type is open (i.e. not closed) - so any type is "possible" - without looking at the code in the method we don't know
            // whether any operations are being applied that "don't work"
            return new AssignableResult(2, givenType);
        }

        var interfaceTypes = givenType.GetInterfaces();
        foreach (var it in interfaceTypes)
        {
            if (it.IsGenericType)
            {
                var givenTypeGenericDef = it.GetGenericTypeDefinition();
                if (givenTypeGenericDef == genericType)
                {
                    return new AssignableResult(0, it);
                }
                else if (genericType.IsGenericType && (givenTypeGenericDef == genericType.GetGenericTypeDefinition()))
                {
                    return new AssignableResult(0, it);
                }
                // TPC: we could also add a loop to recurse and iterate thru the iterfaces of generic type - because of covariance/contravariance
            }
        }

        if (givenType.IsGenericType && givenType.GetGenericTypeDefinition() == genericType)
        {
            return new AssignableResult(0, givenType);
        }

        var baseType = givenType.BaseType;
        if (baseType == null)
        {
            return new AssignableResult(-1, givenType);
        }

        return IsAssignableToGenericType(baseType, genericType);
    }

    /// <summary>
    /// Determines how well parameter type matches target method's type.
    /// </summary>
    private static int CalculateMethodParameterScore(Engine engine, ParameterInfo parameter, JsValue parameterValue)
    {
        var paramType = parameter.ParameterType;
        var objectValue = parameterValue.ToObject();
        var objectValueType = objectValue?.GetType();

        if (objectValueType == paramType)
        {
            return 0;
        }

        if (objectValue is null)
        {
            if (!parameter.IsOptional && !TypeIsNullable(paramType))
            {
                // this is bad
                return -1;
            }

            return 0;
        }

        if (paramType == typeof(JsValue))
        {
            // JsValue is convertible to. But it is still not a perfect match
            return 1;
        }

        if (paramType == typeof(object))
        {
            // a catch-all, prefer others over it
            return 5;
        }

        const int ScoreForDifferentTypeButFittingNumberRange = 2;
        if (parameterValue.IsNumber())
        {
            var num = (JsNumber) parameterValue;
            var numValue = num._value;

            if (paramType == typeof(double))
            {
                return 0;
            }

            if (paramType == typeof(float) && numValue is <= float.MaxValue and >= float.MinValue)
            {
                return ScoreForDifferentTypeButFittingNumberRange;
            }

            var isInteger = num.IsInteger() || TypeConverter.IsIntegralNumber(num._value);

            // if value is integral number and within allowed range for the parameter type, we consider this perfect match
            if (isInteger)
            {
                if (paramType == typeof(int))
                {
                    return 0;
                }

                if (paramType == typeof(long))
                {
                    return ScoreForDifferentTypeButFittingNumberRange;
                }

                // check if we can narrow without exception throwing versions (CanChangeType)
                var integerValue = (int) num._value;
                if (paramType == typeof(short) && integerValue is <= short.MaxValue and >= short.MinValue)
                {
                    return ScoreForDifferentTypeButFittingNumberRange;
                }

                if (paramType == typeof(ushort) && integerValue is <= ushort.MaxValue and >= ushort.MinValue)
                {
                    return ScoreForDifferentTypeButFittingNumberRange;
                }

                if (paramType == typeof(byte) && integerValue is <= byte.MaxValue and >= byte.MinValue)
                {
                    return ScoreForDifferentTypeButFittingNumberRange;
                }

                if (paramType == typeof(sbyte) && integerValue is <= sbyte.MaxValue and >= sbyte.MinValue)
                {
                    return ScoreForDifferentTypeButFittingNumberRange;
                }
            }
        }

        if (paramType.IsEnum &&
            parameterValue is JsNumber jsNumber
            && jsNumber.IsInteger()
            && paramType.GetEnumUnderlyingType() == typeof(int)
            && Enum.IsDefined(paramType, jsNumber.AsInteger()))
        {
            // we can do conversion from int value to enum
            return 0;
        }

        if (paramType.IsAssignableFrom(objectValueType))
        {
            // is-a-relation
            return 1;
        }

        if (parameterValue.IsArray() && paramType.IsArray)
        {
            // we have potential, TODO if we'd know JS array's internal type we could have exact match
            return 2;
        }

        // not sure the best point to start generic type tests
        if (paramType.IsGenericParameter)
        {
            var genericTypeAssignmentScore = IsAssignableToGenericType(objectValueType!, paramType);
            if (genericTypeAssignmentScore.Score != -1)
            {
                return genericTypeAssignmentScore.Score;
            }
        }

        if (CanChangeType(objectValue, paramType))
        {
            // forcing conversion isn't ideal, but works, especially for int -> double for example
            return 3;
        }

        foreach (var m in objectValueType!.GetOperatorOverloadMethods())
        {
            if (paramType.IsAssignableFrom(m.ReturnType) && m.Name is "op_Implicit" or "op_Explicit")
            {
                // implicit/explicit operator conversion is OK, but not ideal
                return 3;
            }
        }

        if (ReflectionExtensions.TryConvertViaTypeCoercion(paramType, engine.Options.Interop.ValueCoercion, parameterValue, out _))
        {
            // gray JS zone where we start to do odd things
            return 10;
        }

        // will rarely succeed
        return 100;
    }

    /// <summary>
    /// Method's match score tells how far away it's from ideal candidate. 0 = ideal, bigger the the number,
    /// the farther away the candidate is from ideal match. Negative signals impossible match.
    /// </summary>
    private static int CalculateMethodScore(Engine engine, MethodDescriptor method, JsCallArguments arguments)
    {
        if (method.Parameters.Length == 0 && arguments.Length == 0)
        {
            // perfect
            return 0;
        }

        var score = 0;
        for (var i = 0; i < arguments.Length; i++)
        {
            var jsValue = arguments[i];

            var parameterScore = CalculateMethodParameterScore(engine, method.Parameters[i], jsValue);
            if (parameterScore < 0)
            {
                return parameterScore;
            }

            score += parameterScore;
        }

        return score;
    }

    private static bool CanChangeType(object value, Type targetType)
    {
        if (value is null && !targetType.IsValueType)
        {
            return true;
        }

        if (value is not IConvertible)
        {
            return false;
        }

        try
        {
            Convert.ChangeType(value, targetType, CultureInfo.InvariantCulture);
            return true;
        }
        catch
        {
            // nope
            return false;
        }
    }

    internal static bool TypeIsNullable(Type type)
    {
        return !type.IsValueType || Nullable.GetUnderlyingType(type) != null;
    }


    internal readonly record struct MethodMatch(MethodDescriptor Method, JsCallArguments Arguments, int Score = 0) : IComparable<MethodMatch>
    {
        public int CompareTo(MethodMatch other) => Score.CompareTo(other.Score);
    }

    internal static IEnumerable<MethodMatch> FindBestMatch(
        Engine engine,
        MethodDescriptor[] methods,
        Func<MethodDescriptor, JsValue[]> argumentProvider)
    {
        List<MethodMatch>? matchingByParameterCount = null;
        foreach (var method in methods)
        {
            var parameterInfos = method.Parameters;
            var arguments = argumentProvider(method);
            if (arguments.Length <= parameterInfos.Length
                && arguments.Length >= parameterInfos.Length - method.ParameterDefaultValuesCount)
            {
                var score = CalculateMethodScore(engine, method, arguments);
                if (score == 0)
                {
                    // perfect match
                    yield return new MethodMatch(method, arguments);
                    yield break;
                }

                if (score < 0)
                {
                    // discard
                    continue;
                }

                matchingByParameterCount ??= new List<MethodMatch>();
                matchingByParameterCount.Add(new MethodMatch(method, arguments, score));
            }
        }

        if (matchingByParameterCount == null)
        {
            yield break;
        }

        if (matchingByParameterCount.Count > 1)
        {
            matchingByParameterCount.Sort();
        }

        foreach (var match in matchingByParameterCount)
        {
            yield return match;
        }
    }
}
