using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;

namespace Jint.SourceGenerators.Interop;

/// <summary>
/// How a member's declared type is reached. The three lanes a CLR member is read and written through are
/// not interchangeable, and which of them a member qualifies for is decided here — by exactly the rules
/// <c>CompiledMemberAccessor.IsSupportedMemberType</c> / <c>IsSupportedWrittenMemberType</c> apply at run
/// time, because a generated member that qualified for a lane the reflected one does not would stop
/// behaving like it.
/// </summary>
internal enum AccessibleValueKind
{
    /// <summary>Neither typed lane applies; the member is reached through the boxed lane only.</summary>
    Other = 0,
    Int32,
    Int64,
    Double,
    Boolean,
    String,

    /// <summary>Exactly <c>Jint.Native.JsValue</c>: both typed lanes apply, neither converts.</summary>
    JsValue,

    /// <summary>A <c>JsValue</c> subtype: the typed read lane applies, the typed write lane does not.</summary>
    JsValueSubtype,
}

internal static class AccessibleValueKindExtensions
{
    /// <summary>Whether the typed read lane (<c>Func&lt;object, JsValue&gt;</c>) covers this kind.</summary>
    public static bool HasTypedRead(this AccessibleValueKind kind) => kind != AccessibleValueKind.Other;

    /// <summary>Whether the typed write lane (<c>Func&lt;object, JsValue, bool&gt;</c>) covers this kind.</summary>
    public static bool HasTypedWrite(this AccessibleValueKind kind)
        => kind != AccessibleValueKind.Other && kind != AccessibleValueKind.JsValueSubtype;
}

internal sealed record class AccessibleMember(
    string Name,
    string TypeFullName,
    AccessibleValueKind Kind,
    bool IsValueType,
    bool CanRead,
    bool CanWrite);

internal sealed record class AccessibleMethod(
    string Name,
    int ParameterCount,
    string ReturnTypeFullName,
    bool ReturnsVoid);

internal sealed record class AccessibleTypeDefinition(
    string FullyQualifiedName,
    string MetadataPath,
    EquatableArray<AccessibleMember> Members,
    EquatableArray<AccessibleMethod> Methods)
{
    /// <summary>
    /// One file per annotated type, named by the path that actually identifies it. The MVP used
    /// <c>{Namespace}.{Name}</c>, which gives <c>Demo.Player</c> for both <c>Demo.Player</c> and
    /// <c>Demo.Outer.Player</c> — and a second <c>AddSource</c> under a hint name already used crashes the
    /// generator rather than reporting anything.
    /// </summary>
    public string HintName => MetadataPath + ".JsAccessible.g.cs";

    /// <summary>The same path as a C# identifier, which is what makes the emitted class names unique.</summary>
    public string SafeId => Sanitize(MetadataPath);

    private static string Sanitize(string id)
    {
        var sb = new StringBuilder(id.Length);
        foreach (var ch in id)
        {
            sb.Append(char.IsLetterOrDigit(ch) || ch == '_' ? ch : '_');
        }

        return sb.ToString();
    }

    public static AccessibleTypeDefinition? From(GeneratorAttributeSyntaxContext context, CancellationToken cancellationToken)
    {
        if (context.TargetSymbol is not INamedTypeSymbol type)
        {
            return null;
        }

        if (!IsEligibleType(type))
        {
            return null;
        }

        var members = ImmutableArray.CreateBuilder<AccessibleMember>();
        var methods = ImmutableArray.CreateBuilder<AccessibleMethod>();

        foreach (var member in type.GetMembers())
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (member.IsStatic || member.DeclaredAccessibility != Accessibility.Public)
            {
                continue;
            }

            switch (member)
            {
                case IPropertySymbol property when TryDescribeProperty(property, out var described):
                    members.Add(described!);
                    break;

                case IFieldSymbol field when TryDescribeField(field, out var describedField):
                    members.Add(describedField!);
                    break;

                case IMethodSymbol method when TryDescribeMethod(type, method, out var describedMethod):
                    methods.Add(describedMethod!);
                    break;
            }
        }

        if (members.Count == 0 && methods.Count == 0)
        {
            return null;
        }

        return new AccessibleTypeDefinition(
            FullyQualifiedName: type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            MetadataPath: MetadataPathOf(type),
            Members: new EquatableArray<AccessibleMember>(members.ToImmutable()),
            Methods: new EquatableArray<AccessibleMethod>(methods.ToImmutable()));
    }

    /// <summary>
    /// <c>Demo.Outer.Player</c> — namespace segments and containing types, which is what distinguishes two
    /// annotated types the MVP's <c>{Namespace}.{Name}</c> could not tell apart.
    /// </summary>
    private static string MetadataPathOf(INamedTypeSymbol type)
    {
        var names = new List<string>();
        for (INamedTypeSymbol? current = type; current is not null; current = current.ContainingType)
        {
            names.Add(current.Name);
        }

        names.Reverse();

        var sb = new StringBuilder();
        if (!type.ContainingNamespace.IsGlobalNamespace)
        {
            sb.Append(type.ContainingNamespace.ToDisplayString()).Append('.');
        }

        for (var i = 0; i < names.Count; i++)
        {
            if (i > 0)
            {
                sb.Append('.');
            }

            sb.Append(names[i]);
        }

        return sb.ToString();
    }

    private static bool IsEligibleType(INamedTypeSymbol type)
    {
        // A value type's instance members are excluded for the reason the run-time compiled lanes exclude
        // them: the typed access runs against a boxed copy. An open generic has no single runtime Type to
        // register under, and an abstract or static type is never a receiver's runtime type.
        if (type.TypeKind != TypeKind.Class || type.IsAbstract || type.IsStatic || type.IsGenericType)
        {
            return false;
        }

        // The generated registration is an ordinary class in the same assembly, so it can name anything
        // internal or better - but not a private or protected nested type.
        for (INamedTypeSymbol? current = type; current is not null; current = current.ContainingType)
        {
            switch (current.DeclaredAccessibility)
            {
                case Accessibility.Public:
                case Accessibility.Internal:
                case Accessibility.ProtectedOrInternal:
                case Accessibility.NotApplicable:
                    break;
                default:
                    return false;
            }
        }

        return true;
    }

    private static bool TryDescribeProperty(IPropertySymbol property, out AccessibleMember? member)
    {
        member = null;

        if (property.IsIndexer || property.Parameters.Length != 0 || property.RefKind != RefKind.None)
        {
            return false;
        }

        if (!IsExpressibleType(property.Type))
        {
            return false;
        }

        // Reflection reads and writes a non-public accessor perfectly well - PropertyInfo.CanWrite is true
        // for a `private set`, and SetValue honours it - while generated code cannot. A property with any
        // non-public accessor therefore stays on the reflection path entirely rather than silently losing
        // half of itself. `init` is the same case for a different reason: assignable only from an object
        // initializer in C#, and assignable by reflection.
        var getter = property.GetMethod;
        var setter = property.SetMethod;

        if (getter is not null && getter.DeclaredAccessibility != Accessibility.Public)
        {
            return false;
        }

        if (setter is not null && (setter.DeclaredAccessibility != Accessibility.Public || setter.IsInitOnly))
        {
            return false;
        }

        if (getter is null && setter is null)
        {
            return false;
        }

        member = new AccessibleMember(
            Name: property.Name,
            TypeFullName: property.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            Kind: Classify(property.Type),
            IsValueType: property.Type.IsValueType,
            CanRead: getter is not null,
            CanWrite: setter is not null);
        return true;
    }

    private static bool TryDescribeField(IFieldSymbol field, out AccessibleMember? member)
    {
        member = null;

        // A constant has no storage to read through, which is why the compiled read lane declines it too;
        // reflection answers it out of metadata.
        if (field.IsConst || field.IsImplicitlyDeclared || field.RefKind != RefKind.None)
        {
            return false;
        }

        if (!IsExpressibleType(field.Type))
        {
            return false;
        }

        member = new AccessibleMember(
            Name: field.Name,
            TypeFullName: field.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            Kind: Classify(field.Type),
            IsValueType: field.Type.IsValueType,
            CanRead: true,
            CanWrite: !field.IsReadOnly);
        return true;
    }

    private static bool TryDescribeMethod(INamedTypeSymbol type, IMethodSymbol method, out AccessibleMethod? described)
    {
        described = null;

        if (method.MethodKind != MethodKind.Ordinary
            || method.IsGenericMethod
            || method.IsAbstract
            || method.RefKind != RefKind.None)
        {
            return false;
        }

        // Overload resolution is what MethodInfoFunction exists for; a name carrying more than one candidate
        // anywhere the engine would look for it stays with it. Base types count (reflection reports inherited
        // methods) and so does System.Object, which is why an override of ToString or Equals is never taken.
        if (NameIsAmbiguous(type, method))
        {
            return false;
        }

        foreach (var parameter in method.Parameters)
        {
            // Only the shape whose reflected argument binding is a pass-through: a JsValue parameter is
            // handed the argument untouched (InteropParameterFlags.JsValueAssignable). Every other parameter
            // type goes through a conversion chain that is steered by engine options, and reproducing it in
            // emitted code is how the MVP acquired both of its behavioural divergences.
            if (parameter.RefKind != RefKind.None
                || parameter.IsParams
                || parameter.IsOptional
                || Classify(parameter.Type) != AccessibleValueKind.JsValue)
            {
                return false;
            }
        }

        if (!method.ReturnsVoid && !IsExpressibleType(method.ReturnType))
        {
            return false;
        }

        described = new AccessibleMethod(
            Name: method.Name,
            ParameterCount: method.Parameters.Length,
            ReturnTypeFullName: method.ReturnsVoid ? "void" : method.ReturnType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            ReturnsVoid: method.ReturnsVoid);
        return true;
    }

    private static bool NameIsAmbiguous(INamedTypeSymbol type, IMethodSymbol method)
    {
        var seen = 0;
        for (INamedTypeSymbol? current = type; current is not null; current = current.BaseType)
        {
            foreach (var sibling in current.GetMembers(method.Name))
            {
                if (sibling is IMethodSymbol { MethodKind: MethodKind.Ordinary })
                {
                    seen++;
                }
            }
        }

        if (seen != 1)
        {
            return true;
        }

        // An explicitly implemented interface member is resolved by name too, and reaches the same accessor.
        foreach (var iface in type.AllInterfaces)
        {
            foreach (var sibling in iface.GetMembers(method.Name))
            {
                if (sibling is IMethodSymbol { MethodKind: MethodKind.Ordinary })
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Whether the type can be named and boxed in emitted C#. Pointers, by-ref types and ref structs cannot.
    /// </summary>
    private static bool IsExpressibleType(ITypeSymbol type)
    {
        if (type.TypeKind == TypeKind.Pointer
            || type.TypeKind == TypeKind.FunctionPointer
            || type.TypeKind == TypeKind.TypeParameter
            || type.TypeKind == TypeKind.Error
            || type.IsRefLikeType)
        {
            return false;
        }

        // A tuple, a nested type, a generic instantiation - all nameable. What is not is anything whose
        // declaration the emitted file cannot see.
        return IsAtLeastInternal(type);
    }

    private static bool IsAtLeastInternal(ITypeSymbol type)
    {
        if (type is IArrayTypeSymbol array)
        {
            return IsAtLeastInternal(array.ElementType);
        }

        if (type is not INamedTypeSymbol named)
        {
            return true;
        }

        for (INamedTypeSymbol? current = named; current is not null; current = current.ContainingType)
        {
            switch (current.DeclaredAccessibility)
            {
                case Accessibility.Public:
                case Accessibility.Internal:
                case Accessibility.ProtectedOrInternal:
                case Accessibility.NotApplicable:
                    break;
                default:
                    return false;
            }
        }

        foreach (var argument in named.TypeArguments)
        {
            if (!IsAtLeastInternal(argument))
            {
                return false;
            }
        }

        return true;
    }

    private static AccessibleValueKind Classify(ITypeSymbol type)
    {
        switch (type.SpecialType)
        {
            case SpecialType.System_Int32: return AccessibleValueKind.Int32;
            case SpecialType.System_Int64: return AccessibleValueKind.Int64;
            case SpecialType.System_Double: return AccessibleValueKind.Double;
            case SpecialType.System_Boolean: return AccessibleValueKind.Boolean;
            case SpecialType.System_String: return AccessibleValueKind.String;
        }

        for (ITypeSymbol? current = type; current is not null; current = current.BaseType)
        {
            // The fully qualified format is deliberate: the default one carries nullable annotations, so a
            // member declared `JsValue?` would not compare equal to the name being looked for.
            if (string.Equals(current.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat), "global::Jint.Native.JsValue", StringComparison.Ordinal))
            {
                return ReferenceEquals(current, type) || SymbolEqualityComparer.Default.Equals(current, type)
                    ? AccessibleValueKind.JsValue
                    : AccessibleValueKind.JsValueSubtype;
            }
        }

        return AccessibleValueKind.Other;
    }
}
