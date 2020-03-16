using Xunit;

namespace Jint.Tests.Test262.Language.Statements
{
    public class ForInTests : Test262Test
    {
        [Theory(DisplayName = "language\\statements\\for-in")]
        [MemberData(nameof(SourceFiles), "language\\statements\\for-in", false)]
        [MemberData(nameof(SourceFiles), "language\\statements\\for-in", true, Skip = "Skipped")]
        protected void ForIn(SourceFile sourceFile)
        {
            RunTestInternal(sourceFile);
        }
    }
}