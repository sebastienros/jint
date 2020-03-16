using Xunit;

namespace Jint.Tests.Test262
{
    public class LanguageTests : Test262Test
    {
        [Theory(DisplayName = "language\\destructuring")]
        [MemberData(nameof(SourceFiles), "language\\destructuring", false)]
        [MemberData(nameof(SourceFiles), "language\\destructuring", true, Skip = "Skipped")]
        protected void Destructuring(SourceFile sourceFile)
        {
            RunTestInternal(sourceFile);
        }

        [Theory(DisplayName = "language\\expressions\\addition")]
        [MemberData(nameof(SourceFiles), "language\\expressions\\addition", false)]
        [MemberData(nameof(SourceFiles), "language\\expressions\\addition", true, Skip = "Skipped")]
        protected void Addition(SourceFile sourceFile)
        {
            RunTestInternal(sourceFile);
        }

        [Theory(DisplayName = "language\\expressions\\array")]
        [MemberData(nameof(SourceFiles), "language\\expressions\\array", false)]
        [MemberData(nameof(SourceFiles), "language\\expressions\\array", true, Skip = "Skipped")]
        protected void Array(SourceFile sourceFile)
        {
            RunTestInternal(sourceFile);
        }

        [Theory(DisplayName = "language\\expressions\\arrow-function")]
        [MemberData(nameof(SourceFiles), "language\\expressions\\arrow-function", false)]
        [MemberData(nameof(SourceFiles), "language\\expressions\\arrow-function", true, Skip = "Skipped")]
        protected void ArrowFunction(SourceFile sourceFile)
        {
            RunTestInternal(sourceFile);
        }

        [Theory(DisplayName = "language\\expressions\\call")]
        [MemberData(nameof(SourceFiles), "language\\expressions\\call", false)]
        [MemberData(nameof(SourceFiles), "language\\expressions\\call", true, Skip = "Skipped")]
        protected void Call(SourceFile sourceFile)
        {
            RunTestInternal(sourceFile);
        }

        [Theory(DisplayName = "language\\expressions\\assignment")]
        [MemberData(nameof(SourceFiles), "language\\expressions\\assignment", false)]
        [MemberData(nameof(SourceFiles), "language\\expressions\\assignment", true, Skip = "Skipped")]
        protected void Assignment(SourceFile sourceFile)
        {
            RunTestInternal(sourceFile);
        }

        [Theory(DisplayName = "language\\expressions\\function")]
        [MemberData(nameof(SourceFiles), "language\\expressions\\function", false)]
        [MemberData(nameof(SourceFiles), "language\\expressions\\function", true, Skip = "Skipped")]
        protected void Function(SourceFile sourceFile)
        {
            RunTestInternal(sourceFile);
        }

        [Theory(DisplayName = "language\\expressions\\new")]
        [MemberData(nameof(SourceFiles), "language\\expressions\\new", false)]
        [MemberData(nameof(SourceFiles), "language\\expressions\\new", true, Skip = "Skipped")]
        protected void New(SourceFile sourceFile)
        {
            RunTestInternal(sourceFile);
        }

        [Theory(DisplayName = "language\\expressions\\object")]
        [MemberData(nameof(SourceFiles), "language\\expressions\\object", false)]
        [MemberData(nameof(SourceFiles), "language\\expressions\\object", true, Skip = "Skipped")]
        protected void Object(SourceFile sourceFile)
        {
            RunTestInternal(sourceFile);
        }

        [Theory(DisplayName = "language\\rest-parameters")]
        [MemberData(nameof(SourceFiles), "language\\rest-parameters", false)]
        [MemberData(nameof(SourceFiles), "language\\rest-parameters", true, Skip = "Skipped")]
        protected void RestParameters(SourceFile sourceFile)
        {
            RunTestInternal(sourceFile);
        }

        [Theory(DisplayName = "language\\source-text")]
        [MemberData(nameof(SourceFiles), "language\\source-text", false)]
        [MemberData(nameof(SourceFiles), "language\\source-text", true, Skip = "Skipped")]
        protected void SourceText(SourceFile sourceFile)
        {
            RunTestInternal(sourceFile);
        }

        [Theory(DisplayName = "language\\expressions\\template-literal")]
        [MemberData(nameof(SourceFiles), "language\\expressions\\template-literal", false)]
        [MemberData(nameof(SourceFiles), "language\\expressions\\template-literal", true, Skip = "Skipped")]
        protected void TemplateLiteral(SourceFile sourceFile)
        {
            RunTestInternal(sourceFile);
        }

        [Theory(DisplayName = "language\\types")]
        [MemberData(nameof(SourceFiles), "language\\types", false)]
        [MemberData(nameof(SourceFiles), "language\\types", true, Skip = "Skipped")]
        protected void Types(SourceFile sourceFile)
        {
            RunTestInternal(sourceFile);
        }

        [Theory(DisplayName = "language\\white-space")]
        [MemberData(nameof(SourceFiles), "language\\white-space", false)]
        [MemberData(nameof(SourceFiles), "language\\white-space", true, Skip = "Skipped")]
        protected void WhiteSpace(SourceFile sourceFile)
        {
            RunTestInternal(sourceFile);
        }
    }
}