using Xunit;

namespace Jint.Tests.Test262.BuiltIns
{
    public class InfinityTests : Test262Test
    {
        [Theory(DisplayName = "built-ins\\Infinity")]
        [MemberData(nameof(SourceFiles), "built-ins\\Infinity", false)]
        [MemberData(nameof(SourceFiles), "built-ins\\Infinity", true, Skip = "Skipped")]
        protected void RunTest(SourceFile sourceFile)
        {
            RunTestInternal(sourceFile);
        }
    }
}