using Xunit;

namespace Jint.Tests.Test262.Language
{
    public class ComputedPropertyNamesTests : Test262Test
    {
        [Theory(DisplayName = "language\\computed-property-names")]
        [MemberData(nameof(SourceFiles), "language\\computed-property-names", false)]
        [MemberData(nameof(SourceFiles), "language\\computed-property-names", true, Skip = "Skipped")]
        protected void ComputedPropertyNames(SourceFile sourceFile)
        {
            RunTestInternal(sourceFile);
        }
    }
}