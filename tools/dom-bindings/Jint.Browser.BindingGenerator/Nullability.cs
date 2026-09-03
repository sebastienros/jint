using System.Reflection;

namespace Jint.Browser.BindingGenerator;

/// <summary>
/// Reads C#'s nullable-reference metadata off a parameter, which is how the generator learns that an
/// AngleSharp <c>String?</c> is Web IDL's <c>DOMString?</c> rather than its <c>DOMString</c>.
/// </summary>
/// <remarks>
/// <para>
/// The compiler records the annotation in two attributes and neither is in the BCL's reflection surface, so
/// they are matched by name: <c>NullableAttribute</c> carries the flags for one member (<c>1</c> not
/// annotated, <c>2</c> annotated), and where it is absent <c>NullableContextAttribute</c> on the nearest
/// enclosing method, type or module carries the default for everything inside it. Both are emitted per
/// assembly, so they are compared by full name rather than by type identity — the generator loads AngleSharp
/// in the reflection-only sense and never shares a <c>System.Runtime.CompilerServices</c> type with it.
/// </para>
/// <para>
/// <b>Only an operation's <c>String</c> parameter is asked</b>, and the two halves of that are separate
/// decisions. Interface-typed parameters go through <c>overrides.json</c>'s <c>nullableParameters</c>
/// instead, one line each naming the clause of the standard it comes from, because flipping every parameter
/// AngleSharp happens to annotate is one unreviewable change; a string argument is the case where the
/// annotation is load-bearing and the alternative is worse than the risk, since every namespaced member
/// takes a nullable namespace and without this <c>getAttributeNS(null, 'id')</c> looks for a namespace
/// literally spelled <c>"null"</c>. An IDL <em>attribute's</em> value is excluded because AngleSharp
/// annotates a hundred and fifty reflected content attributes <c>String?</c> — its own setters read
/// <see langword="null"/> as "remove the attribute" — while Web IDL declares every one of them a
/// non-nullable <c>DOMString</c>, so <c>el.id = null</c> owes the page the string <c>"null"</c>. Where the
/// standard does want null there it says <c>[LegacyNullToEmptyString]</c>, which is <c>""</c> and not null.
/// </para>
/// </remarks>
internal static class Nullability
{
    private const string NullableAttributeName = "System.Runtime.CompilerServices.NullableAttribute";
    private const string NullableContextAttributeName = "System.Runtime.CompilerServices.NullableContextAttribute";

    /// <summary>Whether <paramref name="parameter"/> is declared as a nullable reference.</summary>
    internal static bool IsNullableReference(ParameterInfo parameter)
    {
        if (parameter.ParameterType.IsValueType)
        {
            return false;
        }

        if (Flag(parameter.GetCustomAttributesData(), NullableAttributeName) is { } explicitFlag)
        {
            return explicitFlag == 2;
        }

        // No annotation of its own: the enclosing context decides. The member first, then the type it is
        // declared on, then the module — which is where a project-wide `<Nullable>enable</Nullable>` lands.
        for (var scope = (MemberInfo?) parameter.Member; scope is not null; scope = scope.DeclaringType)
        {
            if (Flag(scope.GetCustomAttributesData(), NullableContextAttributeName) is { } contextFlag)
            {
                return contextFlag == 2;
            }
        }

        var module = parameter.Member.Module;
        return Flag(module.GetCustomAttributesData(), NullableContextAttributeName) == 2;
    }

    /// <summary>
    /// The single byte the named attribute carries, or <see langword="null"/> when it is absent. A
    /// <c>NullableAttribute</c> on a generic type carries an array instead; its first element is the
    /// annotation of the type itself, which is the only one a plain <c>String</c> parameter has.
    /// </summary>
    private static byte? Flag(IList<CustomAttributeData> attributes, string fullName)
    {
        foreach (var attribute in attributes)
        {
            if (attribute.AttributeType.FullName != fullName || attribute.ConstructorArguments.Count != 1)
            {
                continue;
            }

            var argument = attribute.ConstructorArguments[0];
            if (argument.Value is byte flag)
            {
                return flag;
            }

            if (argument.Value is IReadOnlyList<CustomAttributeTypedArgument> { Count: > 0 } flags && flags[0].Value is byte first)
            {
                return first;
            }
        }

        return null;
    }
}
