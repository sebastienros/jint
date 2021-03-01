using Xunit;

namespace Jint.Tests.Test262.BuiltIns.AnnexB
{
    public class UnescapeTests : Test262Test
    {
        [Theory(DisplayName = "annexB\\built-ins\\unescape")]
        [MemberData(nameof(SourceFiles), "annexB\\built-ins\\unescape", false)]
        [MemberData(nameof(SourceFiles), "annexB\\built-ins\\unescape", true, Skip = "Skipped")]
        protected void RunTest(SourceFile sourceFile)
        {
            RunTestInternal(sourceFile);
        }
    }
}