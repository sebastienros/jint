using Xunit;

namespace Jint.Tests.Test262
{
    public class BooleanTests : Test262Test
    {
        [Theory(DisplayName = "built-ins\\Boolean")]
        [MemberData(nameof(SourceFiles), "built-ins\\Boolean", false)]
        [MemberData(nameof(SourceFiles), "built-ins\\Boolean", true, Skip = "Skipped")]
        protected void RunTest(SourceFile sourceFile)
        {
            RunTestInternal(sourceFile);
        }
    }
}