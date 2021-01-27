using Xunit;

namespace Jint.Tests.Test262.Language.Expressions
{
    public class CoalesceTests : Test262Test
    {
        [Theory(DisplayName = "language\\expressions\\\\coalesce")]
        [MemberData(nameof(SourceFiles), "language\\expressions\\\\coalesce", false)]
        [MemberData(nameof(SourceFiles), "language\\expressions\\\\coalesce", true, Skip = "Skipped")]
        protected void Addition(SourceFile sourceFile)
        {
            RunTestInternal(sourceFile);
        }
    }
}