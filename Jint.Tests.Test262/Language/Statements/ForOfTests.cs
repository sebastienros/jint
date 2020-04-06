using Xunit;

namespace Jint.Tests.Test262.Language.Statements
{
    public class ForOfTests : Test262Test
    {
        [Theory(DisplayName = "language\\statements\\for-of")]
        [MemberData(nameof(SourceFiles), "language\\statements\\for-of", false)]
        [MemberData(nameof(SourceFiles), "language\\statements\\for-of", true, Skip = "Skipped")]
        protected void ForOf(SourceFile sourceFile)
        {
            RunTestInternal(sourceFile);
        }
    }
}