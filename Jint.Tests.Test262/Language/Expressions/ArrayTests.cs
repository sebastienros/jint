using Xunit;

namespace Jint.Tests.Test262.Language.Expressions
{
    public class ArrayTests : Test262Test
    {
        [Theory(DisplayName = "language\\expressions\\array")]
        [MemberData(nameof(SourceFiles), "language\\expressions\\array", false)]
        [MemberData(nameof(SourceFiles), "language\\expressions\\array", true, Skip = "Skipped")]
        protected void Array(SourceFile sourceFile)
        {
            RunTestInternal(sourceFile);
        }
    }
}