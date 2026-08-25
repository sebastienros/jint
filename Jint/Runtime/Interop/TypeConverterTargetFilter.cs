using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

namespace Jint.Runtime.Interop;

/// <summary>
/// Answers "would this engine's <see cref="ClrTypeConverter"/> be consulted for a conversion to this target
/// type, and could it answer differently from <see cref="DefaultTypeConverter"/>?" for the interop fast lanes,
/// which have to decline whenever the answer is yes: those lanes bind an argument, write a member or bake an
/// indexer key themselves, reproducing what the stock conversion would have produced, so a converter that
/// would have produced something else must be given the chance to.
/// <para>
/// The engine keeps no filter at all for the stock converter (see <see cref="Create"/>), so an ordinary engine
/// pays one null check per lane and nothing else.
/// </para>
/// <para>
/// The question is <b>exact</b>, which is the whole difference from <see cref="ObjectConverterTypeFilter"/>.
/// An object converter is handed a <em>value</em>, so that filter has to guess which runtime types a member's
/// declared type could hold: it claims in both directions, treats <see cref="object"/> as claiming everything,
/// and needs the sealed/interface reasoning to rule anything out. A type converter is handed the
/// <em>target</em> <see cref="Type"/>, and every call site passes a statically known one — the member type, the
/// parameter type, the indexer's index type. So the predicate here is one <c>IsAssignableFrom</c> in one
/// direction: a declaration claims itself and its subtypes and nothing above them.
/// </para>
/// </summary>
internal sealed class TypeConverterTargetFilter
{
    /// <summary>
    /// Filter for a converter that declared nothing: it may answer any target type differently, which is the
    /// behaviour every host-installed converter had before declared types existed.
    /// </summary>
    private static readonly TypeConverterTargetFilter _unrestricted = new(declaredTypes: null);

    /// <summary><see langword="null"/> means "claims every target type", see <see cref="_unrestricted"/>.</summary>
    private readonly Type[]? _declaredTypes;

    private readonly ConcurrentDictionary<Type, bool>? _claims;

    /// <inheritdoc cref="ObjectConverterTypeFilter" path="/summary/para[2]"/>
    private Func<Type, bool>? _computeClaims;

    private TypeConverterTargetFilter(Type[]? declaredTypes)
    {
        _declaredTypes = declaredTypes;
        _claims = declaredTypes is not null ? new ConcurrentDictionary<Type, bool>() : null;
    }

    /// <summary>
    /// Whether this filter narrows anything, and therefore whether there is a promise for host-contract
    /// verification to check.
    /// </summary>
    internal bool IsRestricted => _declaredTypes is not null;

    /// <summary>
    /// The effective declaration — the converter's own <see cref="ClrTypeConverter.HandledTargetTypes"/> unioned
    /// with what the registration declared — rendered for a diagnostic. Empty when nothing was declared.
    /// </summary>
    internal string DeclaredTypesForDiagnostics => _declaredTypes is null or { Length: 0 }
        ? "(none)"
        : string.Join(", ", Array.ConvertAll(_declaredTypes, static t => t.ToString()));

    /// <summary>
    /// The filter for one engine's installed converter, or <see langword="null"/> when the converter is the
    /// stock one and no lane ever has to consider it.
    /// </summary>
    /// <param name="converter">The converter about to be installed.</param>
    /// <param name="declaredAtRegistration">
    /// What <c>SetTypeConverter</c>'s typed overload declared, or <see langword="null"/>. Unioned with the
    /// converter's own <see cref="ClrTypeConverter.HandledTargetTypes"/>: a registration may widen a
    /// converter's declaration, never narrow it, because narrowing would silently bypass the converter on
    /// exactly the types it knows it handles.
    /// </param>
    internal static TypeConverterTargetFilter? Create(ClrTypeConverter converter, Type[]? declaredAtRegistration)
    {
        // The stock converter is what every lane reproduces, so it claims nothing. This is an exact type test
        // on purpose and it is the only one left: a *subclass* of DefaultTypeConverter overrides some
        // conversion or it would not exist, and which ones is exactly what the declaration is for.
        if (converter.GetType() == typeof(DefaultTypeConverter))
        {
            return null;
        }

        var declaredByConverter = converter.HandledTargetTypes;
        if (declaredByConverter is null && declaredAtRegistration is null)
        {
            return _unrestricted;
        }

        var declaredTypes = new List<Type>();
        AddDistinct(declaredTypes, declaredByConverter);
        AddDistinct(declaredTypes, declaredAtRegistration);
        return new TypeConverterTargetFilter(declaredTypes.ToArray());

        static void AddDistinct(List<Type> target, Type[]? source)
        {
            if (source is null)
            {
                return;
            }

            foreach (var type in source)
            {
                if (type is not null && !target.Contains(type))
                {
                    target.Add(type);
                }
            }
        }
    }

    /// <summary>
    /// Whether a conversion to <paramref name="targetType"/> could reach the converter with an answer of its
    /// own. Conservative: an unknown target type is claimed.
    /// </summary>
    internal bool Claims(Type? targetType)
    {
        if (_declaredTypes is null || targetType is null)
        {
            return true;
        }

        return _claims!.GetOrAdd(targetType, _computeClaims ??= type => ComputeClaims(_declaredTypes!, type));
    }

    private static bool ComputeClaims(Type[] declaredTypes, Type targetType)
    {
        // a boxed Nullable<T> is a boxed T, and a member typed T? is converted by asking for T? — either
        // spelling of the same conversion has to reach a converter that declared T
        var underlyingType = Nullable.GetUnderlyingType(targetType);

        foreach (var declaredType in declaredTypes)
        {
            if (Claims(declaredType, targetType)
                || (underlyingType is not null && Claims(declaredType, underlyingType)))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Whether a conversion asking for <paramref name="targetType"/> is one the converter declared by naming
    /// <paramref name="declaredType"/>. One direction only, unlike
    /// <see cref="ObjectConverterTypeFilter"/>: the target type is the exact argument the converter receives,
    /// not a stand-in for a runtime value, so declaring a base type or an interface covers the family under it
    /// and nothing above it. A converter whose <c>switch</c> also answers for target types <em>above</em> what
    /// it declared has misdeclared itself, which is what verification reports.
    /// </summary>
    private static bool Claims(Type declaredType, Type targetType)
    {
        // an open generic (typeof(List<>)) is not comparable through IsAssignableFrom at all
        if (declaredType.ContainsGenericParameters)
        {
            return true;
        }

        return declaredType.IsAssignableFrom(targetType);
    }
}

/// <summary>
/// Checks a declared <see cref="ClrTypeConverter"/> against what it actually produces, by running the stock
/// conversion beside it for every target type the declaration excluded and comparing the two. Installed in
/// place of the host's converter only when host-contract verification is on, and unwrapped by
/// <c>Engine.TypeConverter</c>, so a host observes its own instance whether or not it is verifying.
/// </summary>
/// <remarks>
/// A declaration is a promise about the whole converter, and nothing links it to the <c>switch</c> inside
/// <see cref="ClrTypeConverter.TryConvert"/>: a case added there and not added to the declaration is silently
/// skipped on exactly the members, parameters and indexer keys the declaration excluded, and honoured
/// everywhere else. That inconsistency is invisible from script, so it is worth a check when a host asks for
/// one — and the check can be exact here, because the promise is exact: for an undeclared target type the
/// converter must produce what the stock one produces.
/// </remarks>
internal sealed class VerifyingClrTypeConverter : ClrTypeConverter
{
    private readonly ClrTypeConverter _converter;
    private readonly TypeConverterTargetFilter _declared;
    private readonly DefaultTypeConverter _stock;

    internal VerifyingClrTypeConverter(ClrTypeConverter converter, TypeConverterTargetFilter declared, DefaultTypeConverter stock)
    {
        _converter = converter;
        _declared = declared;
        _stock = stock;
    }

    /// <summary>The host's own converter, which is what <c>Engine.TypeConverter</c> hands back.</summary>
    internal ClrTypeConverter Inner => _converter;

    protected internal override Type[]? HandledTargetTypes => _converter.HandledTargetTypes;

    public override object? Convert(
        object? value,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicFields)] Type type,
        IFormatProvider formatProvider)
    {
        var converted = _converter.Convert(value, type, formatProvider);

        if (!_declared.Claims(type) && TryStockConvert(value, type, formatProvider, out var stockConverted, out var stockResult))
        {
            // Convert() has already succeeded, so the stock converter refusing the same conversion is itself
            // the disagreement - a conversion the declaration said would not happen
            if (!stockConverted || Differs(converted, stockResult))
            {
                Fail(type, converted: true, converted, stockConverted, stockResult);
            }
        }

        return converted;
    }

    public override bool TryConvert(
        object? value,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicFields)] Type type,
        IFormatProvider formatProvider,
        [NotNullWhen(true)] out object? converted)
    {
        var handled = _converter.TryConvert(value, type, formatProvider, out converted);

        if (!_declared.Claims(type) && TryStockConvert(value, type, formatProvider, out var stockConverted, out var stockResult))
        {
            if (stockConverted != handled || Differs(converted, stockResult))
            {
                Fail(type, handled, converted, stockConverted, stockResult);
            }
        }

        return handled;
    }

    /// <summary>
    /// Runs the stock conversion beside the host's. A stock conversion that throws leaves nothing to compare
    /// against - the host converter is answering a question the stock one cannot - so it reports no answer at
    /// all rather than a disagreement.
    /// </summary>
    private bool TryStockConvert(
        object? value,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicFields)] Type type,
        IFormatProvider formatProvider,
        out bool stockConverted,
        out object? stockResult)
    {
        try
        {
            stockConverted = _stock.TryConvert(value, type, formatProvider, out stockResult);
            return true;
        }
        catch (Exception)
        {
            stockConverted = false;
            stockResult = null;
            return false;
        }
    }

    /// <summary>
    /// Whether two conversion results provably disagree. A value type or a <see cref="string"/> is compared by
    /// value; anything else only by runtime type, because the stock conversion of a function to a delegate (or
    /// of an object to a wrapper) produces a fresh instance every time and comparing those would report a
    /// difference that is not one.
    /// </summary>
    private static bool Differs(object? result, object? stockResult)
    {
        if (ReferenceEquals(result, stockResult))
        {
            return false;
        }

        if (result is null || stockResult is null)
        {
            return true;
        }

        var stockType = stockResult.GetType();
        if (result.GetType() != stockType)
        {
            return true;
        }

        return (stockType.IsValueType || stockType == typeof(string)) && !stockResult.Equals(result);
    }

    [DoesNotReturn]
    private void Fail(Type type, bool converted, object? result, bool stockConverted, object? stockResult)
    {
        HostContractVerification.Fail(
            $"{_converter.GetType()} converted to target type {type} differently from DefaultTypeConverter " +
            $"(it returned {Describe(converted, result)}, the stock converter returns {Describe(stockConverted, stockResult)}), " +
            $"but {type} is not among the target types it was registered as handling ({_declared.DeclaredTypesForDiagnostics}). That declaration promised the engine it may keep the compiled interop fast lanes for members, parameters and indexer keys typed {type}, so this converter is silently bypassed on exactly those — the declaration and the converter's own switch have drifted. Declare {type} at the SetTypeConverter call, or stop converting to it.");

        static string Describe(bool converted, object? result) => converted ? result?.ToString() ?? "null" : "no conversion";
    }
}
