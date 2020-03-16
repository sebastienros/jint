using Xunit;

namespace Jint.Tests.Test262.BuiltIns
{
    public class DecodeUriTests : Test262Test
    {
        [Theory(DisplayName = "built-ins\\decodeURI")]
        [MemberData(nameof(SourceFiles), "built-ins\\decodeURI", false)]
        [MemberData(nameof(SourceFiles), "built-ins\\decodeURI", true, Skip = "Skipped")]
        protected void DecodeUri(SourceFile sourceFile)
        {
            RunTestInternal(sourceFile);
        }
    }
}