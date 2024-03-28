namespace Jint;

public partial class Engine
{
    internal const bool FoldConstantsOnPrepareByDefault = true;

    internal static readonly ParserOptions BaseParserOptions = ParserOptions.Default with
    {
        EcmaVersion = EcmaVersion.ES2023,
        ExperimentalESFeatures = ExperimentalESFeatures.ImportAttributes | ExperimentalESFeatures.RegExpDuplicateNamedCapturingGroups,
        Tolerant = false,
    };
}
