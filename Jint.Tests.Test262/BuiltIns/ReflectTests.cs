using Xunit;

namespace Jint.Tests.Test262.BuiltIns
{
    public class ReflectTests : Test262Test
    {
        [Theory(DisplayName = "built-ins\\Reflect")]
        [MemberData(nameof(SourceFiles), "built-ins\\Reflect", false)]
        [MemberData(nameof(SourceFiles), "built-ins\\Reflect", true, Skip = "Skipped")]
        protected void RunTest(SourceFile sourceFile)
        {
            RunTestInternal(sourceFile);
        }
    }
}