using Xunit;

namespace Jint.Tests.Test262.BuiltIns
{
    public class FunctionTests : Test262Test
    {
        [Theory(DisplayName = "built-ins\\Function")]
        [MemberData(nameof(SourceFiles), "built-ins\\Function", false)]
        [MemberData(nameof(SourceFiles), "built-ins\\Function", true, Skip = "Skipped")]
        protected void Function(SourceFile sourceFile)
        {
            RunTestInternal(sourceFile);
        }
    }
}