using Xunit;

namespace Jint.Tests.Test262
{
    public class ExpressionTests : Test262Test
    {
        [Theory(DisplayName = "language\\expressions\\array")]
        [MemberData(nameof(SourceFiles), "language\\expressions\\array", false)]
        [MemberData(nameof(SourceFiles), "language\\expressions\\array", true, Skip = "Skipped")]
        protected void Array(SourceFile sourceFile)
        {
            RunTestInternal(sourceFile);
        }

        [Theory(DisplayName = "language\\expressions\\call")]
        [MemberData(nameof(SourceFiles), "language\\expressions\\call", false)]
        [MemberData(nameof(SourceFiles), "language\\expressions\\call", true, Skip = "Skipped")]
        protected void Call(SourceFile sourceFile)
        {
            RunTestInternal(sourceFile);
        }

        [Theory(DisplayName = "language\\expressions\\new")]
        [MemberData(nameof(SourceFiles), "language\\expressions\\new", false)]
        [MemberData(nameof(SourceFiles), "language\\expressions\\new", true, Skip = "Skipped")]
        protected void New(SourceFile sourceFile)
        {
            RunTestInternal(sourceFile);
        }
    }
}