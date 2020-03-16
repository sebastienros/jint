using Xunit;

namespace Jint.Tests.Test262.Language
{
    public class RestParametersTests : Test262Test
    {
        [Theory(DisplayName = "language\\rest-parameters")]
        [MemberData(nameof(SourceFiles), "language\\rest-parameters", false)]
        [MemberData(nameof(SourceFiles), "language\\rest-parameters", true, Skip = "Skipped")]
        protected void RestParameters(SourceFile sourceFile)
        {
            RunTestInternal(sourceFile);
        }
    }
}