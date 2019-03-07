using Xunit;

namespace Jint.Tests.Test262
{
    public class StatementTests : Test262Test
    {
        [Theory(DisplayName = "language\\statements\\for")]
        [MemberData(nameof(SourceFiles), "language\\statements\\for", false)]
        [MemberData(nameof(SourceFiles), "language\\statements\\for", true, Skip = "Skipped")]
        protected void For(SourceFile sourceFile)
        {
            RunTestInternal(sourceFile);
        }

        [Theory(DisplayName = "language\\statements\\for-in")]
        [MemberData(nameof(SourceFiles), "language\\statements\\for-in", false)]
        [MemberData(nameof(SourceFiles), "language\\statements\\for-in", true, Skip = "Skipped")]
        protected void ForIn(SourceFile sourceFile)
        {
            RunTestInternal(sourceFile);
        }

        [Theory(Skip = "for of not implemented", DisplayName = "language\\statements\\for-of")]
        [MemberData(nameof(SourceFiles), "language\\statements\\for-of", false)]
        [MemberData(nameof(SourceFiles), "language\\statements\\for-of", true, Skip = "Skipped")]
        protected void ForOf(SourceFile sourceFile)
        {
            RunTestInternal(sourceFile);
        }
    }
}