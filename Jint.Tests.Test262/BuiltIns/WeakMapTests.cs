using Xunit;

namespace Jint.Tests.Test262.BuiltIns
{
    public class WeakMapTests : Test262Test
    {
        [Theory(DisplayName = "built-ins\\WeakMap")]
        [MemberData(nameof(SourceFiles), "built-ins\\WeakMap", false)]
        [MemberData(nameof(SourceFiles), "built-ins\\WeakMap", true, Skip = "Skipped")]
        protected void RunTest(SourceFile sourceFile)
        {
            RunTestInternal(sourceFile);
        }
    }
}