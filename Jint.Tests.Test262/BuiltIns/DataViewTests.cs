using Xunit;

namespace Jint.Tests.Test262.BuiltIns
{
    public class DataViewTests : Test262Test
    {
        [Theory(DisplayName = "built-ins\\DataView")]
        [MemberData(nameof(SourceFiles), "built-ins\\DataView", false)]
        [MemberData(nameof(SourceFiles), "built-ins\\DataView", true, Skip = "Skipped")]
        protected void RunTest(SourceFile sourceFile)
        {
            RunTestInternal(sourceFile);
        }
    }
}