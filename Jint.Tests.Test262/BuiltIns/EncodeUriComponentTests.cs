using Xunit;

namespace Jint.Tests.Test262.BuiltIns
{
    public class EncodeUriComponentTests : Test262Test
    {
        [Theory(DisplayName = "built-ins\\encodeURIComponent")]
        [MemberData(nameof(SourceFiles), "built-ins\\encodeURIComponent", false)]
        [MemberData(nameof(SourceFiles), "built-ins\\encodeURIComponent", true, Skip = "Skipped")]
        protected void EncodeUriComponent(SourceFile sourceFile)
        {
            RunTestInternal(sourceFile);
        }
    }
}