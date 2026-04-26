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

    public static readonly DiagnosticDescriptor ConversionTypeMismatch = new(
        id: "JINT013",
        title: "Conversion attribute requires a matching CLR parameter type",
        messageFormat: "Parameter '{0}' on method '{1}' has [{2}] but the CLR type is '{3}' — expected '{4}'",
        category: "Jint.SourceGenerators",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor ConflictingConversionAttributes = new(
        id: "JINT014",
        title: "Multiple conversion attributes on a single parameter",
        messageFormat: "Parameter '{0}' on method '{1}' has more than one of [ToNumber]/[ToInt32]/[ToUint32]/[Rest] — pick one",
        category: "Jint.SourceGenerators",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor DuplicateThrowerAccessor = new(
        id: "JINT016",
        title: "Duplicate [JsThrowerAccessor] name on the same class",
        messageFormat: "[JsThrowerAccessor(\"{0}\")] appears more than once on '{1}'",
        category: "Jint.SourceGenerators",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor ConflictingAccessorFlags = new(
        id: "JINT017",
        title: "[JsAccessor] get and set declare different Flags",
        messageFormat: "Accessor '{0}' on '{1}' has get and set with different Flags values — declare Flags only once or set them identically",
        category: "Jint.SourceGenerators",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor MissingRealmField = new(
        id: "JINT018",
        title: "Host using cast precondition needs an accessible _realm field",
        messageFormat: "Type '{0}' has [JsFunction] methods with non-JsValue 'thisObject' types but no accessible Realm-typed '_realm' field — derive from Function or declare 'private readonly Realm _realm;'",
        category: "Jint.SourceGenerators",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor InvalidAccessorSignature = new(
        id: "JINT019",
        title: "[JsAccessor] method has wrong arity",
        messageFormat: "[JsAccessor] {0} '{1}' on '{2}': {3}",
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
