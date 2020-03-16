using Xunit;

namespace Jint.Tests.Test262.Language.Expressions
{
    public class CallTests : Test262Test
    {
        [Theory(DisplayName = "language\\expressions\\call")]
        [MemberData(nameof(SourceFiles), "language\\expressions\\call", false)]
        [MemberData(nameof(SourceFiles), "language\\expressions\\call", true, Skip = "Skipped")]
        protected void Call(SourceFile sourceFile)
        {
            RunTestInternal(sourceFile);
        }
    }
}