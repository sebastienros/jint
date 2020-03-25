using Xunit;

namespace Jint.Tests.Test262.BuiltIns
{
    public class SymbolTests : Test262Test
    {
        [Theory(DisplayName = "built-ins\\Symbol")]
        [MemberData(nameof(SourceFiles), "built-ins\\Symbol", false)]
        [MemberData(nameof(SourceFiles), "built-ins\\Symbol", true, Skip = "Skipped")]
        protected void Symbol(SourceFile sourceFile)
        {
            RunTestInternal(sourceFile);
        }
    }
}