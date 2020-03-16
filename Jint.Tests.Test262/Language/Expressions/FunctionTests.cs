using Xunit;

namespace Jint.Tests.Test262.Language.Expressions
{
    public class FunctionTests : Test262Test
    {
        [Theory(DisplayName = "language\\expressions\\function")]
        [MemberData(nameof(SourceFiles), "language\\expressions\\function", false)]
        [MemberData(nameof(SourceFiles), "language\\expressions\\function", true, Skip = "Skipped")]
        protected void Function(SourceFile sourceFile)
        {
            RunTestInternal(sourceFile);
        }
    }
}