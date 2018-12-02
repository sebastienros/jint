using Xunit;

namespace Jint.Tests.Test262
{
    public class StringTests : Test262Test
    {
        [Theory(DisplayName = "built-ins\\String")]
        [MemberData(nameof(SourceFiles), "built-ins\\String", false)]
        [MemberData(nameof(SourceFiles), "built-ins\\String", true, Skip = "Skipped")]
        protected void RunTest(SourceFile sourceFile)
        {
            RunTestInternal(sourceFile);
        }
    }
}