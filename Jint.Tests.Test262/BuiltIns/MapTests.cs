using Xunit;

namespace Jint.Tests.Test262.BuiltIns
{
    public class MapTests : Test262Test
    {
        [Theory(DisplayName = "built-ins\\Map")]
        [MemberData(nameof(SourceFiles), "built-ins\\Map", false)]
        [MemberData(nameof(SourceFiles), "built-ins\\Map", true, Skip = "Skipped")]
        protected void RunTest(SourceFile sourceFile)
        {
            RunTestInternal(sourceFile);
        }
    }
}