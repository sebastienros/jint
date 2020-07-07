using Xunit;

namespace Jint.Tests.Test262.Language
{
    public class GlobalCodeTests : Test262Test
    {
        [Theory(DisplayName = "language\\global-code")]
        [MemberData(nameof(SourceFiles), "language\\global-code", false)]
        [MemberData(nameof(SourceFiles), "language\\global-code", true, Skip = "Skipped")]
        protected void GlobalCode(SourceFile sourceFile)
        {
            RunTestInternal(sourceFile);
        }
    }
}