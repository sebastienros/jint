using Xunit;

namespace Jint.Tests.Test262.Language.Expressions
{
    public class OptionalChaining : Test262Test
    {
        [Theory(DisplayName = "language\\expressions\\optional-chaining")]
        [MemberData(nameof(SourceFiles), "language\\expressions\\optional-chaining", false)]
        [MemberData(nameof(SourceFiles), "language\\expressions\\optional-chaining", true, Skip = "Skipped")]
        protected void Chaining(SourceFile sourceFile)
        {
            RunTestInternal(sourceFile);
        }
    }
}