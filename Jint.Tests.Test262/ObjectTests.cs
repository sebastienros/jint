using Xunit;

namespace Jint.Tests.Test262
{
    public class ObjectTests : Test262Test
    {
        [Theory(DisplayName = "built-ins\\Object")]
        [MemberData(nameof(SourceFiles), "built-ins\\Object", false)]
        [MemberData(nameof(SourceFiles), "built-ins\\Object", true, Skip = "Skipped")]
        protected void RunTest(SourceFile sourceFile)
        {
            RunTestInternal(sourceFile);
        }
    }
}