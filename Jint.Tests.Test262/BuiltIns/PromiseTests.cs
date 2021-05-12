using Xunit;

namespace Jint.Tests.Test262.BuiltIns
{
    public class PromiseTests : Test262Test
    {
        [Theory(DisplayName = "built-ins\\Promise")]
        [MemberData(nameof(SourceFiles), "built-ins\\Promise", false)]
        [MemberData(nameof(SourceFiles), "built-ins\\Promise", true, Skip = "Skipped")]
        protected void Promise(SourceFile sourceFile)
        {
            RunTestInternal(sourceFile, withEventLoop: true);
        }
    }
}