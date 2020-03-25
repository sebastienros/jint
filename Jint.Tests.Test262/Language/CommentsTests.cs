using Xunit;

namespace Jint.Tests.Test262.Language
{
    public class CommentsTests : Test262Test
    {
        [Theory(DisplayName = "language\\comments")]
        [MemberData(nameof(SourceFiles), "language\\comments", false)]
        [MemberData(nameof(SourceFiles), "language\\comments", true, Skip = "Skipped")]
        protected void Comments(SourceFile sourceFile)
        {
            RunTestInternal(sourceFile);
        }
    }
}