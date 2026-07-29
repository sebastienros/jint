using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using Jint.Native;

namespace Jint.Runtime.Interop;

/// <summary>
/// Answers "could any of this engine's registered <see cref="IObjectConverter"/>s be handed a value read
/// from a member — or returned by a method — of this declared type?" for the interop fast lanes, which have
/// to decline whenever the answer is yes: on the paths those lanes short-circuit, a converter is otherwise
/// offered every CLR value on its way to a <see cref="JsValue"/>, and the lanes produce the
/// <see cref="JsValue"/> themselves.
/// (That is a property of those paths, not a global invariant — the array-like element lane in
/// <c>ObjectWrapper.Specialized</c> turns common primitive item types into <see cref="JsValue"/>s without
/// consulting any converter.)
/// <para>
/// A converter registered without declaring the CLR types it handles can be handed anything, so a single
/// such converter makes the filter claim every member — which is exactly the behaviour every registration
/// had before declared types existed. Only when <b>every</b> registered converter declared its types can a
/// member whose declared type none of them claims keep the fast lane.
/// </para>
/// <para>
/// The per-type answer is memoized: the set of member types an engine touches is small and fixed, so the
/// steady-state cost is one dictionary probe instead of the assignability walk.
/// </para>
/// </summary>
internal sealed class ObjectConverterTypeFilter
{
    /// <summary>
    /// Filter for a converter set containing at least one converter that did not declare its types.
    /// </summary>
    private static readonly ObjectConverterTypeFilter _unrestricted = new(declaredTypes: null);

    /// <summary><see langword="null"/> means "claims everything", see <see cref="_unrestricted"/>.</summary>
    private readonly Type[]? _declaredTypes;

    private readonly ConcurrentDictionary<Type, bool>? _claims;

    /// <summary>
    /// Memoized <see cref="ConcurrentDictionary{TKey,TValue}.GetOrAdd(TKey, Func{TKey, TValue})"/> factory.
    /// It cannot be <c>static</c> — the answer depends on <see cref="_declaredTypes"/>, which lives on this
    /// instance — and the state-carrying <c>GetOrAdd</c> overload that would let it be does not exist on
    /// every target framework, so the delegate is built once per filter instead of once per miss.
    /// </summary>
    private Func<Type, bool>? _computeClaims;

    private ObjectConverterTypeFilter(Type[]? declaredTypes)
    {
        _declaredTypes = declaredTypes;
        _claims = declaredTypes is not null ? new ConcurrentDictionary<Type, bool>() : null;
    }

    /// <summary>
    /// The filter for one converter's own declaration, used to check that what the converter actually converts
    /// stays inside what it declared. It has to be this type rather than a bespoke assignability test, so the
    /// promise a registration makes and the check that it was kept can never drift apart.
    /// </summary>
    internal static ObjectConverterTypeFilter ForDeclaredTypes(Type[] declaredTypes) => new(declaredTypes);

    /// <summary>
    /// Builds the filter for an engine's converter set, or <see langword="null"/> when no converters are
    /// registered at all (in which case no fast lane ever has to consider them).
    /// </summary>
    internal static ObjectConverterTypeFilter? Create(IObjectConverter[]? converters)
    {
        if (converters is null || converters.Length == 0)
        {
            return null;
        }

        List<Type>? declaredTypes = null;
        foreach (var converter in converters)
        {
            if (converter is not TypedObjectConverter typed)
            {
                return _unrestricted;
            }

            declaredTypes ??= new List<Type>();
            foreach (var handledType in typed.HandledTypes)
            {
                if (!declaredTypes.Contains(handledType))
                {
                    declaredTypes.Add(handledType);
                }
            }
        }

        return new ObjectConverterTypeFilter(declaredTypes!.ToArray());
    }

    /// <summary>
    /// Whether a value read from a member declared as <paramref name="memberType"/> could reach one of the
    /// converters. Conservative: an unknown declared type is claimed.
    /// </summary>
    internal bool Claims(Type? memberType)
    {
        if (_declaredTypes is null || memberType is null)
        {
            return true;
        }

        return _claims!.GetOrAdd(memberType, _computeClaims ??= type => ComputeClaims(_declaredTypes!, type));
    }

    private static bool ComputeClaims(Type[] declaredTypes, Type memberType)
    {
        // a member typed as object can hold a value of any type at all
        if (memberType == typeof(object))
        {
            return true;
        }

        // a boxed Nullable<T> is a boxed T, so the converter sees the underlying type
        var underlyingType = Nullable.GetUnderlyingType(memberType);

        foreach (var declaredType in declaredTypes)
        {
            if (Claims(declaredType, memberType)
                || (underlyingType is not null && Claims(declaredType, underlyingType)))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Whether some runtime type assignable to <paramref name="memberType"/> is also a
    /// <paramref name="declaredType"/>. Errs towards <see langword="true"/> — a wrong <c>true</c> only
    /// forgoes a fast lane, a wrong <c>false</c> would silently bypass a converter.
    /// </summary>
    private static bool Claims(Type declaredType, Type memberType)
    {
        // an open generic (typeof(List<>)) is not comparable through IsAssignableFrom at all
        if (declaredType.ContainsGenericParameters)
        {
            return true;
        }

        // every value of the member's type is a declaredType (covers a member typed as a concrete enum
        // against a converter declaring System.Enum, and an implementer against a declared interface)
        if (declaredType.IsAssignableFrom(memberType))
        {
            return true;
        }

        // the member could hold a declaredType (or one of its subtypes)
        if (memberType.IsAssignableFrom(declaredType))
        {
            return true;
        }

        // neither is a view of the other, so only a type deriving from both could be claimed.
        // A value type or sealed type has no such subtype (IsSealed also covers value types).
        if (memberType.IsSealed || declaredType.IsSealed)
        {
            return false;
        }

        // an interface on either side can be mixed into a subtype of the other
        if (memberType.IsInterface || declaredType.IsInterface)
        {
            return true;
        }

        // two unrelated non-sealed classes: single inheritance means no type can be both
        return false;
    }
}

/// <summary>
/// An <see cref="IObjectConverter"/> registered together with the CLR types it handles. The declaration is
/// carried by this wrapper rather than by an interface member because <see cref="IObjectConverter"/> is a
/// shipped public interface and the target frameworks include ones without default interface methods.
/// </summary>
internal sealed class TypedObjectConverter : IObjectConverter
{
    private readonly IObjectConverter _converter;

    /// <summary>
    /// This converter's own declaration as a filter, so a converted value can be asked the very question the
    /// read lane asks of a member type. Built only when verification is on — the flag is a JIT constant, so an
    /// ordinary process neither allocates it nor tests it.
    /// </summary>
    private readonly ObjectConverterTypeFilter? _declared;

    internal TypedObjectConverter(IObjectConverter converter, Type[] handledTypes)
    {
        _converter = converter;
        HandledTypes = handledTypes;

        if (HostContractVerification.Enabled)
        {
            _declared = ObjectConverterTypeFilter.ForDeclaredTypes(handledTypes);
        }
    }

    internal Type[] HandledTypes { get; }

    public bool TryConvert(Engine engine, object value, [NotNullWhen(true)] out JsValue? result)
    {
        var converted = _converter.TryConvert(engine, value, out result);
        if (HostContractVerification.Enabled && converted)
        {
            AssertConvertedTypeWasDeclared(value);
        }

        return converted;
    }

    /// <summary>
    /// A declaration is a promise about the whole converter, and nothing links it to the <c>switch</c> inside
    /// <see cref="IObjectConverter.TryConvert"/>: a case added there and not added to the registration is
    /// silently skipped on exactly the members whose declared type the registration excluded, and honoured
    /// everywhere else. That inconsistency is invisible from script, so it is worth a check when a host asks
    /// for one.
    /// </summary>
    private void AssertConvertedTypeWasDeclared(object value)
    {
        var runtimeType = value.GetType();
        if (_declared!.Claims(runtimeType))
        {
            return;
        }

        HostContractVerification.Fail(
            $"{_converter.GetType()} converted a value of type {runtimeType}, which is not among the types it was registered as handling ({string.Join(", ", Array.ConvertAll(HandledTypes, static t => t.ToString()))}). That registration promised the engine it may keep the compiled interop fast lanes for members and methods which cannot produce the declared types, so this converter is silently bypassed on exactly those — the declaration and the converter's own switch have drifted. Declare {runtimeType} at the AddObjectConverter call, or stop converting it.");
    }
}
