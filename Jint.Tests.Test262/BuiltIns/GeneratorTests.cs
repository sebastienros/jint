using Xunit;

namespace Jint.Tests.Test262.BuiltIns
{
    public class GeneratorTests : Test262Test
    {
        [Theory(DisplayName = "built-ins\\GeneratorFunction", Skip = "TODO")]
        [MemberData(nameof(SourceFiles), "built-ins\\GeneratorFunction", false)]
        [MemberData(nameof(SourceFiles), "built-ins\\GeneratorFunction", true, Skip = "Skipped")]
        protected void GeneratorFunction(SourceFile sourceFile)
        {
            RunTestInternal(sourceFile);
        }

        [Theory(DisplayName = "built-ins\\GeneratorPrototype")]
        [MemberData(nameof(SourceFiles), "built-ins\\GeneratorPrototype", false)]
        [MemberData(nameof(SourceFiles), "built-ins\\GeneratorPrototype", true, Skip = "Skipped")]
        protected void GeneratorPrototype(SourceFile sourceFile)
        {
            RunTestInternal(sourceFile);
        }
    }
}