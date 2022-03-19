using Xunit;

namespace Jint.Tests.Test262.Language.Expressions
{
    public class TypeOfTests : Test262Test
    {
        [Theory(DisplayName = "language\\expressions\\typeof")]
        [MemberData(nameof(SourceFiles), "language\\expressions\\typeof", false)]
        [MemberData(nameof(SourceFiles), "language\\expressions\\typeof", true, Skip = "Skipped")]
        protected void TemplateLiteral(SourceFile sourceFile)
        {
            RunTestInternal(sourceFile);
        }
    }
}