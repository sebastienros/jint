using Xunit;

namespace Jint.Tests.Test262.BuiltIns
{
    public class TypedArrayTests : Test262Test
    {
        [Theory(DisplayName = "built-ins\\TypedArray")]
        [MemberData(nameof(SourceFiles), "built-ins\\TypedArray", false)]
        [MemberData(nameof(SourceFiles), "built-ins\\TypedArray", true, Skip = "Skipped")]
        protected void TypedArray(SourceFile sourceFile)
        {
            RunTestInternal(sourceFile);
        }

        [Theory(DisplayName = "built-ins\\TypedArrayConstructors")]
        [MemberData(nameof(SourceFiles), "built-ins\\TypedArrayConstructors", false)]
        [MemberData(nameof(SourceFiles), "built-ins\\TypedArrayConstructors", true, Skip = "Skipped")]
        protected void TypedArrayConstructors(SourceFile sourceFile)
        {
            RunTestInternal(sourceFile);
        }
    }
}