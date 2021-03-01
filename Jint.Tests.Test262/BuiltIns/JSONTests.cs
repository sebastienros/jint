using Xunit;

namespace Jint.Tests.Test262.BuiltIns
{
    public class JSONTests : Test262Test
    {
        [Theory(DisplayName = "built-ins\\JSON")]
        [MemberData(nameof(SourceFiles), "built-ins\\JSON", false)]
        [MemberData(nameof(SourceFiles), "built-ins\\JSON", true, Skip = "Skipped")]
        protected void RunTest(SourceFile sourceFile)
        {
            RunTestInternal(sourceFile);
        }
    }
}