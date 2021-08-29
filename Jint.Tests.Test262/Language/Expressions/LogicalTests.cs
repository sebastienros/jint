using Xunit;

namespace Jint.Tests.Test262.Language.Expressions
{
    public class LogicalTests : Test262Test
    {
        [Theory(DisplayName = "language\\expressions\\logical-and")]
        [MemberData(nameof(SourceFiles), "language\\expressions\\logical-and", false)]
        [MemberData(nameof(SourceFiles), "language\\expressions\\logical-and", true, Skip = "Skipped")]
        protected void LogicalAnd(SourceFile sourceFile)
        {
            RunTestInternal(sourceFile);
        }

        [Theory(DisplayName = "language\\expressions\\logical-assignment")]
        [MemberData(nameof(SourceFiles), "language\\expressions\\logical-assignment", false)]
        [MemberData(nameof(SourceFiles), "language\\expressions\\logical-assignment", true, Skip = "Skipped")]
        protected void LogicalAssignment(SourceFile sourceFile)
        {
            RunTestInternal(sourceFile);
        }

        [Theory(DisplayName = "language\\expressions\\logical-not")]
        [MemberData(nameof(SourceFiles), "language\\expressions\\logical-not", false)]
        [MemberData(nameof(SourceFiles), "language\\expressions\\logical-not", true, Skip = "Skipped")]
        protected void LogicalNot(SourceFile sourceFile)
        {
            RunTestInternal(sourceFile);
        }

        [Theory(DisplayName = "language\\expressions\\logical-or")]
        [MemberData(nameof(SourceFiles), "language\\expressions\\logical-or", false)]
        [MemberData(nameof(SourceFiles), "language\\expressions\\logical-or", true, Skip = "Skipped")]
        protected void LogicalOr(SourceFile sourceFile)
        {
            RunTestInternal(sourceFile);
        }
    }
}