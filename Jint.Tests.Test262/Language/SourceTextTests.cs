using Xunit;

namespace Jint.Tests.Test262.Language
{
    public class SourceTextTests : Test262Test
    {

        [Theory(DisplayName = "language\\source-text")]
        [MemberData(nameof(SourceFiles), "language\\source-text", false)]
        [MemberData(nameof(SourceFiles), "language\\source-text", true, Skip = "Skipped")]
        protected void SourceText(SourceFile sourceFile)
        {
            RunTestInternal(sourceFile);
        }
    }
}