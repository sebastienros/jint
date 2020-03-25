using Xunit;

namespace Jint.Tests.Test262.Language.Expressions
{
    public class NewTests : Test262Test
    {
        [Theory(DisplayName = "language\\expressions\\new")]
        [MemberData(nameof(SourceFiles), "language\\expressions\\new", false)]
        [MemberData(nameof(SourceFiles), "language\\expressions\\new", true, Skip = "Skipped")]
        protected void New(SourceFile sourceFile)
        {
            RunTestInternal(sourceFile);
        }
    }
}