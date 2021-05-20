using Xunit;

namespace Jint.Tests.Test262.Language.Expressions
{
    public class YieldTests : Test262Test
    {
        [Theory(DisplayName = "language\\expressions\\yield")]
        [MemberData(nameof(SourceFiles), "language\\expressions\\yield", false)]
        [MemberData(nameof(SourceFiles), "language\\expressions\\yield", true, Skip = "Skipped")]
        protected void New(SourceFile sourceFile)
        {
            RunTestInternal(sourceFile);
        }
    }
}