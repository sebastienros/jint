using Xunit;

namespace Jint.Tests.Test262
{
    public class MathTests : Test262Test
    {
        [Theory(DisplayName = "built-ins\\Math")]
        [MemberData(nameof(SourceFiles), "built-ins\\Math", false)]
        [MemberData(nameof(SourceFiles), "built-ins\\Math", true, Skip = "Skipped")]
        protected void Math(SourceFile sourceFile)
        {
            RunTestInternal(sourceFile);
        }
    }
}