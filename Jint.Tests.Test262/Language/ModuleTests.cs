using Xunit;

namespace Jint.Tests.Test262.Language
{
    public class ModuleTests : Test262Test
    {
        [Theory(DisplayName = "language\\module-code")]
        [MemberData(nameof(SourceFiles), "language\\module-code", false)]
        [MemberData(nameof(SourceFiles), "language\\module-code", true, Skip = "Skipped")]
        protected void ModuleCode(SourceFile sourceFile)
        {
            RunTestInternal(sourceFile);
        }

        [Theory(DisplayName = "language\\export")]
        [MemberData(nameof(SourceFiles), "language\\export", false)]
        [MemberData(nameof(SourceFiles), "language\\export", true, Skip = "Skipped")]
        protected void Export(SourceFile sourceFile)
        {
            RunTestInternal(sourceFile);
        }

        [Theory(DisplayName = "language\\import")]
        [MemberData(nameof(SourceFiles), "language\\import", false)]
        [MemberData(nameof(SourceFiles), "language\\import", true, Skip = "Skipped")]
        protected void Import(SourceFile sourceFile)
        {
            RunTestInternal(sourceFile);
        }

        private void RunModuleTest(SourceFile sourceFile)
        {

        }
    }
}