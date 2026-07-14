using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Jint.SourceGenerators;

internal sealed record class AccessibleTypeDefinition(
    string Namespace,
    string Name,
    string FullyQualifiedName,
    EquatableArray<AccessibleProperty> Properties,
    EquatableArray<AccessibleMethod> Methods,
    EquatableArray<DiagnosticInfo> DiagnosticInfos)
{
    public string HintName => string.IsNullOrEmpty(Namespace)
        ? $"{Name}.JsAccessible.g.cs"
        : $"{Namespace}.{Name}.JsAccessible.g.cs";

    public string SafeId => (string.IsNullOrEmpty(Namespace) ? Name : Namespace.Replace('.', '_') + "_" + Name);

    public bool HasErrors
    {
        get
        {
            foreach (var d in DiagnosticInfos)
            {
                if (d.Severity == DiagnosticSeverity.Error) return true;
            }
            return false;
        }
    }

    public IEnumerable<Diagnostic> Diagnostics
    {
        get
        {
            foreach (var info in DiagnosticInfos) yield return info.ToDiagnostic();
        }
    }

    public static AccessibleTypeDefinition? From(GeneratorAttributeSyntaxContext ctx, CancellationToken ct)
    {
        if (ctx.TargetSymbol is not INamedTypeSymbol type) return null;
        if (ctx.TargetNode is not ClassDeclarationSyntax classDecl) return null;
        if (type.IsAbstract || type.IsStatic) return null;

        var diagnostics = new List<DiagnosticInfo>();
        var ns = type.ContainingNamespace.IsGlobalNamespace ? string.Empty : type.ContainingNamespace.ToDisplayString();
        var fqn = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

        var properties = new List<AccessibleProperty>();
        var methods = new List<AccessibleMethod>();

        foreach (var member in type.GetMembers())
        {
            ct.ThrowIfCancellationRequested();
            if (member.DeclaredAccessibility != Accessibility.Public) continue;
            if (member.IsStatic) continue;

            switch (member)
            {
                case IPropertySymbol prop when prop.Parameters.Length == 0 && !prop.IsIndexer:
                    {
                        var supported = AccessibleTypeExtensions.Classify(prop.Type);
                        if (supported == AccessibleType.Unsupported) continue;

                        var canGet = prop.GetMethod is { DeclaredAccessibility: Accessibility.Public };
                        var canSet = prop.SetMethod is { DeclaredAccessibility: Accessibility.Public };
                        if (!canGet && !canSet) continue;

                        properties.Add(new AccessibleProperty(
                            Name: prop.Name,
                            TypeFqn: prop.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                            ValueType: supported,
                            CanGet: canGet,
                            CanSet: canSet));
                        break;
                    }

                case IMethodSymbol m when m.MethodKind == MethodKind.Ordinary && !m.IsGenericMethod:
                    {
                        // MVP: methods with 0..1 parameters and supported return + parameter types only.
                        // Skip overloaded names — keeping first occurrence is too ambiguous; defer to reflection.
                        if (m.Parameters.Length > 1) continue;

                        // Skip overloads — only emit when the name is unique on this type. Overload resolution
                        // is non-trivial and defers to the existing MethodAccessor reflection path cleanly.
                        var sameNameCount = 0;
                        foreach (var sibling in type.GetMembers(m.Name))
                        {
                            if (sibling is IMethodSymbol sm && sm.MethodKind == MethodKind.Ordinary) sameNameCount++;
                        }
                        if (sameNameCount > 1) continue;

                        var returnKind = m.ReturnsVoid ? AccessibleType.Void : AccessibleTypeExtensions.Classify(m.ReturnType);
                        if (returnKind == AccessibleType.Unsupported) continue;

                        AccessibleType? paramKind = null;
                        string? paramTypeFqn = null;
                        if (m.Parameters.Length == 1)
                        {
                            var p = m.Parameters[0];
                            if (p.RefKind != RefKind.None) continue;
                            var k = AccessibleTypeExtensions.Classify(p.Type);
                            if (k == AccessibleType.Unsupported) continue;
                            paramKind = k;
                            paramTypeFqn = p.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                        }

                        methods.Add(new AccessibleMethod(
                            Name: m.Name,
                            ReturnType: returnKind,
                            ReturnTypeFqn: m.ReturnsVoid ? "void" : m.ReturnType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                            ParameterType: paramKind,
                            ParameterTypeFqn: paramTypeFqn));
                        break;
                    }
            }
        }

        if (properties.Count == 0 && methods.Count == 0)
        {
            return null;
        }

        return new AccessibleTypeDefinition(
            Namespace: ns,
            Name: type.Name,
            FullyQualifiedName: fqn,
            Properties: new EquatableArray<AccessibleProperty>(properties.ToImmutableArray()),
            Methods: new EquatableArray<AccessibleMethod>(methods.ToImmutableArray()),
            DiagnosticInfos: new EquatableArray<DiagnosticInfo>(diagnostics.ToImmutableArray()));
    }
}

internal sealed record class AccessibleProperty(
    string Name,
    string TypeFqn,
    AccessibleType ValueType,
    bool CanGet,
    bool CanSet);

internal sealed record class AccessibleMethod(
    string Name,
    AccessibleType ReturnType,
    string ReturnTypeFqn,
    AccessibleType? ParameterType,
    string? ParameterTypeFqn);

/// <summary>
/// Supported member shapes for MVP. Any other type causes the member to be skipped (silently —
/// reflection fallback covers it).
/// </summary>
internal enum AccessibleType
{
    Unsupported = 0,
    Void,
    Int32,
    Double,
    Boolean,
    String,
    JsValue,
}

internal static class AccessibleTypeExtensions
{
    public static AccessibleType Classify(this ITypeSymbol type)
    {
        switch (type.SpecialType)
        {
            case SpecialType.System_Int32: return AccessibleType.Int32;
            case SpecialType.System_Double: return AccessibleType.Double;
            case SpecialType.System_Boolean: return AccessibleType.Boolean;
            case SpecialType.System_String: return AccessibleType.String;
        }

        // JsValue + subtypes — the consuming user may pass-through JsValue without coercion.
        for (var cur = type; cur is not null; cur = cur.BaseType)
        {
            if (cur.ToDisplayString() == "Jint.Native.JsValue")
            {
                return AccessibleType.JsValue;
            }
        }

        return AccessibleType.Unsupported;
    }
}
