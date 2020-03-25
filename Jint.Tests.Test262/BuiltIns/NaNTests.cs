using Xunit;

namespace Jint.Tests.Test262.BuiltIns
{
    public class NaNTests : Test262Test
    {
        [Theory(DisplayName = "built-ins\\NaN")]
        [MemberData(nameof(SourceFiles), "built-ins\\NaN", false)]
        [MemberData(nameof(SourceFiles), "built-ins\\NaN", true, Skip = "Skipped")]
        protected void RunTest(SourceFile sourceFile)
        {
            RunTestInternal(sourceFile);
        }
    }
}