using Xunit;

namespace Jint.Tests.Test262.Language.Expressions
{
    public class TemplateLiteralTests : Test262Test
    {
        [Theory(DisplayName = "language\\expressions\\template-literal")]
        [MemberData(nameof(SourceFiles), "language\\expressions\\template-literal", false)]
        [MemberData(nameof(SourceFiles), "language\\expressions\\template-literal", true, Skip = "Skipped")]
        protected void TemplateLiteral(SourceFile sourceFile)
        {
            RunTestInternal(sourceFile);
        }
    }
}