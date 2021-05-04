using Xunit;

namespace Jint.Tests.Test262.Language.Expressions
{
    public class InstanceOfTests : Test262Test
    {
        [Theory(DisplayName = "language\\expressions\\instanceof")]
        [MemberData(nameof(SourceFiles), "language\\expressions\\instanceof", false)]
        [MemberData(nameof(SourceFiles), "language\\expressions\\instanceof", true, Skip = "Skipped")]
        protected void New(SourceFile sourceFile)
        {
            RunTestInternal(sourceFile);
        }
    }
}