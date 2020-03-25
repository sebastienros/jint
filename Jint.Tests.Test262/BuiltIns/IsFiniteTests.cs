using Xunit;

namespace Jint.Tests.Test262.BuiltIns
{
    public class IsFiniteTests : Test262Test
    {
        [Theory(DisplayName = "built-ins\\isFinite")]
        [MemberData(nameof(SourceFiles), "built-ins\\isFinite", false)]
        [MemberData(nameof(SourceFiles), "built-ins\\isFinite", true, Skip = "Skipped")]
        protected void RunTest(SourceFile sourceFile)
        {
            RunTestInternal(sourceFile);
        }
    }
}