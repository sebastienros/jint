using Xunit;

namespace Jint.Tests.Test262
{
    public class LanguageTests : Test262Test
    {
        [Theory(DisplayName = "language\\rest-parameters")]
        [MemberData(nameof(SourceFiles), "language\\rest-parameters", false)]
        [MemberData(nameof(SourceFiles), "language\\rest-parameters", true, Skip = "Skipped")]
        protected void RestParameters(SourceFile sourceFile)
        {
            RunTestInternal(sourceFile);
        }

        [Theory(DisplayName = "language\\expressions\\array")]
        [MemberData(nameof(SourceFiles), "language\\expressions\\array", false)]
        [MemberData(nameof(SourceFiles), "language\\expressions\\array", true, Skip = "Skipped")]
        protected void ExpressionsArray(SourceFile sourceFile)
        {
            RunTestInternal(sourceFile);
        }

        [Theory(DisplayName = "language\\expressions\\call")]
        [MemberData(nameof(SourceFiles), "language\\expressions\\call", false)]
        [MemberData(nameof(SourceFiles), "language\\expressions\\call", true, Skip = "Skipped")]
        protected void ExpressionsCall(SourceFile sourceFile)
        {
            RunTestInternal(sourceFile);
        }

        [Theory(DisplayName = "language\\expressions\\new")]
        [MemberData(nameof(SourceFiles), "language\\expressions\\new", false)]
        [MemberData(nameof(SourceFiles), "language\\expressions\\new", true, Skip = "Skipped")]
        protected void ExpressionsNew(SourceFile sourceFile)
        {
            RunTestInternal(sourceFile);
        }
    }
}