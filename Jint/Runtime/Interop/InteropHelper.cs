using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Reflection;
using Jint.Extensions;
using Jint.Native;

namespace Jint.Runtime.Interop;

internal sealed class InteropHelper
{
    /// <summary>
    /// What every public entry point that hands a CLR type to the engine promises to preserve:
    /// <see cref="Engine.SetValue{T}(string, T)"/> and <c>SetValue(string, Type)</c> on both
    /// <see cref="Engine"/> and <c>ShadowRealm</c>, <see cref="TypeReference.CreateTypeReference(Engine, Type)"/>,
    /// and <c>ModuleBuilder.ExportType</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// It is one constant because it is one promise, and it has to cover everything member resolution reads
    /// off the exposed type rather than only the members a script names.
    /// <see cref="DynamicallyAccessedMemberTypes.Interfaces"/> is the entry that is not about a named
    /// member: <c>TypeResolver</c> walks <see cref="Type.GetInterfaces"/> for a property or method the class
    /// implements only through an interface, and <c>TypeDescriptor</c> walks them to decide whether a target
    /// is array-like, dictionary-shaped or enumerable. A trimmer may remove an interface implementation
    /// nothing uses, and Jint would then answer <c>undefined</c> for a member that is there — the failure
    /// mode with no diagnostic anywhere.
    /// </para>
    /// <para>
    /// Two things about that entry are measured rather than assumed, and both are needed before anyone
    /// touches it (<see href="https://github.com/sebastienros/jint/issues/3479">#3479</see>). Removing it
    /// changes <b>no</b> answer a published native binary gives, because ILC keeps the interface
    /// implementations of a type whose MethodTable it emits whenever the interface is used anywhere in the
    /// closed program — so no probe can be written that discriminates it at run time. What removing it does
    /// change is the analysis: the published inventory goes 67 to 75, all in the embedder's build. And it
    /// does not stretch to a member behind an <em>explicit</em> implementation, because it asks for the
    /// interfaces and not for their members, so <c>iface.GetProperties()</c> has nothing to read — an
    /// unrooted host type's explicit member reads <c>undefined</c> with this constant exactly as it stands,
    /// which <c>Jint.AotExample</c> pins as a <c>KnownTrimmedAway</c>. Neither fact is a reason to widen it;
    /// the second is the embedder's to close by rooting their own assembly.
    /// </para>
    /// <para>
    /// <see cref="DynamicallyAccessedMemberTypes.PublicNestedTypes"/> is deliberately <b>not</b> here, and
    /// that is a measured decision rather than an oversight. Member resolution really does call
    /// <see cref="Type.GetNestedType(string, System.Reflection.BindingFlags)"/> — a nested type is a member a
    /// script can name — so the requirement is stated where it is read, on
    /// <c>TypeResolver.TryFindMemberAccessor</c>, and the two diagnostics it raises on the way to
    /// <see cref="TypeReference.ReferenceType"/> are left standing. Carrying it here instead costs the
    /// embedder: the attribute marks each nested type with <c>All</c>, so every public nested <b>enum</b> of
    /// every exposed type produces an <c>IL3050</c> in the host's own build through the inherited
    /// <c>[RequiresDynamicCode]</c> <c>Enum.GetValues(Type)</c> — two of them for a single
    /// <c>SetValue("Env", typeof(Environment))</c>, measured on a published native binary. It buys nothing
    /// there in exchange: ILC keeps the nested types of a type whose metadata it emits either way, which the
    /// same measurement confirmed for six candidates.
    /// </para>
    /// <para>
    /// Widening this constant is a real cost to every AOT consumer, including the ones that never reach the
    /// member, so add to it only for something resolution genuinely reads <em>and</em> a trimmer genuinely
    /// removes.
    /// </para>
    /// </remarks>
    internal const DynamicallyAccessedMemberTypes DefaultDynamicallyAccessedMemberTypes = DynamicallyAccessedMemberTypes.PublicConstructors
                                                                                          | DynamicallyAccessedMemberTypes.PublicProperties
                                                                                          | DynamicallyAccessedMemberTypes.PublicMethods
                                                                                          | DynamicallyAccessedMemberTypes.PublicFields
                                                                                          | DynamicallyAccessedMemberTypes.PublicEvents
                                                                                          | DynamicallyAccessedMemberTypes.Interfaces;

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
    /// <remarks>
    /// <para>
    /// The two parameters are annotated asymmetrically on purpose, and the asymmetry is what each one
    /// actually reads. <paramref name="givenType"/> is asked for its interfaces, so it declares
    /// <see cref="DynamicallyAccessedMemberTypes.Interfaces"/> and nothing else. <paramref name="genericType"/>
    /// is only ever compared - <c>IsConstructedGenericType</c>, <c>IsGenericType</c>,
    /// <c>GetGenericTypeDefinition</c> - none of which reads a member, so it declares nothing at all. The
    /// pair used to demand every public member of both, which no caller could satisfy: the types arrive as
    /// a parameter's type or a value's <c>GetType()</c>, so the excess propagated outwards as a trim
    /// diagnostic at every call site rather than preserving anything.
    /// </para>
    /// <para>
    /// The base-type walk is a loop rather than the recursion it used to be for the same reason.
    /// <see cref="Type.GetInterfaces"/> already returns the transitive closure, so a base type's interfaces
    /// were re-scanned having already been scanned here, and the recursive call demanded
    /// <c>Interfaces</c> of a <see cref="Type.BaseType"/> that cannot carry it. What is left in the loop -
    /// the constructed-generic-definition test - reads no members, so it needs no annotation. Same answer,
    /// including which type a miss reports (the last one in the chain).
    /// </para>
    /// </remarks>
    internal static AssignableResult IsAssignableToGenericType(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)]
        Type givenType,
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

        var current = givenType;
        while (true)
        {
            if (current.IsGenericType && current.GetGenericTypeDefinition() == genericType)
            {
                return new AssignableResult(0, current);
            }

            var baseType = current.BaseType;
            if (baseType is null)
            {
                return new AssignableResult(-1, current);
            }

            current = baseType;
        }
    }

    /// <summary>
    /// Determines how well parameter type matches target method's type.
    /// </summary>
    private static int CalculateMethodParameterScore(Engine engine, ParameterInfo parameter, JsValue parameterValue)
    {
        var paramType = parameter.ParameterType;

        // Special case: if parameter expects a JsValue-derived type (e.g., TypeReference),
        // check if the argument is of that exact type before calling ToObject().
        // This is important because ToObject() unwraps TypeReference to System.Type,
        // losing the original wrapper type information needed for overload resolution.
        if (typeof(JsValue).IsAssignableFrom(paramType))
        {
            var jsValueType = parameterValue.GetType();
            if (jsValueType == paramType)
            {
                return 0; // Exact match
            }

            if (paramType.IsAssignableFrom(jsValueType))
            {
                return 1; // Is-a relationship
            }
        }

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

        if (parameterValue.IsSpecArray() && (paramType.IsArray || IsGenericCollectionType(paramType)))
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

        // Nothing above recognizes the pair, and that is not the same as the conversion being impossible.
        // The installed converter still reaches a JS function -> delegate, a JS object -> POCO or target
        // dictionary, a string -> enum, a JS array -> T[]/List<T>, and a cast operator declared on the
        // *target* type - none of which any rule above can see, which is what "rarely succeeds" was for.
        // Only the converter can tell the two apart, and it has to, because FindBestMatch discards a
        // negative score and accepts every other: a hopeless candidate scored 100 is the best match
        // whenever it is the only one. Ask the very converter MethodDescriptor.Call would use, so the
        // score cannot claim a conversion the call then fails to perform.
        //
        // A parameter type still carrying open type parameters (T, Func<T, bool>) is the one case that
        // cannot be asked: the type the argument will actually be converted to does not exist yet -
        // MethodInfoFunction.ResolveMethod closes the method first - and handing an open type to the
        // converter is an ArgumentException, not an answer. Those keep the undecided score.
        if (paramType.ContainsGenericParameters
            || engine._typeConverter.TryConvert(objectValue, paramType, CultureInfo.InvariantCulture, out _))
        {
            // will rarely succeed
            return 100;
        }

        // this is bad
        return -1;
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

    internal static readonly HashSet<Type> GenericCollectionTypeDefinitions =
    [
        typeof(List<>),
        typeof(IList<>),
        typeof(ICollection<>),
        typeof(IEnumerable<>),
        typeof(IReadOnlyList<>),
        typeof(IReadOnlyCollection<>),
        typeof(System.Collections.ObjectModel.Collection<>),
    ];

    internal static bool IsGenericCollectionType(Type type)
    {
        return type.IsGenericType
               && type.GetGenericArguments().Length == 1
               && GenericCollectionTypeDefinitions.Contains(type.GetGenericTypeDefinition());
    }

    internal static bool TypeIsNullable(Type type)
    {
        return !type.IsValueType || Nullable.GetUnderlyingType(type) != null;
    }

    // (double) long.MaxValue rounds up to 2^63 which overflows long, the bound must be exclusive
    private const double LongMaxValueExclusiveUpperBound = 9223372036854775808d;

    /// <summary>
    /// Fast conversion for the dominant numeric argument shapes, avoiding the generic
    /// converter and Convert.ChangeType. Only converts when the result is bit-exact with
    /// the general conversion path; otherwise declines and the caller falls back.
    /// </summary>
    internal static bool TryConvertNumberFast(double value, Type parameterType, out object? converted)
    {
        if (parameterType == typeof(double))
        {
            converted = value;
            return true;
        }

        if (TypeConverter.IsIntegralNumber(value))
        {
            if (parameterType == typeof(int))
            {
                if (value is >= int.MinValue and <= int.MaxValue)
                {
                    converted = (int) value;
                    return true;
                }
            }
            else if (parameterType == typeof(long))
            {
                if (value is >= long.MinValue and < LongMaxValueExclusiveUpperBound)
                {
                    converted = (long) value;
                    return true;
                }
            }
        }

        converted = null;
        return false;
    }


    internal readonly record struct MethodMatch(MethodDescriptor Method, JsCallArguments Arguments, int Score = 0) : IComparable<MethodMatch>
    {
        public int CompareTo(MethodMatch other) => Score.CompareTo(other.Score);
    }

    /// <summary>
    /// Match candidates in preference order: either a single perfect match or a score-sorted
    /// candidate list. A struct with a struct enumerator so the common single-match case does
    /// not allocate; only the ambiguous multi-candidate case carries a list.
    /// </summary>
    internal readonly struct MethodMatches
    {
        private readonly MethodMatch _single;
        private readonly List<MethodMatch>? _list;
        private readonly bool _hasSingle;

        private MethodMatches(in MethodMatch single, List<MethodMatch>? list, bool hasSingle)
        {
            _single = single;
            _list = list;
            _hasSingle = hasSingle;
        }

        public static MethodMatches Empty => default;

        public static MethodMatches Single(in MethodMatch match) => new(in match, list: null, hasSingle: true);

        public static MethodMatches Sorted(List<MethodMatch> list) => new(default, list, hasSingle: false);

        public MethodMatch FirstOrDefault()
        {
            if (_hasSingle)
            {
                return _single;
            }

            return _list is { Count: > 0 } ? _list[0] : default;
        }

        public Enumerator GetEnumerator() => new(in this);

        internal struct Enumerator
        {
            private readonly MethodMatches _matches;
            private int _index;

            internal Enumerator(in MethodMatches matches)
            {
                _matches = matches;
                _index = -1;
            }

            public readonly MethodMatch Current => _matches._list is not null ? _matches._list[_index] : _matches._single;

            public bool MoveNext()
            {
                var count = _matches._list?.Count ?? (_matches._hasSingle ? 1 : 0);
                return ++_index < count;
            }
        }
    }

    internal static MethodMatches FindBestMatch<TState>(
        Engine engine,
        MethodDescriptor[] methods,
        Func<MethodDescriptor, TState, JsValue[]> argumentProvider,
        TState state)
    {
        List<MethodMatch>? matchingByParameterCount = null;
        foreach (var method in methods)
        {
            var parameterInfos = method.Parameters;
            var arguments = argumentProvider(method, state);
            if (arguments.Length <= parameterInfos.Length
                && arguments.Length >= parameterInfos.Length - method.ParameterDefaultValuesCount)
            {
                var score = CalculateMethodScore(engine, method, arguments);
                if (score == 0)
                {
                    // perfect match, earlier non-perfect candidates are discarded
                    return MethodMatches.Single(new MethodMatch(method, arguments));
                }

                if (score < 0)
                {
                    // discard
                    continue;
                }

                matchingByParameterCount ??= [];
                matchingByParameterCount.Add(new MethodMatch(method, arguments, score));
            }
        }

        if (matchingByParameterCount == null)
        {
            return MethodMatches.Empty;
        }

        if (matchingByParameterCount.Count > 1)
        {
            matchingByParameterCount.Sort();
        }

        return MethodMatches.Sorted(matchingByParameterCount);
    }
}
