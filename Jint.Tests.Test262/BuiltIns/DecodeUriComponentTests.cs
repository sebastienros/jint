using Xunit;

namespace Jint.Tests.Test262.BuiltIns
{
    public class DecodeUriComponentTests : Test262Test
    {
        [Theory(DisplayName = "built-ins\\decodeURIComponent")]
        [MemberData(nameof(SourceFiles), "built-ins\\decodeURIComponent", false)]
        [MemberData(nameof(SourceFiles), "built-ins\\decodeURIComponent", true, Skip = "Skipped")]
        protected void DecodeUriComponent(SourceFile sourceFile)
        {
            RunTestInternal(sourceFile);
        }
    }
}