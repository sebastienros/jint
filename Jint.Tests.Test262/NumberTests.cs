using Xunit;

namespace Jint.Tests.Test262
{
    public class NumberTests : Test262Test
    {
        [Theory(DisplayName = "built-ins\\Number")]
        [MemberData(nameof(SourceFiles), "built-ins\\Number", false)]
        [MemberData(nameof(SourceFiles), "built-ins\\Number", true, Skip = "Skipped")]
        protected void RunTest(SourceFile sourceFile)
        {
            RunTestInternal(sourceFile);
        }
    }
}