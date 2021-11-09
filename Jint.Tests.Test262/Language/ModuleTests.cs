using Jint.Runtime;
using System;
using Xunit;
using Xunit.Sdk;

namespace Jint.Tests.Test262.Language
{
    public class ModuleTests : Test262Test
    {
        [Theory(DisplayName = "language\\module-code")]
        [MemberData(nameof(SourceFiles), "language\\module-code", false)]
        [MemberData(nameof(SourceFiles), "language\\module-code", true, Skip = "Skipped")]
        protected void ModuleCode(SourceFile sourceFile)
        {
            RunModuleTest(sourceFile);
        }

        [Theory(DisplayName = "language\\export")]
        [MemberData(nameof(SourceFiles), "language\\export", false)]
        [MemberData(nameof(SourceFiles), "language\\export", true, Skip = "Skipped")]
        protected void Export(SourceFile sourceFile)
        {
            RunModuleTest(sourceFile);
        }

        [Theory(DisplayName = "language\\import")]
        [MemberData(nameof(SourceFiles), "language\\import", false)]
        [MemberData(nameof(SourceFiles), "language\\import", true, Skip = "Skipped")]
        protected void Import(SourceFile sourceFile)
        {
            RunModuleTest(sourceFile);
        }

        private void RunModuleTest(SourceFile sourceFile)
        {
            if (sourceFile.Skip)
            {
                return;
            }

            var code = sourceFile.Code;

            var options = new Options();
            options.Host.Factory = (_) => new ModuleTestHost();

            var engine = new Engine(options);

            var negative = code.IndexOf("negative:", StringComparison.OrdinalIgnoreCase) != -1;
            string lastError = null;

            try
            {
                engine.LoadModule(sourceFile.FullPath);
            }
            catch (JavaScriptException ex)
            {
                lastError = ex.ToString();
            }
            catch (Exception ex)
            {
                lastError = ex.ToString();
            }

            if (!negative && !string.IsNullOrWhiteSpace(lastError))
            {
                throw new XunitException(lastError);
            }
            
        }
    }
}