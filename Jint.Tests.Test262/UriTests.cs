using Xunit;

namespace Jint.Tests.Test262
{
    public class UriTests : Test262Test
    {
        [Theory(DisplayName = "built-ins\\decodeURI")]
        [MemberData(nameof(SourceFiles), "built-ins\\decodeURI", false)]
        [MemberData(nameof(SourceFiles), "built-ins\\decodeURI", true, Skip = "Skipped")]
        protected void DecodeUri(SourceFile sourceFile)
        {
            RunTestInternal(sourceFile);
        }

        [Theory(DisplayName = "built-ins\\decodeURIComponent")]
        [MemberData(nameof(SourceFiles), "built-ins\\decodeURIComponent", false)]
        [MemberData(nameof(SourceFiles), "built-ins\\decodeURIComponent", true, Skip = "Skipped")]
        protected void DecodeUriComponent(SourceFile sourceFile)
        {
            RunTestInternal(sourceFile);
        }

        [Theory(DisplayName = "built-ins\\encodeURI")]
        [MemberData(nameof(SourceFiles), "built-ins\\encodeURI", false)]
        [MemberData(nameof(SourceFiles), "built-ins\\encodeURI", true, Skip = "Skipped")]
        protected void EncodeUri(SourceFile sourceFile)
        {
            RunTestInternal(sourceFile);
        }

        [Theory(DisplayName = "built-ins\\encodeURIComponent")]
        [MemberData(nameof(SourceFiles), "built-ins\\encodeURIComponent", false)]
        [MemberData(nameof(SourceFiles), "built-ins\\encodeURIComponent", true, Skip = "Skipped")]
        protected void EncodeUriComponent(SourceFile sourceFile)
        {
            RunTestInternal(sourceFile);
        }
    }
}