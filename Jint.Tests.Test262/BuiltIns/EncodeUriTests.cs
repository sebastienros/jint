using Xunit;

namespace Jint.Tests.Test262.BuiltIns
{
    public class EncodeUriTests : Test262Test
    {
        [Theory(DisplayName = "built-ins\\encodeURI")]
        [MemberData(nameof(SourceFiles), "built-ins\\encodeURI", false)]
        [MemberData(nameof(SourceFiles), "built-ins\\encodeURI", true, Skip = "Skipped")]
        protected void EncodeUri(SourceFile sourceFile)
        {
            RunTestInternal(sourceFile);
        }
    }
}