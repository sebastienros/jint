using Xunit;

namespace Jint.Tests.Test262.Language
{
    public class FunctionCodeTests : Test262Test
    {
        [Theory(DisplayName = "language\\function-code")]
        [MemberData(nameof(SourceFiles), "language\\function-code", false)]
        [MemberData(nameof(SourceFiles), "language\\function-code", true, Skip = "Skipped")]
        protected void FunctionCode(SourceFile sourceFile)
        {
            RunTestInternal(sourceFile);
        }
    }
}