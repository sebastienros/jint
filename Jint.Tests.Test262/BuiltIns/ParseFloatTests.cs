using Xunit;

namespace Jint.Tests.Test262.BuiltIns
{
    public class ParseFloatTests : Test262Test
    {
        [Theory(DisplayName = "built-ins\\parseFloat")]
        [MemberData(nameof(SourceFiles), "built-ins\\parseFloat", false)]
        [MemberData(nameof(SourceFiles), "built-ins\\parseFloat", true, Skip = "Skipped")]
        protected void RunTest(SourceFile sourceFile)
        {
            RunTestInternal(sourceFile);
        }
    }
}