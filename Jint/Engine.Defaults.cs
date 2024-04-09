namespace Jint;

public partial class Engine
{
    internal const bool FoldConstantsOnPrepareByDefault = true;

    internal static readonly ParserOptions BaseParserOptions = ParserOptions.Default with
    {
        Tolerant = true,

        // This should be kept sync with https://github.com/sebastienros/esprima-dotnet/blob/v3.0.4/test/Esprima.Tests/JavaScriptParserTests.cs#L14-L18
#if DEBUG
        MaxAssignmentDepth = 200,
#else
        MaxAssignmentDepth = 500,
#endif
    };
}
