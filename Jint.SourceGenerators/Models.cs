using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Jint.SourceGenerators;

/// <summary>
/// Per-class transform context. Caches the <c>Jint.Native.JsValue</c> symbol so we don't pay
/// <c>ToDisplayString</c> allocations on every parameter/return-type check.
/// </summary>
internal readonly struct ParseContext
{
    public readonly INamedTypeSymbol? JsValueSymbol;

    public ParseContext(Compilation compilation)
    {
        JsValueSymbol = compilation.GetTypeByMetadataName("Jint.Native.JsValue");
    }

    public bool IsJsValueOrSubtype(ITypeSymbol type)
    {
        if (JsValueSymbol is null) return false;
        for (var cur = type; cur is not null; cur = cur.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(cur, JsValueSymbol)) return true;
        }
        return false;
    }
}

internal sealed record class ObjectDefinition(
    string Namespace,
    string Name,
    string Accessibility,
    bool IsSealed,
    EquatableArray<FunctionDefinition> Functions,
    EquatableArray<PropertyDefinition> Properties,
    EquatableArray<SymbolDefinition> Symbols,
    EquatableArray<DiagnosticInfo> DiagnosticInfos)
{
    public string HintName => string.IsNullOrEmpty(Namespace)
        ? $"{Name}.g.cs"
        : $"{Namespace}.{Name}.g.cs";

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

    public static ObjectDefinition? From(GeneratorAttributeSyntaxContext ctx, CancellationToken ct)
    {
        if (ctx.TargetSymbol is not INamedTypeSymbol typeSymbol) return null;
        if (ctx.TargetNode is not ClassDeclarationSyntax classDecl) return null;

        var pc = new ParseContext(ctx.SemanticModel.Compilation);
        var diagnostics = new List<DiagnosticInfo>();
        var location = classDecl.Identifier.GetLocation();

        var isPartial = false;
        foreach (var modifier in classDecl.Modifiers)
        {
            if (modifier.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.PartialKeyword))
            {
                isPartial = true;
                break;
            }
        }

        if (!isPartial)
        {
            diagnostics.Add(new DiagnosticInfo(DiagnosticDescriptors.MustBePartial, location, typeSymbol.Name));
        }

        if (typeSymbol.ContainingType is not null)
        {
            diagnostics.Add(new DiagnosticInfo(DiagnosticDescriptors.MustNotBeNested, location, typeSymbol.Name));
        }

        var ns = typeSymbol.ContainingNamespace.IsGlobalNamespace
            ? string.Empty
            : typeSymbol.ContainingNamespace.ToDisplayString();

        var functions = new List<FunctionDefinition>();
        var properties = new List<PropertyDefinition>();
        var symbols = new List<SymbolDefinition>();
        HashSet<string>? seenFunctionClrNames = null;

        foreach (var member in typeSymbol.GetMembers())
        {
            ct.ThrowIfCancellationRequested();

            switch (member)
            {
                case IMethodSymbol method when HasAttribute(method, "JsFunctionAttribute"):
                {
                    seenFunctionClrNames ??= new HashSet<string>(StringComparer.Ordinal);
                    if (!seenFunctionClrNames.Add(method.Name))
                    {
                        diagnostics.Add(new DiagnosticInfo(
                            DiagnosticDescriptors.DuplicateJsName,
                            method.Locations.FirstOrDefault(),
                            method.Name,
                            typeSymbol.Name));
                        break;
                    }
                    var fn = FunctionDefinition.From(method, pc, diagnostics);
                    if (fn is not null) functions.Add(fn);
                    break;
                }
                case IFieldSymbol field when HasAttribute(field, "JsPropertyAttribute"):
                {
                    var prop = PropertyDefinition.FromField(field, pc, diagnostics);
                    if (prop is not null) properties.Add(prop);
                    break;
                }
                case IPropertySymbol property when HasAttribute(property, "JsPropertyAttribute"):
                {
                    var prop = PropertyDefinition.FromProperty(property, pc, diagnostics);
                    if (prop is not null) properties.Add(prop);
                    break;
                }
                case IFieldSymbol field when HasAttribute(field, "JsSymbolAttribute"):
                {
                    var sym = SymbolDefinition.FromField(field, pc, diagnostics);
                    if (sym is not null) symbols.Add(sym);
                    break;
                }
                case IPropertySymbol property when HasAttribute(property, "JsSymbolAttribute"):
                {
                    var sym = SymbolDefinition.FromProperty(property, pc, diagnostics);
                    if (sym is not null) symbols.Add(sym);
                    break;
                }
            }
        }

        functions.Sort(static (a, b) => string.CompareOrdinal(a.JsName, b.JsName));
        properties.Sort(static (a, b) => string.CompareOrdinal(a.JsName, b.JsName));
        symbols.Sort(static (a, b) => string.CompareOrdinal(a.SymbolName, b.SymbolName));

        return new ObjectDefinition(
            Namespace: ns,
            Name: typeSymbol.Name,
            Accessibility: AccessibilityToString(typeSymbol.DeclaredAccessibility),
            IsSealed: typeSymbol.IsSealed,
            Functions: functions.ToEquatableArray(),
            Properties: properties.ToEquatableArray(),
            Symbols: symbols.ToEquatableArray(),
            DiagnosticInfos: diagnostics.ToEquatableArray());
    }

    internal static bool HasAttribute(ISymbol symbol, string shortName)
    {
        foreach (var attr in symbol.GetAttributes())
        {
            if (attr.AttributeClass?.Name == shortName) return true;
        }
        return false;
    }

    internal static AttributeData? GetAttribute(ISymbol symbol, string shortName)
    {
        foreach (var attr in symbol.GetAttributes())
        {
            if (attr.AttributeClass?.Name == shortName) return attr;
        }
        return null;
    }

    internal static object? GetNamedArg(AttributeData? attr, string name)
    {
        if (attr is null) return null;
        foreach (var kvp in attr.NamedArguments)
        {
            if (kvp.Key == name) return kvp.Value.Value;
        }
        return null;
    }

    internal static bool HasNamedArg(AttributeData? attr, string name)
    {
        if (attr is null) return false;
        foreach (var kvp in attr.NamedArguments)
        {
            if (kvp.Key == name) return true;
        }
        return false;
    }

    private static string AccessibilityToString(Microsoft.CodeAnalysis.Accessibility a) => a switch
    {
        Microsoft.CodeAnalysis.Accessibility.Public => "public",
        Microsoft.CodeAnalysis.Accessibility.Internal => "internal",
        Microsoft.CodeAnalysis.Accessibility.Private => "private",
        Microsoft.CodeAnalysis.Accessibility.Protected => "protected",
        Microsoft.CodeAnalysis.Accessibility.ProtectedAndInternal => "private protected",
        Microsoft.CodeAnalysis.Accessibility.ProtectedOrInternal => "protected internal",
        _ => "internal",
    };
}

internal enum ParameterKind
{
    ThisObject,
    ValueAt,
    ToNumber,
    ToInt32,
    ToUint32,
    Rest,
    AllArguments,
}

internal sealed record class ParameterDefinition(ParameterKind Kind, int Position);

internal sealed record class FunctionDefinition(
    string ClrName,
    string JsName,
    int Length,
    bool IsStatic,
    EquatableArray<ParameterDefinition> Parameters)
{
    public static FunctionDefinition? From(IMethodSymbol method, ParseContext pc, List<DiagnosticInfo> diagnostics)
    {
        if (!pc.IsJsValueOrSubtype(method.ReturnType))
        {
            diagnostics.Add(new DiagnosticInfo(
                DiagnosticDescriptors.UnsupportedReturnType,
                method.Locations.FirstOrDefault(),
                method.Name,
                method.ReturnType.ToDisplayString()));
            return null;
        }

        var attr = ObjectDefinition.GetAttribute(method, "JsFunctionAttribute");
        var explicitName = ObjectDefinition.GetNamedArg(attr, "Name") as string;
        var explicitLength = ObjectDefinition.GetNamedArg(attr, "Length") as int? ?? -1;

        var jsName = !string.IsNullOrWhiteSpace(explicitName) ? explicitName! : ToCamelCase(method.Name);

        var parameters = new List<ParameterDefinition>();
        var valuePosition = 0;

        for (var i = 0; i < method.Parameters.Length; i++)
        {
            var param = method.Parameters[i];
            var isLast = i == method.Parameters.Length - 1;
            var isFirst = i == 0;

            if (isFirst && param.Name is "thisObject" or "thisObj")
            {
                if (!pc.IsJsValueOrSubtype(param.Type))
                {
                    diagnostics.Add(new DiagnosticInfo(
                        DiagnosticDescriptors.UnsupportedParameterType,
                        param.Locations.FirstOrDefault(),
                        param.Name, method.Name, param.Type.ToDisplayString()));
                    return null;
                }
                parameters.Add(new ParameterDefinition(ParameterKind.ThisObject, 0));
                continue;
            }

            // Detect conversion attributes — at most one per parameter, and not combinable with [Rest].
            var conversion = DetectConversion(param, method, diagnostics, out var failed);
            if (failed) return null;

            if (ObjectDefinition.HasAttribute(param, "RestAttribute"))
            {
                if (conversion is not null)
                {
                    diagnostics.Add(new DiagnosticInfo(
                        DiagnosticDescriptors.ConflictingConversionAttributes,
                        param.Locations.FirstOrDefault(),
                        param.Name, method.Name));
                    return null;
                }
                if (!isLast)
                {
                    diagnostics.Add(new DiagnosticInfo(
                        DiagnosticDescriptors.UnsupportedParameterType,
                        param.Locations.FirstOrDefault(),
                        param.Name, method.Name, "[Rest] must be the last parameter"));
                    return null;
                }
                if (!IsReadOnlySpanOfJsValue(param.Type, pc))
                {
                    diagnostics.Add(new DiagnosticInfo(
                        DiagnosticDescriptors.UnsupportedParameterType,
                        param.Locations.FirstOrDefault(),
                        param.Name, method.Name, "[Rest] must be of type ReadOnlySpan<JsValue>, got " + param.Type.ToDisplayString()));
                    return null;
                }
                parameters.Add(new ParameterDefinition(ParameterKind.Rest, valuePosition));
                continue;
            }

            if (IsJsValueArray(param.Type, pc))
            {
                if (!isLast)
                {
                    diagnostics.Add(new DiagnosticInfo(
                        DiagnosticDescriptors.UnsupportedParameterType,
                        param.Locations.FirstOrDefault(),
                        param.Name, method.Name, "JsCallArguments / JsValue[] must be the last parameter"));
                    return null;
                }
                parameters.Add(new ParameterDefinition(ParameterKind.AllArguments, 0));
                continue;
            }

            if (conversion is { } kind)
            {
                var expected = ExpectedConversionType(kind);
                if (param.Type.SpecialType != expected.specialType)
                {
                    diagnostics.Add(new DiagnosticInfo(
                        DiagnosticDescriptors.ConversionTypeMismatch,
                        param.Locations.FirstOrDefault(),
                        param.Name, method.Name, expected.attrName, param.Type.ToDisplayString(), expected.csharpName));
                    return null;
                }
                parameters.Add(new ParameterDefinition(kind, valuePosition));
                valuePosition++;
                continue;
            }

            if (!pc.IsJsValueOrSubtype(param.Type))
            {
                diagnostics.Add(new DiagnosticInfo(
                    DiagnosticDescriptors.UnsupportedParameterType,
                    param.Locations.FirstOrDefault(),
                    param.Name, method.Name, param.Type.ToDisplayString()));
                return null;
            }

            parameters.Add(new ParameterDefinition(ParameterKind.ValueAt, valuePosition));
            valuePosition++;
        }

        var length = explicitLength >= 0 ? explicitLength : valuePosition;

        return new FunctionDefinition(
            ClrName: method.Name,
            JsName: jsName,
            Length: length,
            IsStatic: method.IsStatic,
            Parameters: parameters.ToEquatableArray());
    }

    private static ParameterKind? DetectConversion(IParameterSymbol param, IMethodSymbol method, List<DiagnosticInfo> diagnostics, out bool failed)
    {
        failed = false;
        ParameterKind? found = null;
        foreach (var attr in param.GetAttributes())
        {
            var name = attr.AttributeClass?.Name;
            ParameterKind? kind = name switch
            {
                "ToNumberAttribute" => ParameterKind.ToNumber,
                "ToInt32Attribute" => ParameterKind.ToInt32,
                "ToUint32Attribute" => ParameterKind.ToUint32,
                _ => null,
            };
            if (kind is null) continue;
            if (found is not null)
            {
                diagnostics.Add(new DiagnosticInfo(
                    DiagnosticDescriptors.ConflictingConversionAttributes,
                    param.Locations.FirstOrDefault(),
                    param.Name, method.Name));
                failed = true;
                return null;
            }
            found = kind;
        }
        return found;
    }

    private static (SpecialType specialType, string csharpName, string attrName) ExpectedConversionType(ParameterKind kind) => kind switch
    {
        ParameterKind.ToNumber => (SpecialType.System_Double, "double", "ToNumber"),
        ParameterKind.ToInt32 => (SpecialType.System_Int32, "int", "ToInt32"),
        ParameterKind.ToUint32 => (SpecialType.System_UInt32, "uint", "ToUint32"),
        _ => (SpecialType.None, string.Empty, string.Empty),
    };

    private static bool IsReadOnlySpanOfJsValue(ITypeSymbol t, ParseContext pc)
    {
        if (t is not INamedTypeSymbol named) return false;
        if (named.ConstructedFrom.ToDisplayString() != "System.ReadOnlySpan<T>") return false;
        return named.TypeArguments.Length == 1 && pc.IsJsValueOrSubtype(named.TypeArguments[0]);
    }

    private static bool IsJsValueArray(ITypeSymbol t, ParseContext pc)
    {
        return t is IArrayTypeSymbol arr && pc.IsJsValueOrSubtype(arr.ElementType);
    }

    private static string ToCamelCase(string s)
    {
        if (string.IsNullOrEmpty(s) || !char.IsUpper(s[0])) return s;
        return char.ToLowerInvariant(s[0]) + s.Substring(1);
    }
}

internal sealed record class SymbolDefinition(
    string ClrName,
    string SymbolName,
    bool IsStatic,
    string FlagsExpression)
{
    public static SymbolDefinition? FromField(IFieldSymbol field, ParseContext pc, List<DiagnosticInfo> diagnostics)
    {
        if (!field.IsReadOnly || !pc.IsJsValueOrSubtype(field.Type))
        {
            diagnostics.Add(new DiagnosticInfo(
                DiagnosticDescriptors.UnsupportedPropertyType,
                field.Locations.FirstOrDefault(),
                field.Name));
            return null;
        }
        return Build(field.Name, field.IsStatic, field, diagnostics);
    }

    public static SymbolDefinition? FromProperty(IPropertySymbol property, ParseContext pc, List<DiagnosticInfo> diagnostics)
    {
        if (!property.IsReadOnly || !pc.IsJsValueOrSubtype(property.Type))
        {
            diagnostics.Add(new DiagnosticInfo(
                DiagnosticDescriptors.UnsupportedPropertyType,
                property.Locations.FirstOrDefault(),
                property.Name));
            return null;
        }
        return Build(property.Name, property.IsStatic, property, diagnostics);
    }

    private static SymbolDefinition? Build(string clrName, bool isStatic, ISymbol symbol, List<DiagnosticInfo> diagnostics)
    {
        var attr = ObjectDefinition.GetAttribute(symbol, "JsSymbolAttribute");
        if (attr is null || attr.ConstructorArguments.Length == 0) return null;
        var symbolName = attr.ConstructorArguments[0].Value as string;
        if (string.IsNullOrWhiteSpace(symbolName)) return null;

        var flagsExplicit = ObjectDefinition.HasNamedArg(attr, "Flags")
            ? ObjectDefinition.GetNamedArg(attr, "Flags") as int?
            : null;

        return new SymbolDefinition(
            ClrName: clrName,
            SymbolName: symbolName!,
            IsStatic: isStatic,
            FlagsExpression: FlagExpression.From(flagsExplicit, "Configurable"));
    }
}

internal sealed record class PropertyDefinition(
    string ClrName,
    string JsName,
    bool IsStatic,
    string FlagsExpression)
{
    public static PropertyDefinition? FromField(IFieldSymbol field, ParseContext pc, List<DiagnosticInfo> diagnostics)
    {
        if (!field.IsReadOnly || !pc.IsJsValueOrSubtype(field.Type))
        {
            diagnostics.Add(new DiagnosticInfo(
                DiagnosticDescriptors.UnsupportedPropertyType,
                field.Locations.FirstOrDefault(),
                field.Name));
            return null;
        }

        var attr = ObjectDefinition.GetAttribute(field, "JsPropertyAttribute");
        var name = ObjectDefinition.GetNamedArg(attr, "Name") as string;
        var flagsExplicit = ObjectDefinition.HasNamedArg(attr, "Flags")
            ? ObjectDefinition.GetNamedArg(attr, "Flags") as int?
            : null;

        return new PropertyDefinition(
            ClrName: field.Name,
            JsName: !string.IsNullOrWhiteSpace(name) ? name! : field.Name,
            IsStatic: field.IsStatic,
            FlagsExpression: FlagExpression.From(flagsExplicit, "AllForbidden"));
    }

    public static PropertyDefinition? FromProperty(IPropertySymbol property, ParseContext pc, List<DiagnosticInfo> diagnostics)
    {
        if (!property.IsReadOnly || !pc.IsJsValueOrSubtype(property.Type))
        {
            diagnostics.Add(new DiagnosticInfo(
                DiagnosticDescriptors.UnsupportedPropertyType,
                property.Locations.FirstOrDefault(),
                property.Name));
            return null;
        }

        var attr = ObjectDefinition.GetAttribute(property, "JsPropertyAttribute");
        var name = ObjectDefinition.GetNamedArg(attr, "Name") as string;
        var flagsExplicit = ObjectDefinition.HasNamedArg(attr, "Flags")
            ? ObjectDefinition.GetNamedArg(attr, "Flags") as int?
            : null;

        return new PropertyDefinition(
            ClrName: property.Name,
            JsName: !string.IsNullOrWhiteSpace(name) ? name! : property.Name,
            IsStatic: property.IsStatic,
            FlagsExpression: FlagExpression.From(flagsExplicit, "AllForbidden"));
    }
}
