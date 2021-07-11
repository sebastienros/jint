using Xunit;

namespace Jint.Tests.Test262.BuiltIns
{
    public class MapTests : Test262Test
    {
        [Theory(DisplayName = "built-ins\\Map")]
        [MemberData(nameof(SourceFiles), "built-ins\\Map", false)]
        [MemberData(nameof(SourceFiles), "built-ins\\Map", true, Skip = "Skipped")]
        protected void Map(SourceFile sourceFile)
        {
            RunTestInternal(sourceFile);
        }

        [Theory(DisplayName = "built-ins\\MapIteratorPrototype")]
        [MemberData(nameof(SourceFiles), "built-ins\\MapIteratorPrototype", false)]
        [MemberData(nameof(SourceFiles), "built-ins\\MapIteratorPrototype", true, Skip = "Skipped")]
        protected void MapIteratorPrototype(SourceFile sourceFile)
        {
            RunTestInternal(sourceFile);
        }
    }
}