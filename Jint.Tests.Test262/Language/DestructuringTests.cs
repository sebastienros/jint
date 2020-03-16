using Xunit;

namespace Jint.Tests.Test262.Language
{
    public class DestructuringTests : Test262Test
    {
        [Theory(DisplayName = "language\\destructuring")]
        [MemberData(nameof(SourceFiles), "language\\destructuring", false)]
        [MemberData(nameof(SourceFiles), "language\\destructuring", true, Skip = "Skipped")]
        protected void Destructuring(SourceFile sourceFile)
        {
            RunTestInternal(sourceFile);
        }
    }
}