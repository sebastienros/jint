using Xunit;

namespace Jint.Tests.Test262.Language.Expressions
{
    public class AdditionTests : Test262Test
    {
        [Theory(DisplayName = "language\\expressions\\addition")]
        [MemberData(nameof(SourceFiles), "language\\expressions\\addition", false)]
        [MemberData(nameof(SourceFiles), "language\\expressions\\addition", true, Skip = "Skipped")]
        protected void Addition(SourceFile sourceFile)
        {
            RunTestInternal(sourceFile);
        }
    }
}