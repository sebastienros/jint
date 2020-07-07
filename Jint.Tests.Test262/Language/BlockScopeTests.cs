using Xunit;

namespace Jint.Tests.Test262.Language
{
    public class BlockScopeTests : Test262Test
    {
        [Theory(DisplayName = "language\\block-scope")]
        [MemberData(nameof(SourceFiles), "language\\block-scope", false)]
        [MemberData(nameof(SourceFiles), "language\\block-scope", true, Skip = "Skipped")]
        protected void BlockScope(SourceFile sourceFile)
        {
            RunTestInternal(sourceFile);
        }
    }
}