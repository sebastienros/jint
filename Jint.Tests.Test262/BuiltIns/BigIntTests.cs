using Xunit;

namespace Jint.Tests.Test262.BuiltIns
{
    public class BigIntTests : Test262Test
    {
        [Theory(DisplayName = "built-ins\\BigInt")]
        [MemberData(nameof(SourceFiles), "built-ins\\BigInt", false)]
        [MemberData(nameof(SourceFiles), "built-ins\\BigInt", true, Skip = "Skipped")]
        protected void RunTest(SourceFile sourceFile)
        {
            RunTestInternal(sourceFile);
        }
    }
}