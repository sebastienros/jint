using Xunit;

namespace Jint.Tests.Test262.Language.Expressions
{
    public class AssignmentTests : Test262Test
    {
        [Theory(DisplayName = "language\\expressions\\assignment")]
        [MemberData(nameof(SourceFiles), "language\\expressions\\assignment", false)]
        [MemberData(nameof(SourceFiles), "language\\expressions\\assignment", true, Skip = "Skipped")]
        protected void Assignment(SourceFile sourceFile)
        {
            RunTestInternal(sourceFile);
        }
    }
}