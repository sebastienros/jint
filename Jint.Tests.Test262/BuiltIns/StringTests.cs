using Xunit;

namespace Jint.Tests.Test262.BuiltIns
{
    public class StringTests : Test262Test
    {
        [Theory(DisplayName = "built-ins\\String")]
        [MemberData(nameof(SourceFiles), "built-ins\\String", false)]
        [MemberData(nameof(SourceFiles), "built-ins\\String", true, Skip = "Skipped")]
        protected void String(SourceFile sourceFile)
        {
            RunTestInternal(sourceFile);
        }

        [Theory(DisplayName = "built-ins\\StringIteratorPrototype")]
        [MemberData(nameof(SourceFiles), "built-ins\\StringIteratorPrototype", false)]
        [MemberData(nameof(SourceFiles), "built-ins\\StringIteratorPrototype", true, Skip = "Skipped")]
        protected void StringIteratorPrototype(SourceFile sourceFile)
        {
            RunTestInternal(sourceFile);
        }
    }
}