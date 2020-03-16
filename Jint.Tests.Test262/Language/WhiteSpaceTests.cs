using Xunit;

namespace Jint.Tests.Test262.Language
{
    public class WhiteSpaceTests : Test262Test
    {
        [Theory(DisplayName = "language\\white-space")]
        [MemberData(nameof(SourceFiles), "language\\white-space", false)]
        [MemberData(nameof(SourceFiles), "language\\white-space", true, Skip = "Skipped")]
        protected void WhiteSpace(SourceFile sourceFile)
        {
            RunTestInternal(sourceFile);
        }
    }
}