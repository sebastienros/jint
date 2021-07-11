using Xunit;

namespace Jint.Tests.Test262.BuiltIns
{
    public class ArrayTests : Test262Test
    {
        [Theory(DisplayName = "built-ins\\Array")]
        [MemberData(nameof(SourceFiles), "built-ins\\Array", false)]
        [MemberData(nameof(SourceFiles), "built-ins\\Array", true, Skip = "Skipped")]
        protected void Array(SourceFile sourceFile)
        {
            RunTestInternal(sourceFile);
        }

        [Theory(DisplayName = "built-ins\\ArrayIteratorPrototype")]
        [MemberData(nameof(SourceFiles), "built-ins\\ArrayIteratorPrototype", false)]
        [MemberData(nameof(SourceFiles), "built-ins\\ArrayIteratorPrototype", true, Skip = "Skipped")]
        protected void ArrayIteratorPrototype(SourceFile sourceFile)
        {
            RunTestInternal(sourceFile);
        }
    }
}