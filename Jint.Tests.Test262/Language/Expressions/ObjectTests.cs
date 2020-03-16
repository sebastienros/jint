using Xunit;

namespace Jint.Tests.Test262.Language.Expressions
{
    public class ObjectTests : Test262Test
    {
        [Theory(DisplayName = "language\\expressions\\object")]
        [MemberData(nameof(SourceFiles), "language\\expressions\\object", false)]
        [MemberData(nameof(SourceFiles), "language\\expressions\\object", true, Skip = "Skipped")]
        protected void Object(SourceFile sourceFile)
        {
            RunTestInternal(sourceFile);
        }
    }
}