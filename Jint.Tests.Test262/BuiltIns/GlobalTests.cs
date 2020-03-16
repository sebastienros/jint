using Xunit;

namespace Jint.Tests.Test262.BuiltIns
{
    public class GlobalTests : Test262Test
    {
        [Theory(DisplayName = "built-ins\\global")]
        [MemberData(nameof(SourceFiles), "built-ins\\global", false)]
        [MemberData(nameof(SourceFiles), "built-ins\\global", true, Skip = "Skipped")]
        protected void RunTest(SourceFile sourceFile)
        {
            RunTestInternal(sourceFile);
        }
    }
}