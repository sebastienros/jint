using Xunit;

namespace Jint.Tests.Test262.BuiltIns
{
    public class ParseIntTests : Test262Test
    {
        [Theory(DisplayName = "built-ins\\parseInt")]
        [MemberData(nameof(SourceFiles), "built-ins\\parseInt", false)]
        [MemberData(nameof(SourceFiles), "built-ins\\parseInt", true, Skip = "Skipped")]
        protected void RunTest(SourceFile sourceFile)
        {
            RunTestInternal(sourceFile);
        }
    }
}