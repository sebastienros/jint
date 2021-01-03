using Xunit;

namespace Jint.Tests.Test262.Language.Expressions
{
    public class ClassTests : Test262Test
    {
        [Theory(DisplayName = "language\\expressions\\class")]
        [MemberData(nameof(SourceFiles), "language\\expressions\\class", false)]
        [MemberData(nameof(SourceFiles), "language\\expressions\\class", true, Skip = "Skipped")]
        protected void Class(SourceFile sourceFile)
        {
            RunTestInternal(sourceFile);
        }
    }
}