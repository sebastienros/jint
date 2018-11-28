using Xunit;

namespace Jint.Tests.Test262
{
    public class ArrayTests : Test262Test
    {
        [Theory(DisplayName = "built-ins\\Array")]
        [MemberData(nameof(SourceFiles), "built-ins\\Array", false)]
        [MemberData(nameof(SourceFiles), "built-ins\\Array", true, Skip = "Skipped")]
        protected void RunTest(SourceFile sourceFile)
        {
            RunTestInternal(sourceFile);
        }
    }
}