using Xunit;

namespace Jint.Tests.Test262.Language
{
    public class TypesTests : Test262Test
    {
        [Theory(DisplayName = "language\\types")]
        [MemberData(nameof(SourceFiles), "language\\types", false)]
        [MemberData(nameof(SourceFiles), "language\\types", true, Skip = "Skipped")]
        protected void Types(SourceFile sourceFile)
        {
            RunTestInternal(sourceFile);
        }
    }
}