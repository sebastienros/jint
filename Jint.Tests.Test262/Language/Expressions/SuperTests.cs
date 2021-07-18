using Xunit;

namespace Jint.Tests.Test262.Language.Expressions
{
    public class SuperTests : Test262Test
    {
        [Theory(DisplayName = "language\\expressions\\super")]
        [MemberData(nameof(SourceFiles), "language\\expressions\\super", false)]
        [MemberData(nameof(SourceFiles), "language\\expressions\\super", true, Skip = "Skipped")]
        protected void Super(SourceFile sourceFile)
        {
            RunTestInternal(sourceFile);
        }
    }
}