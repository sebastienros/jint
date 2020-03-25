using Xunit;

namespace Jint.Tests.Test262.Language.Statements
{
    public class ForTests : Test262Test
    {
        [Theory(DisplayName = "language\\statements\\for")]
        [MemberData(nameof(SourceFiles), "language\\statements\\for", false)]
        [MemberData(nameof(SourceFiles), "language\\statements\\for", true, Skip = "Skipped")]
        protected void For(SourceFile sourceFile)
        {
            RunTestInternal(sourceFile);
        }
    }
}