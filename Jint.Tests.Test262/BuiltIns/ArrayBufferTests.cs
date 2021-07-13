using Xunit;

namespace Jint.Tests.Test262.BuiltIns
{
    public class ArrayBufferTests : Test262Test
    {
        [Theory(DisplayName = "built-ins\\ArrayBuffer")]
        [MemberData(nameof(SourceFiles), "built-ins\\ArrayBuffer", false)]
        [MemberData(nameof(SourceFiles), "built-ins\\ArrayBuffer", true, Skip = "Skipped")]
        protected void RunTest(SourceFile sourceFile)
        {
            RunTestInternal(sourceFile);
        }
    }
}