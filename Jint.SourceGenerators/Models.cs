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
    EquatableArray<ThrowerAccessorDefinition> ThrowerAccessors,
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
        var throwerAccessors = new List<ThrowerAccessorDefinition>();
        HashSet<string>? seenFunctionClrNames = null;
        HashSet<string>? seenThrowerNames = null;
        // For JINT017: tracks the FlagsExpression first seen per accessor JsName so we can flag a
        // conflict at the SECOND method's location (not the class identifier).
        Dictionary<string, string>? accessorFlagsByJsName = null;

        // Class-level [JsThrowerAccessor("name")] — repeats permitted.
        foreach (var attr in typeSymbol.GetAttributes())
        {
            if (attr.AttributeClass?.Name != "JsThrowerAccessorAttribute") continue;
            if (attr.ConstructorArguments.Length == 0) continue;
            var name = attr.ConstructorArguments[0].Value as string;
            if (string.IsNullOrWhiteSpace(name)) continue;
            seenThrowerNames ??= new HashSet<string>(StringComparer.Ordinal);
            if (!seenThrowerNames.Add(name!))
            {
                diagnostics.Add(new DiagnosticInfo(
                    DiagnosticDescriptors.DuplicateThrowerAccessor,
                    location,
                    name!, typeSymbol.Name));
                continue;
            }
            var flagsExplicit = HasNamedArg(attr, "Flags") ? GetNamedArg(attr, "Flags") as int? : null;
            throwerAccessors.Add(new ThrowerAccessorDefinition(name!, FlagExpression.From(flagsExplicit, "Configurable")));
        }

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
                    var fn = FunctionDefinition.FromJsFunction(method, pc, diagnostics);
                    if (fn is not null) functions.Add(fn);
                    break;
                }
                case IMethodSymbol method when HasAttribute(method, "JsAccessorAttribute"):
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
                    var fn = FunctionDefinition.FromJsAccessor(method, pc, diagnostics);
                    if (fn is null) break;
                    functions.Add(fn);

                    // JINT017: pair this accessor with any prior get/set sharing the same JsName.
                    // Diagnostic points at the SECOND method's location for actionable navigation.
                    accessorFlagsByJsName ??= new Dictionary<string, string>(StringComparer.Ordinal);
                    if (accessorFlagsByJsName.TryGetValue(fn.JsName, out var firstFlags))
                    {
                        if (!string.Equals(firstFlags, fn.FlagsExpression, StringComparison.Ordinal))
                        {
                            diagnostics.Add(new DiagnosticInfo(
                                DiagnosticDescriptors.ConflictingAccessorFlags,
                                method.Locations.FirstOrDefault(),
                                fn.JsName, typeSymbol.Name));
                        }
                    }
                    else
                    {
                        accessorFlagsByJsName[fn.JsName] = fn.FlagsExpression;
                    }
                    break;
                }
                case IMethodSymbol method when HasAttribute(method, "JsSymbolFunctionAttribute"):
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
                    var fn = FunctionDefinition.FromJsSymbolFunction(method, pc, diagnostics);
                    if (fn is not null) functions.Add(fn);
                    break;
                }
                case IMethodSymbol method when HasAttribute(method, "JsSymbolAccessorAttribute"):
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
                    var fn = FunctionDefinition.FromJsSymbolAccessor(method, pc, diagnostics);
                    if (fn is null) break;
                    functions.Add(fn);

                    // JINT017: pair this symbol-accessor with any prior get/set under the same SymbolName.
                    accessorFlagsByJsName ??= new Dictionary<string, string>(StringComparer.Ordinal);
                    var pairKey = "@@" + fn.JsName;
                    if (accessorFlagsByJsName.TryGetValue(pairKey, out var firstFlags))
                    {
                        if (!string.Equals(firstFlags, fn.FlagsExpression, StringComparison.Ordinal))
                        {
                            diagnostics.Add(new DiagnosticInfo(
                                DiagnosticDescriptors.ConflictingAccessorFlags,
                                method.Locations.FirstOrDefault(),
                                fn.JsName, typeSymbol.Name));
                        }
                    }
                    else
                    {
                        accessorFlagsByJsName[pairKey] = fn.FlagsExpression;
                    }
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

        // JINT018: any function uses cast (non-JsValue thisObject) but the host has no accessible _realm field?
        var usesCast = false;
        foreach (var fn in functions)
        {
            foreach (var p in fn.Parameters)
            {
                if (p.Kind == ParameterKind.ThisObject && p.CastTargetType is not null) { usesCast = true; break; }
            }
            if (usesCast) break;
        }
        if (usesCast && !HasAccessibleRealmField(typeSymbol))
        {
            diagnostics.Add(new DiagnosticInfo(
                DiagnosticDescriptors.MissingRealmField,
                location,
                typeSymbol.Name));
        }

        functions.Sort(static (a, b) => string.CompareOrdinal(a.JsName, b.JsName));
        properties.Sort(static (a, b) => string.CompareOrdinal(a.JsName, b.JsName));
        symbols.Sort(static (a, b) => string.CompareOrdinal(a.SymbolName, b.SymbolName));
        throwerAccessors.Sort(static (a, b) => string.CompareOrdinal(a.JsName, b.JsName));

        return new ObjectDefinition(
            Namespace: ns,
            Name: typeSymbol.Name,
            Accessibility: AccessibilityToString(typeSymbol.DeclaredAccessibility),
            IsSealed: typeSymbol.IsSealed,
            Functions: functions.ToEquatableArray(),
            Properties: properties.ToEquatableArray(),
            Symbols: symbols.ToEquatableArray(),
            ThrowerAccessors: throwerAccessors.ToEquatableArray(),
            DiagnosticInfos: diagnostics.ToEquatableArray());
    }

    /// <summary>
    /// True if the type or any base class declares a Realm-typed field named <c>_realm</c>. The check is
    /// structural — we don't verify visibility — but in practice every consumer in this assembly hits one
    /// of two cases: a Function-derived host inherits the <c>internal</c> _realm from Function, or a
    /// custom prototype declares its own <c>private readonly Realm _realm;</c> on the same class as the
    /// (nested) dispatcher. Both are accessible at the dispatcher's emit site under standard nested-type
    /// + same-assembly access rules. A future host that hides _realm behind cross-assembly inaccessibility
    /// would slip through here and surface as a generated-code compile error instead of JINT018.
    /// </summary>
    private static bool HasAccessibleRealmField(INamedTypeSymbol type)
    {
        for (INamedTypeSymbol? t = type; t is not null; t = t.BaseType)
        {
            foreach (var member in t.GetMembers("_realm"))
            {
                if (member is IFieldSymbol field && field.Type.ToDisplayString() == "Jint.Runtime.Realm")
                {
                    return true;
                }
            }
        }
        return false;
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
    ToInteger,
    ToLength,
    ToString,
    ToJsString,
    ToObject,
    Rest,
    AllArguments,
}

internal sealed record class ParameterDefinition(ParameterKind Kind, int Position, string? CastTargetType = null);

internal enum RegistrationKind
{
    DataProperty,        // [JsFunction] — emit into properties[…] as LazyPropertyDescriptor
    AccessorGet,         // [JsAccessor(Get)] — pair with matching Set by JsName, emit GetSetPropertyDescriptor
    AccessorSet,         // [JsAccessor(Set)]
    SymbolFunction,      // [JsSymbolFunction] — emit into symbols[GlobalSymbolRegistry.X] as LazyPropertyDescriptor
    SymbolAccessorGet,   // [JsSymbolAccessor(Get)] — pair with matching Set by SymbolName, emit symbol-keyed GetSetPropertyDescriptor
    SymbolAccessorSet,   // [JsSymbolAccessor(Set)]
}

internal sealed record class ThrowerAccessorDefinition(string JsName, string FlagsExpression);

internal sealed record class FunctionDefinition(
    string ClrName,
    string JsName,
    string FunctionName,
    int Length,
    bool IsStatic,
    EquatableArray<ParameterDefinition> Parameters,
    RegistrationKind Registration,
    string FlagsExpression,
    bool RequireObjectCoercible)
{
    public static FunctionDefinition? FromJsFunction(IMethodSymbol method, ParseContext pc, List<DiagnosticInfo> diagnostics)
    {
        var attr = ObjectDefinition.GetAttribute(method, "JsFunctionAttribute");
        var explicitName = ObjectDefinition.GetNamedArg(attr, "Name") as string;
        var explicitLength = ObjectDefinition.GetNamedArg(attr, "Length") as int? ?? -1;
        var jsName = !string.IsNullOrWhiteSpace(explicitName) ? explicitName! : ToCamelCase(method.Name);
        var flagsExplicit = ObjectDefinition.HasNamedArg(attr, "Flags")
            ? ObjectDefinition.GetNamedArg(attr, "Flags") as int?
            : null;
        var flags = FlagExpression.From(flagsExplicit, "NonEnumerable");
        return BuildCommon(method, pc, diagnostics, jsName, /* functionName */ jsName, explicitLength, RegistrationKind.DataProperty, flags);
    }

    public static FunctionDefinition? FromJsAccessor(IMethodSymbol method, ParseContext pc, List<DiagnosticInfo> diagnostics)
    {
        var attr = ObjectDefinition.GetAttribute(method, "JsAccessorAttribute");
        if (attr is null || attr.ConstructorArguments.Length == 0) return null;
        var jsName = attr.ConstructorArguments[0].Value as string;
        if (string.IsNullOrWhiteSpace(jsName)) return null;
        var kindEnumValue = attr.ConstructorArguments.Length > 1 ? (attr.ConstructorArguments[1].Value as int? ?? 0) : 0;
        var registration = kindEnumValue == 1 ? RegistrationKind.AccessorSet : RegistrationKind.AccessorGet;
        // Default accessor flags: enumerable: false, configurable: true → Configurable | EnumerableSet (16 | 2 = 18).
        // EnumerableSet matters for Object.getOwnPropertyDescriptor introspection.
        var flagsExplicit = ObjectDefinition.HasNamedArg(attr, "Flags")
            ? ObjectDefinition.GetNamedArg(attr, "Flags") as int?
            : (int?) 18;
        var flags = FlagExpression.From(flagsExplicit, "Configurable");
        var expectedValueParams = registration == RegistrationKind.AccessorSet ? 1 : 0;
        var functionName = (registration == RegistrationKind.AccessorSet ? "set " : "get ") + jsName;
        var fn = BuildCommon(method, pc, diagnostics, jsName!, functionName, expectedValueParams, registration, flags);
        if (fn is null) return null;

        // JINT019: validate the actual value-parameter count matches the accessor kind.
        var actualValueParams = 0;
        foreach (var p in fn.Parameters)
        {
            if (p.Kind != ParameterKind.ThisObject && p.Kind != ParameterKind.AllArguments && p.Kind != ParameterKind.Rest)
            {
                actualValueParams++;
            }
        }
        if (actualValueParams != expectedValueParams)
        {
            // Spec arity: getter must have 0 value params, setter must have exactly 1.
            var detail = expectedValueParams == 0
                ? $"getter must take no value parameters, has {actualValueParams}"
                : $"setter must take exactly one value parameter, has {actualValueParams}";
            diagnostics.Add(new DiagnosticInfo(
                DiagnosticDescriptors.InvalidAccessorSignature,
                method.Locations.FirstOrDefault(),
                registration == RegistrationKind.AccessorSet ? "set" : "get",
                method.Name,
                method.ContainingType.Name,
                detail));
            return null;
        }

        return fn;
    }

    public static FunctionDefinition? FromJsSymbolFunction(IMethodSymbol method, ParseContext pc, List<DiagnosticInfo> diagnostics)
    {
        var attr = ObjectDefinition.GetAttribute(method, "JsSymbolFunctionAttribute");
        if (attr is null || attr.ConstructorArguments.Length == 0) return null;
        var symbolName = attr.ConstructorArguments[0].Value as string;
        if (string.IsNullOrWhiteSpace(symbolName)) return null;
        var explicitLength = ObjectDefinition.GetNamedArg(attr, "Length") as int? ?? -1;
        var flagsExplicit = ObjectDefinition.HasNamedArg(attr, "Flags")
            ? ObjectDefinition.GetNamedArg(attr, "Flags") as int?
            : null;
        var flags = FlagExpression.From(flagsExplicit, "AllForbidden");
        // The JS-visible function name follows the ECMA convention "[Symbol.X]" with X = lowercase first letter.
        var functionName = "[Symbol." + ToCamelCase(symbolName!) + "]";
        return BuildCommon(method, pc, diagnostics, symbolName!, functionName, explicitLength, RegistrationKind.SymbolFunction, flags);
    }

    public static FunctionDefinition? FromJsSymbolAccessor(IMethodSymbol method, ParseContext pc, List<DiagnosticInfo> diagnostics)
    {
        var attr = ObjectDefinition.GetAttribute(method, "JsSymbolAccessorAttribute");
        if (attr is null || attr.ConstructorArguments.Length == 0) return null;
        var symbolName = attr.ConstructorArguments[0].Value as string;
        if (string.IsNullOrWhiteSpace(symbolName)) return null;
        var kindEnumValue = attr.ConstructorArguments.Length > 1 ? (attr.ConstructorArguments[1].Value as int? ?? 0) : 0;
        var registration = kindEnumValue == 1 ? RegistrationKind.SymbolAccessorSet : RegistrationKind.SymbolAccessorGet;
        // Default symbol-accessor flags mirror [JsAccessor]: Configurable, non-enumerable (EnumerableSet bit set).
        var flagsExplicit = ObjectDefinition.HasNamedArg(attr, "Flags")
            ? ObjectDefinition.GetNamedArg(attr, "Flags") as int?
            : (int?) 18;
        var flags = FlagExpression.From(flagsExplicit, "Configurable");
        var expectedValueParams = registration == RegistrationKind.SymbolAccessorSet ? 1 : 0;
        // The JS-visible function name follows the "get [Symbol.X]" / "set [Symbol.X]" convention.
        var functionName = (registration == RegistrationKind.SymbolAccessorSet ? "set [Symbol." : "get [Symbol.") + ToCamelCase(symbolName!) + "]";
        var fn = BuildCommon(method, pc, diagnostics, symbolName!, functionName, expectedValueParams, registration, flags);
        if (fn is null) return null;

        // JINT019: validate the actual value-parameter count matches the accessor kind.
        var actualValueParams = 0;
        foreach (var p in fn.Parameters)
        {
            if (p.Kind != ParameterKind.ThisObject && p.Kind != ParameterKind.AllArguments && p.Kind != ParameterKind.Rest)
            {
                actualValueParams++;
            }
        }
        if (actualValueParams != expectedValueParams)
        {
            var detail = expectedValueParams == 0
                ? $"getter must take no value parameters, has {actualValueParams}"
                : $"setter must take exactly one value parameter, has {actualValueParams}";
            diagnostics.Add(new DiagnosticInfo(
                DiagnosticDescriptors.InvalidAccessorSignature,
                method.Locations.FirstOrDefault(),
                registration == RegistrationKind.SymbolAccessorSet ? "set" : "get",
                method.Name,
                method.ContainingType.Name,
                detail));
            return null;
        }

        return fn;
    }

    private static FunctionDefinition? BuildCommon(IMethodSymbol method, ParseContext pc, List<DiagnosticInfo> diagnostics, string jsName, string functionName, int explicitLength, RegistrationKind registration, string flagsExpression)
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

        var parameters = new List<ParameterDefinition>();
        var valuePosition = 0;

        for (var i = 0; i < method.Parameters.Length; i++)
        {
            var param = method.Parameters[i];
            var isLast = i == method.Parameters.Length - 1;
            var isFirst = i == 0;

            if (isFirst && param.Name is "thisObject" or "thisObj")
            {
                // JsValue → forward as-is. JsValue subtype OR ICallable → cast (with TypeError on mismatch).
                var typeName = param.Type.ToDisplayString();
                if (typeName == "Jint.Native.JsValue")
                {
                    parameters.Add(new ParameterDefinition(ParameterKind.ThisObject, 0));
                    continue;
                }
                if (pc.IsJsValueOrSubtype(param.Type) || typeName == "Jint.Native.ICallable")
                {
                    parameters.Add(new ParameterDefinition(ParameterKind.ThisObject, 0, typeName));
                    continue;
                }
                diagnostics.Add(new DiagnosticInfo(
                    DiagnosticDescriptors.UnsupportedParameterType,
                    param.Locations.FirstOrDefault(),
                    param.Name, method.Name, typeName));
                return null;
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
                if (!ParameterTypeMatches(param.Type, kind))
                {
                    var expected = ExpectedConversionType(kind);
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

        var requireOc = ObjectDefinition.HasAttribute(method, "RequireObjectCoercibleAttribute");

        return new FunctionDefinition(
            ClrName: method.Name,
            JsName: jsName,
            FunctionName: functionName,
            Length: length,
            IsStatic: method.IsStatic,
            Parameters: parameters.ToEquatableArray(),
            Registration: registration,
            FlagsExpression: flagsExpression,
            RequireObjectCoercible: requireOc);
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
                "ToIntegerAttribute" => ParameterKind.ToInteger,
                "ToLengthAttribute" => ParameterKind.ToLength,
                "ToStringAttribute" => ParameterKind.ToString,
                "ToJsStringAttribute" => ParameterKind.ToJsString,
                "ToObjectAttribute" => ParameterKind.ToObject,
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

    private static (string csharpName, string attrName) ExpectedConversionType(ParameterKind kind) => kind switch
    {
        ParameterKind.ToNumber => ("double", "ToNumber"),
        ParameterKind.ToInt32 => ("int", "ToInt32"),
        ParameterKind.ToUint32 => ("uint", "ToUint32"),
        ParameterKind.ToInteger => ("double", "ToInteger"),
        ParameterKind.ToLength => ("ulong", "ToLength"),
        ParameterKind.ToString => ("string", "ToString"),
        ParameterKind.ToJsString => ("Jint.Native.JsString", "ToJsString"),
        ParameterKind.ToObject => ("Jint.Native.Object.ObjectInstance", "ToObject"),
        _ => (string.Empty, string.Empty),
    };

    private static bool ParameterTypeMatches(ITypeSymbol paramType, ParameterKind kind) => kind switch
    {
        ParameterKind.ToNumber => paramType.SpecialType == SpecialType.System_Double,
        ParameterKind.ToInt32 => paramType.SpecialType == SpecialType.System_Int32,
        ParameterKind.ToUint32 => paramType.SpecialType == SpecialType.System_UInt32,
        ParameterKind.ToInteger => paramType.SpecialType == SpecialType.System_Double,
        ParameterKind.ToLength => paramType.SpecialType == SpecialType.System_UInt64,
        ParameterKind.ToString => paramType.SpecialType == SpecialType.System_String,
        ParameterKind.ToJsString => paramType.ToDisplayString() == "Jint.Native.JsString",
        ParameterKind.ToObject => paramType.ToDisplayString() == "Jint.Native.Object.ObjectInstance",
        _ => false,
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
