using Xunit;

namespace Jint.Tests.Test262.Language
{
    public class ArgumentsObjectTests : Test262Test
    {
        [Theory(DisplayName = "language\\arguments-object")]
        [MemberData(nameof(SourceFiles), "language\\arguments-object", false)]
        [MemberData(nameof(SourceFiles), "language\\arguments-object", true, Skip = "Skipped")]
        protected void ArgumentsObject(SourceFile sourceFile)
        {
            RunTestInternal(sourceFile);
        }
    }
}