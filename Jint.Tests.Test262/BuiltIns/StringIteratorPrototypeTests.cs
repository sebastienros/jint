using Xunit;

namespace Jint.Tests.Test262.BuiltIns
{
    public class StringIteratorPrototypeTests : Test262Test
    {
        [Theory(DisplayName = "built-ins\\StringIteratorPrototype")]
        [MemberData(nameof(SourceFiles), "built-ins\\StringIteratorPrototype", false)]
        [MemberData(nameof(SourceFiles), "built-ins\\StringIteratorPrototype", true, Skip = "Skipped")]
        protected void StringIteratorPrototype(SourceFile sourceFile)
        {
            RunTestInternal(sourceFile);
        }
    }
}