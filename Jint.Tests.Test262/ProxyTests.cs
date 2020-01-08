using Xunit;

namespace Jint.Tests.Test262
{
    public class ProxyTests : Test262Test
    {
        [Theory(DisplayName = "built-ins\\Proxy")]
        [MemberData(nameof(SourceFiles), "built-ins\\Proxy", false)]
        [MemberData(nameof(SourceFiles), "built-ins\\Proxy", true, Skip = "Skipped")]
        protected void RunTest(SourceFile sourceFile)
        {
            RunTestInternal(sourceFile);
        }
    }
}