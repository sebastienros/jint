using Xunit;

namespace Jint.Tests.Test262
{
    public class MathTests : Test262Test
    {
        [Theory(DisplayName = "built-ins\\Math\\trunc")]
        [MemberData(nameof(SourceFiles), "built-ins\\Math\\trunc", false)]
        [MemberData(nameof(SourceFiles), "built-ins\\Math\\trunc", true, Skip = "Skipped")]
        protected void Trunc(SourceFile sourceFile)
        {
            RunTestInternal(sourceFile);
        }

        [Theory(DisplayName = "built-ins\\Math\\sign")]
        [MemberData(nameof(SourceFiles), "built-ins\\Math\\sign", false)]
        [MemberData(nameof(SourceFiles), "built-ins\\Math\\sign", true, Skip = "Skipped")]
        protected void Sign(SourceFile sourceFile)
        {
            RunTestInternal(sourceFile);
        }
    }
}