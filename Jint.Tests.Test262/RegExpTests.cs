using Xunit;

namespace Jint.Tests.Test262
{
    public class RegExpTests : Test262Test
    {
        [Theory(DisplayName = "built-ins\\RegExp")]
        [MemberData(nameof(SourceFiles), "built-ins\\RegExp", false)]
        [MemberData(nameof(SourceFiles), "built-ins\\RegExp", true, Skip = "Skipped")]
        protected void RegExp(SourceFile sourceFile)
        {
            RunTestInternal(sourceFile);
        }

        [Theory(DisplayName = "built-ins\\StringIteratorPrototype")]
        [MemberData(nameof(SourceFiles), "built-ins\\RegExpStringIteratorPrototype", false)]
        [MemberData(nameof(SourceFiles), "built-ins\\RegExpStringIteratorPrototype", true, Skip = "Skipped")]
        protected void RegExpStringIteratorPrototype(SourceFile sourceFile)
        {
            RunTestInternal(sourceFile);
        }
    }
}