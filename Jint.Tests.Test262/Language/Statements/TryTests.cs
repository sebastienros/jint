using Xunit;

namespace Jint.Tests.Test262.Language.Statements
{
    public class TryTests : Test262Test
    {
        [Theory(DisplayName = "language\\statements\\try")]
        [MemberData(nameof(SourceFiles), "language\\statements\\try", false)]
        [MemberData(nameof(SourceFiles), "language\\statements\\try", true, Skip = "Skipped")]
        protected void For(SourceFile sourceFile)
        {
            RunTestInternal(sourceFile);
        }
    }
}