using Xunit;

namespace Jint.Tests.Test262.Language.Expressions
{
    public class ArrowFunctionTests : Test262Test
    {
        [Theory(DisplayName = "language\\expressions\\arrow-function")]
        [MemberData(nameof(SourceFiles), "language\\expressions\\arrow-function", false)]
        [MemberData(nameof(SourceFiles), "language\\expressions\\arrow-function", true, Skip = "Skipped")]
        protected void ArrowFunction(SourceFile sourceFile)
        {
            RunTestInternal(sourceFile);
        }
    }
}