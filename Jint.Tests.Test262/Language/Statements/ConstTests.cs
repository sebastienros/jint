using Xunit;

namespace Jint.Tests.Test262.Language.Statements
{
    public class ConstTests : Test262Test
    {
        [Theory(DisplayName = "language\\statements\\const")]
        [MemberData(nameof(SourceFiles), "language\\statements\\const", false)]
        [MemberData(nameof(SourceFiles), "language\\statements\\const", true, Skip = "Skipped")]
        protected void For(SourceFile sourceFile)
        {
            RunTestInternal(sourceFile);
        }
    }
}