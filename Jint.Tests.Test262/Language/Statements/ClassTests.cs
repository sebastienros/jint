using Xunit;

namespace Jint.Tests.Test262.Language.Statements
{
    public class ClassTests : Test262Test
    {
        [Theory(DisplayName = "language\\statements\\class")]
        [MemberData(nameof(SourceFiles), "language\\statements\\class", false)]
        [MemberData(nameof(SourceFiles), "language\\statements\\class", true, Skip = "Skipped")]
        protected void Class(SourceFile sourceFile)
        {
            RunTestInternal(sourceFile);
        }
    }
}