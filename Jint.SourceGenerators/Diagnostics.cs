using Microsoft.CodeAnalysis;

namespace Jint.SourceGenerators;

internal static class DiagnosticDescriptors
{
    public static readonly DiagnosticDescriptor MustBePartial = new(
        id: "JINT001",
        title: "[JsObject] type must be partial",
        messageFormat: "Type '{0}' is annotated with [JsObject] and must be declared 'partial'",
        category: "Jint.SourceGenerators",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor MustNotBeNested = new(
        id: "JINT002",
        title: "[JsObject] type must not be nested",
        messageFormat: "Type '{0}' is annotated with [JsObject] and must be declared at namespace scope (not nested in another type)",
        category: "Jint.SourceGenerators",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor UnsupportedReturnType = new(
        id: "JINT010",
        title: "Unsupported [JsFunction] return type",
        messageFormat: "Method '{0}' annotated with [JsFunction] must return JsValue (or a JsValue subtype) but returns '{1}'",
        category: "Jint.SourceGenerators",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor UnsupportedParameterType = new(
        id: "JINT011",
        title: "Unsupported [JsFunction] parameter type",
        messageFormat: "Parameter '{0}' on method '{1}' has unsupported type '{2}' (only JsValue is supported)",
        category: "Jint.SourceGenerators",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor DuplicateJsName = new(
        id: "JINT012",
        title: "Duplicate [JsFunction] CLR method name",
        messageFormat: "Method '{0}' in '{1}' is annotated with [JsFunction] more than once or has overloads — only one [JsFunction] per CLR name is supported",
        category: "Jint.SourceGenerators",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor UnsupportedPropertyType = new(
        id: "JINT020",
        title: "Unsupported [JsProperty] member type",
        messageFormat: "Member '{0}' annotated with [JsProperty] must be a readonly JsValue-typed field or get-only property",
        category: "Jint.SourceGenerators",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);
}
