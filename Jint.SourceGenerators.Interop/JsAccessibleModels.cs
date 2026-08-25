using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

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

/// <summary>
/// One annotated declaration, as the pipeline carries it: what can be emitted for it, if anything, and
/// what could not be — the second half being the point of the diagnostics, since a declined member keeps
/// the reflection path silently and the feature's whole claim is about not reflecting.
/// </summary>
/// <remarks>
/// The two halves travel together because they are decided together and deduplicated together: a partial
/// class carrying the attribute on two of its parts arrives twice, and reporting one part's diagnostics
/// twice would be as wrong as emitting one part's source twice.
/// </remarks>
internal sealed record class AccessibleTypeResult(
    string MetadataPath,
    AccessibleTypeDefinition? Definition,
    EquatableArray<AccessibleDiagnostic> DiagnosticInfos)
{
    public IEnumerable<Diagnostic> Diagnostics
    {
        get
        {
            foreach (var info in DiagnosticInfos)
            {
                yield return info.ToDiagnostic();
            }
        }
    }

    public static AccessibleTypeResult? From(GeneratorAttributeSyntaxContext context, CancellationToken cancellationToken)
    {
        if (context.TargetSymbol is not INamedTypeSymbol type)
        {
            return null;
        }

        var typeLocation = context.TargetNode is TypeDeclarationSyntax declaration
            ? declaration.Identifier.GetLocation()
            : context.TargetNode.GetLocation();

        var diagnostics = new List<AccessibleDiagnostic>();
        var metadataPath = MetadataPathOf(type);

        var ineligible = IneligibleTypeReason(type);
        if (ineligible is not null)
        {
            diagnostics.Add(new AccessibleDiagnostic(
                JsAccessibleDiagnosticDescriptors.TypeCannotBeRegistered,
                typeLocation,
                type.Name,
                ineligible));
            return new AccessibleTypeResult(metadataPath, Definition: null, diagnostics.ToEquatableArray());
        }

        var members = ImmutableArray.CreateBuilder<AccessibleMember>();
        var methods = ImmutableArray.CreateBuilder<AccessibleMethod>();

        foreach (var member in type.GetMembers())
        {
            cancellationToken.ThrowIfCancellationRequested();

            // A member that is not public is not reachable from script under any binding-flag configuration
            // the engine offers, so declining it costs nothing and there is nothing to report. Saying so on
            // every private field of an annotated type would be the noise that gets the whole rule turned
            // off.
            if (member.DeclaredAccessibility != Accessibility.Public)
            {
                continue;
            }

            switch (member)
            {
                case IPropertySymbol property:
                    DescribeProperty(type, property, members, diagnostics);
                    break;

                case IFieldSymbol field:
                    DescribeField(type, field, members, diagnostics);
                    break;

                case IMethodSymbol method:
                    DescribeMethod(type, method, methods, diagnostics);
                    break;
            }
        }

        if (members.Count == 0 && methods.Count == 0)
        {
            diagnostics.Add(new AccessibleDiagnostic(
                JsAccessibleDiagnosticDescriptors.TypeRegistersNothing,
                typeLocation,
                type.Name));
            return new AccessibleTypeResult(metadataPath, Definition: null, diagnostics.ToEquatableArray());
        }

        var definition = new AccessibleTypeDefinition(
            FullyQualifiedName: type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            MetadataPath: metadataPath,
            Members: new EquatableArray<AccessibleMember>(members.ToImmutable()),
            Methods: new EquatableArray<AccessibleMethod>(methods.ToImmutable()));

        return new AccessibleTypeResult(metadataPath, definition, diagnostics.ToEquatableArray());
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

    /// <summary>
    /// Why the whole type stays on the reflection path, or <see langword="null"/> when it does not. A value
    /// type's instance members are excluded for the reason the run-time compiled lanes exclude them: the
    /// typed access runs against a boxed copy. An open generic has no single runtime <c>Type</c> to register
    /// under, and an abstract or static type is never a receiver's runtime type.
    /// </summary>
    private static string? IneligibleTypeReason(INamedTypeSymbol type)
    {
        if (type.TypeKind != TypeKind.Class)
        {
            return $"it is a {type.TypeKind.ToString().ToLowerInvariant()}, and only a class is registered";
        }

        // A record is a class, so it reaches here — and its compiler-generated members are exactly the shape
        // the emitter cannot name: `<Clone>$` is an ordinary public method whose name is not an identifier.
        // Declining the type is what the generator has always done (a record declaration is not a
        // ClassDeclarationSyntax, so it never used to arrive at all); the only change is that it now says so.
        if (type.IsRecord)
        {
            return "it is a record, whose compiler-generated members the generator does not model";
        }

        if (type.IsStatic)
        {
            return "it is a static class, so it is never the runtime type of a receiver";
        }

        if (type.IsAbstract)
        {
            return "it is abstract, so it is never the runtime type of a receiver";
        }

        if (type.IsGenericType)
        {
            return "it is a generic type definition, and has no single runtime type to register under";
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
                    return "it, or a type containing it, is private or protected, and the generated registration cannot name it";
            }
        }

        return null;
    }

    private static void DescribeProperty(
        INamedTypeSymbol type,
        IPropertySymbol property,
        ImmutableArray<AccessibleMember>.Builder members,
        List<AccessibleDiagnostic> diagnostics)
    {
        // Reported on its own rule: an indexer is not merely a member the generator declines, it is probed
        // ahead of the declared members, so every name it answers for resolves through it whatever the
        // registry holds.
        if (property.IsIndexer)
        {
            diagnostics.Add(new AccessibleDiagnostic(
                JsAccessibleDiagnosticDescriptors.IndexerIsProbedFirst,
                LocationOf(property),
                property.Parameters.Length == 1 ? property.Parameters[0].Type.ToDisplayString() : "…",
                type.Name));
            return;
        }

        // Default ObjectWrapperReportedPropertyBindingFlags is Public | Instance, so a static property is
        // not reachable from script at all and declining it takes nothing away. (Methods differ - the
        // default method flags include Static - which is why DescribeMethod reports one and this does not.)
        if (property.IsStatic || property.Parameters.Length != 0)
        {
            return;
        }

        if (property.RefKind != RefKind.None)
        {
            ReportMember(diagnostics, JsAccessibleDiagnosticDescriptors.MemberDeclarationNotGenerated, property, type, "it returns by reference");
            return;
        }

        if (!IsExpressibleType(property.Type))
        {
            ReportInexpressible(diagnostics, property, type, property.Type, "its type");
            return;
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
            ReportMember(diagnostics, JsAccessibleDiagnosticDescriptors.MemberDeclarationNotGenerated, property, type, "its get accessor is not public, and generated code cannot call it");
            return;
        }

        if (setter is not null && setter.IsInitOnly)
        {
            ReportMember(diagnostics, JsAccessibleDiagnosticDescriptors.MemberDeclarationNotGenerated, property, type, "its setter is 'init', which only an object initializer or reflection can call");
            return;
        }

        if (setter is not null && setter.DeclaredAccessibility != Accessibility.Public)
        {
            ReportMember(diagnostics, JsAccessibleDiagnosticDescriptors.MemberDeclarationNotGenerated, property, type, "its set accessor is not public, and generated code cannot call it");
            return;
        }

        if (getter is null && setter is null)
        {
            return;
        }

        members.Add(new AccessibleMember(
            Name: property.Name,
            TypeFullName: property.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            Kind: Classify(property.Type),
            IsValueType: property.Type.IsValueType,
            CanRead: getter is not null,
            CanWrite: setter is not null));
    }

    private static void DescribeField(
        INamedTypeSymbol type,
        IFieldSymbol field,
        ImmutableArray<AccessibleMember>.Builder members,
        List<AccessibleDiagnostic> diagnostics)
    {
        // Three silent declines, and none of them is a difference a host could act on. A backing field is
        // not something they wrote. A static field - and a constant, which is static - is not reported by
        // the default ObjectWrapperReportedFieldBindingFlags (Public | Instance), so it is not reachable
        // from script whether or not the generator takes it; the IsConst test is kept beside IsStatic
        // because the compiled read lane declines a constant for its own reason, that it has no storage to
        // read through. And a ref field exists only in a ref struct, which never reaches this method.
        if (field.IsImplicitlyDeclared || field.IsStatic || field.IsConst || field.RefKind != RefKind.None)
        {
            return;
        }

        if (!IsExpressibleType(field.Type))
        {
            ReportInexpressible(diagnostics, field, type, field.Type, "its type");
            return;
        }

        members.Add(new AccessibleMember(
            Name: field.Name,
            TypeFullName: field.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            Kind: Classify(field.Type),
            IsValueType: field.Type.IsValueType,
            CanRead: true,
            CanWrite: !field.IsReadOnly));
    }

    private static void DescribeMethod(
        INamedTypeSymbol type,
        IMethodSymbol method,
        ImmutableArray<AccessibleMethod>.Builder methods,
        List<AccessibleDiagnostic> diagnostics)
    {
        // Property accessors, constructors, operators, finalizers and event accessors. Reflection does
        // resolve `get_Score` by name, so declining these is a real difference — but nobody annotated a type
        // wanting a generated `get_Score`, and reporting two lines per property is how a rule earns a
        // blanket suppression.
        if (method.MethodKind != MethodKind.Ordinary)
        {
            return;
        }

        // Unlike properties and fields, the default ObjectWrapperReportedMethodBindingFlags includes Static,
        // so a public static method IS reachable from script through an instance wrapper and declining it is
        // a genuine difference.
        if (method.IsStatic)
        {
            ReportMethod(diagnostics, JsAccessibleDiagnosticDescriptors.MethodSignatureNotGenerated, method, type, "it is static, and the generated read, write and invoke lanes are instance lanes");
            return;
        }

        if (method.IsGenericMethod)
        {
            ReportMethod(diagnostics, JsAccessibleDiagnosticDescriptors.MethodSignatureNotGenerated, method, type, "it is generic, and its type arguments are chosen per call");
            return;
        }

        // Defensive, and silent because it cannot be reached: an abstract method exists only in an abstract
        // class, and IneligibleTypeReason has already declined the whole type by the time this runs.
        if (method.IsAbstract)
        {
            return;
        }

        if (method.RefKind != RefKind.None)
        {
            ReportMethod(diagnostics, JsAccessibleDiagnosticDescriptors.MethodSignatureNotGenerated, method, type, "it returns by reference");
            return;
        }

        // Overload resolution is what MethodInfoFunction exists for; a name carrying more than one candidate
        // anywhere the engine would look for it stays with it. Base types count (reflection reports inherited
        // methods) and so does System.Object, which is why an override of ToString or Equals is never taken.
        var ambiguity = NameAmbiguityReason(type, method);
        if (ambiguity is not null)
        {
            diagnostics.Add(new AccessibleDiagnostic(
                JsAccessibleDiagnosticDescriptors.MethodNameIsNotUnique,
                LocationOf(method),
                method.Name,
                type.Name,
                ambiguity));
            return;
        }

        foreach (var parameter in method.Parameters)
        {
            // Only the shape whose reflected argument binding is a pass-through: a JsValue parameter is
            // handed the argument untouched (InteropParameterFlags.JsValueAssignable). Every other parameter
            // type goes through a conversion chain that is steered by engine options, and reproducing it in
            // emitted code is how the MVP acquired both of its behavioural divergences.
            var problem = ParameterProblem(parameter);
            if (problem is not null)
            {
                diagnostics.Add(new AccessibleDiagnostic(
                    JsAccessibleDiagnosticDescriptors.MethodSignatureNotGenerated,
                    parameter.Locations.FirstOrDefault() ?? LocationOf(method),
                    method.Name,
                    type.Name,
                    problem));
                return;
            }
        }

        if (!method.ReturnsVoid && !IsExpressibleType(method.ReturnType))
        {
            var reason = InexpressibleReason(method.ReturnType);
            if (reason is not null)
            {
                diagnostics.Add(new AccessibleDiagnostic(
                    JsAccessibleDiagnosticDescriptors.MemberTypeCannotBeNamed,
                    LocationOf(method),
                    "Method",
                    method.Name,
                    type.Name,
                    $"its return type '{method.ReturnType.ToDisplayString()}' {reason}"));
            }

            return;
        }

        methods.Add(new AccessibleMethod(
            Name: method.Name,
            ParameterCount: method.Parameters.Length,
            ReturnTypeFullName: method.ReturnsVoid ? "void" : method.ReturnType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            ReturnsVoid: method.ReturnsVoid));
    }

    private static string? ParameterProblem(IParameterSymbol parameter)
    {
        if (parameter.RefKind != RefKind.None)
        {
            return $"parameter '{parameter.Name}' is passed by reference";
        }

        if (parameter.IsParams)
        {
            return $"parameter '{parameter.Name}' is a params parameter, whose reflected binding spreads the remaining arguments";
        }

        if (parameter.IsOptional)
        {
            return $"parameter '{parameter.Name}' is optional, and its default is applied by the reflected binder";
        }

        if (Classify(parameter.Type) != AccessibleValueKind.JsValue)
        {
            return $"parameter '{parameter.Name}' is typed '{parameter.Type.ToDisplayString()}' rather than JsValue, so its reflected binding is a conversion chain steered by engine options";
        }

        return null;
    }

    private static string? NameAmbiguityReason(INamedTypeSymbol type, IMethodSymbol method)
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
            return $"'{method.Name}' is declared more than once on the type or a base type, and choosing between the candidates is what the reflected path exists for";
        }

        // An explicitly implemented interface member is resolved by name too, and reaches the same accessor.
        foreach (var iface in type.AllInterfaces)
        {
            foreach (var sibling in iface.GetMembers(method.Name))
            {
                if (sibling is IMethodSymbol { MethodKind: MethodKind.Ordinary })
                {
                    return $"'{iface.ToDisplayString()}' also declares '{method.Name}', which resolves by name to the same accessor";
                }
            }
        }

        return null;
    }

    /// <summary>JINT032 and JINT033 name the method directly, so their arguments are (name, type, reason).</summary>
    private static void ReportMethod(
        List<AccessibleDiagnostic> diagnostics,
        DiagnosticDescriptor descriptor,
        IMethodSymbol method,
        INamedTypeSymbol type,
        string reason)
        => diagnostics.Add(new AccessibleDiagnostic(descriptor, LocationOf(method), method.Name, type.Name, reason));

    /// <summary>JINT034 and JINT035 cover more than one member kind, so they lead with the kind word.</summary>
    private static void ReportMember(
        List<AccessibleDiagnostic> diagnostics,
        DiagnosticDescriptor descriptor,
        ISymbol member,
        INamedTypeSymbol type,
        string reason)
        => diagnostics.Add(new AccessibleDiagnostic(descriptor, LocationOf(member), KindWordOf(member), member.Name, type.Name, reason));

    private static void ReportInexpressible(
        List<AccessibleDiagnostic> diagnostics,
        ISymbol member,
        INamedTypeSymbol type,
        ITypeSymbol memberType,
        string possessive)
    {
        var reason = InexpressibleReason(memberType);
        if (reason is null)
        {
            return;
        }

        diagnostics.Add(new AccessibleDiagnostic(
            JsAccessibleDiagnosticDescriptors.MemberTypeCannotBeNamed,
            LocationOf(member),
            KindWordOf(member),
            member.Name,
            type.Name,
            $"{possessive} '{memberType.ToDisplayString()}' {reason}"));
    }

    private static string KindWordOf(ISymbol member) => member switch
    {
        IPropertySymbol => "Property",
        IFieldSymbol => "Field",
        _ => "Method",
    };

    private static Location? LocationOf(ISymbol symbol)
    {
        foreach (var location in symbol.Locations)
        {
            if (location.IsInSource)
            {
                return location;
            }
        }

        return null;
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

    /// <summary>
    /// Why <see cref="IsExpressibleType"/> declined the type, as a clause, or <see langword="null"/> when
    /// there is nothing worth saying — which is the error-type case, where the compilation already carries a
    /// diagnostic of its own and a second one about interop would only bury it.
    /// </summary>
    private static string? InexpressibleReason(ITypeSymbol type)
    {
        if (type.TypeKind == TypeKind.Error)
        {
            return null;
        }

        if (type.TypeKind is TypeKind.Pointer or TypeKind.FunctionPointer)
        {
            return "is a pointer type, which generated code cannot box";
        }

        if (type.TypeKind == TypeKind.TypeParameter)
        {
            return "is a type parameter, which has no single runtime type";
        }

        if (type.IsRefLikeType)
        {
            return "is a ref struct, which generated code cannot box";
        }

        return "is less accessible than internal, so the generated file cannot name it";
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
}
