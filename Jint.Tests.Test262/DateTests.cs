using Xunit;

namespace Jint.Tests.Test262
{
    public class DateTests : Test262Test
    {
        [Theory(DisplayName = "built-ins\\Date")]
        [MemberData(nameof(SourceFiles), "built-ins\\Date", false)]
        [MemberData(nameof(SourceFiles), "built-ins\\Date", true, Skip = "Skipped")]
        protected void RunTest(SourceFile sourceFile)
        {
            RunTestInternal(sourceFile);
        }
    }
}