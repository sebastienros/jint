using Xunit;

namespace Jint.Tests.Test262.Language.Statements
{
    public class LetTests : Test262Test
    {
        [Theory(DisplayName = "language\\statements\\let")]
        [MemberData(nameof(SourceFiles), "language\\statements\\let", false)]
        [MemberData(nameof(SourceFiles), "language\\statements\\let", true, Skip = "Skipped")]
        protected void For(SourceFile sourceFile)
        {
            RunTestInternal(sourceFile);
        }
    }
}