using Xunit;

namespace Jint.Tests.Test262.BuiltIns
{
    public class WeakSetTests : Test262Test
    {
        [Theory(DisplayName = "built-ins\\WeakSet")]
        [MemberData(nameof(SourceFiles), "built-ins\\WeakSet", false)]
        [MemberData(nameof(SourceFiles), "built-ins\\WeakSet", true, Skip = "Skipped")]
        protected void RunTest(SourceFile sourceFile)
        {
            RunTestInternal(sourceFile);
        }
    }
}