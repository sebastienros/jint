using Xunit;

namespace Jint.Tests.Test262.BuiltIns
{
    public class IsNaNTests : Test262Test
    {
        [Theory(DisplayName = "built-ins\\isNaN")]
        [MemberData(nameof(SourceFiles), "built-ins\\isNaN", false)]
        [MemberData(nameof(SourceFiles), "built-ins\\isNaN", true, Skip = "Skipped")]
        protected void RunTest(SourceFile sourceFile)
        {
            RunTestInternal(sourceFile);
        }
    }
}