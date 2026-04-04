namespace Jint;

public partial class Engine
{
    internal const bool FoldConstantsOnPrepareByDefault = true;

    internal static readonly ParserOptions BaseParserOptions = ParserOptions.Default with
    {
        EcmaVersion = EcmaVersion.ES2024,
        ExperimentalESFeatures = ExperimentalESFeatures.ImportAttributes
                                 | ExperimentalESFeatures.RegExpDuplicateNamedCapturingGroups
                                 | ExperimentalESFeatures.RegExpModifiers
                                 | ExperimentalESFeatures.ExplicitResourceManagement
                                 | ExperimentalESFeatures.Decorators,
        // Validate regex syntax without converting to .NET Regex.
        // Conversion is done at runtime in RegExpInitialize/JintLiteralExpression
        // with fallback to the custom engine for patterns .NET can't handle.
        RegExpParseMode = RegExpParseMode.Validate,
        Tolerant = false,
    };
}
