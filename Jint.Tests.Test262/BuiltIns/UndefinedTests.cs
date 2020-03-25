using Xunit;

namespace Jint.Tests.Test262.BuiltIns
{
    public class UndefinedTests : Test262Test
    {
        [Theory(DisplayName = "built-ins\\undefined")]
        [MemberData(nameof(SourceFiles), "built-ins\\undefined", false)]
        [MemberData(nameof(SourceFiles), "built-ins\\undefined", true, Skip = "Skipped")]
        protected void RunTest(SourceFile sourceFile)
        {
            RunTestInternal(sourceFile);
        }
    }
}