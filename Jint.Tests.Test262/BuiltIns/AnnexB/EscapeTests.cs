using Xunit;

namespace Jint.Tests.Test262.BuiltIns.AnnexB
{
    public class EscapeTests : Test262Test
    {
        [Theory(DisplayName = "annexB\\built-ins\\escape")]
        [MemberData(nameof(SourceFiles), "annexB\\built-ins\\escape", false)]
        [MemberData(nameof(SourceFiles), "annexB\\built-ins\\escape", true, Skip = "Skipped")]
        protected void RunTest(SourceFile sourceFile)
        {
            RunTestInternal(sourceFile);
        }
    }
}