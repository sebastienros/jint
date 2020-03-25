using Xunit;

namespace Jint.Tests.Test262.BuiltIns
{
    public class SetTests : Test262Test
    {
        [Theory(DisplayName = "built-ins\\Set")]
        [MemberData(nameof(SourceFiles), "built-ins\\Set", false)]
        [MemberData(nameof(SourceFiles), "built-ins\\Set", true, Skip = "Skipped")]
        protected void RunTest(SourceFile sourceFile)
        {
            RunTestInternal(sourceFile);
        }
    }
}