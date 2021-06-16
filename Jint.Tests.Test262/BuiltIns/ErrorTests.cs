using Xunit;

namespace Jint.Tests.Test262.BuiltIns
{
    public class ErrorTests : Test262Test
    {
        [Theory(DisplayName = "built-ins\\Error")]
        [MemberData(nameof(SourceFiles), "built-ins\\Error", false)]
        [MemberData(nameof(SourceFiles), "built-ins\\Error", true, Skip = "Skipped")]
        protected void EncodeUri(SourceFile sourceFile)
        {
            RunTestInternal(sourceFile);
        }
    }
}