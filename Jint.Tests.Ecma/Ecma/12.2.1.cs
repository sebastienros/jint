using Xunit;
using Xunit.Sdk;

namespace Jint.Tests.Ecma
{
    public class Test_12_2_1 : EcmaTest
    {
        [Fact]
        [Trait("Category", "12.2.1")]
        public void EvalAFunctionDeclaringAVarNamedEvalThrowsSyntaxerrorInStrictMode()
        {
			RunTest(@"TestCases/ch12/12.2/12.2.1/12.2.1-1-s.js", false);
        }

        [Fact(Skip = "Indirect eval call also imply changes to the parser logic")]
        [Trait("Category", "12.2.1")]
        public void StrictModeAnIndirectEvalAssigningIntoEvalDoesNotThrow()
        {
			RunTest(@"TestCases/ch12/12.2/12.2.1/12.2.1-10-s.js", false);
        }

        [Fact]
        [Trait("Category", "12.2.1")]
        public void ArgumentsAsVarIdentifierInEvalCodeIsAllowed()
        {
			RunTest(@"TestCases/ch12/12.2/12.2.1/12.2.1-11.js", false);
        }

        [Fact]
        [Trait("Category", "12.2.1")]
        public void ArgumentsAsLocalVarIdentifierThrowsSyntaxerrorInStrictMode()
        {
			RunTest(@"TestCases/ch12/12.2/12.2.1/12.2.1-12-s.js", false);
        }

        [Fact]
        [Trait("Category", "12.2.1")]
        public void ArgumentsAsLocalVarIdentifierIsAllowed()
        {
			RunTest(@"TestCases/ch12/12.2/12.2.1/12.2.1-12.js", false);
        }

        [Fact]
        [Trait("Category", "12.2.1")]
        public void ArgumentsAssignmentThrowsSyntaxerrorInStrictMode()
        {
			RunTest(@"TestCases/ch12/12.2/12.2.1/12.2.1-13-s.js", false);
        }

        [Fact]
        [Trait("Category", "12.2.1")]
        public void ArgumentsAFunctionExprDeclaringAVarNamedArgumentsThrowsSyntaxerrorInStrictMode()
        {
			RunTest(@"TestCases/ch12/12.2/12.2.1/12.2.1-14-s.js", false);
        }

        [Fact]
        [Trait("Category", "12.2.1")]
        public void ArgumentsAFunctionExprAssigningIntoArgumentsThrowsASyntaxerrorInStrictMode()
        {
			RunTest(@"TestCases/ch12/12.2/12.2.1/12.2.1-15-s.js", false);
        }

        [Fact]
        [Trait("Category", "12.2.1")]
        public void AFunctionConstructorCalledAsAFunctionDeclaringAVarNamedArgumentsDoesNotThrowASyntaxerrorInStrictMode()
        {
			RunTest(@"TestCases/ch12/12.2/12.2.1/12.2.1-16-s.js", false);
        }

        [Fact]
        [Trait("Category", "12.2.1")]
        public void AFunctionConstructorCalledAsAFunctionAssigningIntoArgumentsWillNotThrowAnyErrorIfContainedWithinStrictModeAndItsBodyDoesNotStartWithStrictMode()
        {
			RunTest(@"TestCases/ch12/12.2/12.2.1/12.2.1-17-s.js", false);
        }

        [Fact]
        [Trait("Category", "12.2.1")]
        public void ADirectEvalDeclaringAVarNamedArgumentsThrowsSyntaxerrorInStrictMode()
        {
			RunTest(@"TestCases/ch12/12.2/12.2.1/12.2.1-18-s.js", false);
        }

        [Fact]
        [Trait("Category", "12.2.1")]
        public void ADirectEvalAssigningIntoArgumentsThrowsSyntaxerrorInStrictMode()
        {
			RunTest(@"TestCases/ch12/12.2/12.2.1/12.2.1-19-s.js", false);
        }

        [Fact]
        [Trait("Category", "12.2.1")]
        public void StrictModeSyntaxerrorIsThrownIfAVariabledeclarationOccursWithinStrictCodeAndItsIdentifierIsEval()
        {
			RunTest(@"TestCases/ch12/12.2/12.2.1/12.2.1-1gs.js", true);
        }

        [Fact]
        [Trait("Category", "12.2.1")]
        public void EvalAFunctionAssigningIntoEvalThrowsSyntaxerrorInStrictMode()
        {
			RunTest(@"TestCases/ch12/12.2/12.2.1/12.2.1-2-s.js", false);
        }

        [Fact(Skip = "Indirect eval call also imply changes to the parser logic")]
        [Trait("Category", "12.2.1")]
        public void StrictModeAnIndirectEvalDeclaringAVarNamedArgumentsDoesNotThrow()
        {
			RunTest(@"TestCases/ch12/12.2/12.2.1/12.2.1-20-s.js", false);
        }

        [Fact(Skip = "Indirect eval call also imply changes to the parser logic")]
        [Trait("Category", "12.2.1")]
        public void StrictModeAnIndirectEvalAssigningIntoArgumentsDoesNotThrow()
        {
			RunTest(@"TestCases/ch12/12.2/12.2.1/12.2.1-21-s.js", false);
        }

        [Fact]
        [Trait("Category", "12.2.1")]
        public void ArgumentsAsGlobalVarIdentifierThrowsSyntaxerrorInStrictMode()
        {
			RunTest(@"TestCases/ch12/12.2/12.2.1/12.2.1-22-s.js", false);
        }

        [Fact]
        [Trait("Category", "12.2.1")]
        public void ArgumentsAsLocalVarIdentifierAssignedToThrowsSyntaxerrorInStrictMode()
        {
			RunTest(@"TestCases/ch12/12.2/12.2.1/12.2.1-23-s.js", false);
        }

        [Fact]
        [Trait("Category", "12.2.1")]
        public void EvalAsLocalVarIdentifierAssignedToThrowsSyntaxerrorInStrictMode()
        {
			RunTest(@"TestCases/ch12/12.2/12.2.1/12.2.1-24-s.js", false);
        }

        [Fact]
        [Trait("Category", "12.2.1")]
        public void ArgumentsAsLocalVarIdentifierThrowsSyntaxerrorInStrictMode2()
        {
			RunTest(@"TestCases/ch12/12.2/12.2.1/12.2.1-25-s.js", false);
        }

        [Fact]
        [Trait("Category", "12.2.1")]
        public void EvalAsLocalVarIdentifierThrowsSyntaxerrorInStrictMode()
        {
			RunTest(@"TestCases/ch12/12.2/12.2.1/12.2.1-26-s.js", false);
        }

        [Fact]
        [Trait("Category", "12.2.1")]
        public void EvalAsLocalVarIdentifierAssignedToThrowsSyntaxerrorInStrictMode2()
        {
			RunTest(@"TestCases/ch12/12.2/12.2.1/12.2.1-27-s.js", false);
        }

        [Fact]
        [Trait("Category", "12.2.1")]
        public void ArgumentsAsLocalVarIdentifierAssignedToThrowsSyntaxerrorInStrictMode2()
        {
			RunTest(@"TestCases/ch12/12.2/12.2.1/12.2.1-28-s.js", false);
        }

        [Fact]
        [Trait("Category", "12.2.1")]
        public void EvalAsLocalVarIdentifierThrowsSyntaxerrorInStrictMode2()
        {
			RunTest(@"TestCases/ch12/12.2/12.2.1/12.2.1-29-s.js", false);
        }

        [Fact]
        [Trait("Category", "12.2.1")]
        public void EvalAFunctionExprDeclaringAVarNamedEvalThrowsSyntaxerrorInStrictMode()
        {
			RunTest(@"TestCases/ch12/12.2/12.2.1/12.2.1-3-s.js", false);
        }

        [Fact]
        [Trait("Category", "12.2.1")]
        public void ArgumentsAsLocalVarIdentifierThrowsSyntaxerrorInStrictMode3()
        {
			RunTest(@"TestCases/ch12/12.2/12.2.1/12.2.1-30-s.js", false);
        }

        [Fact]
        [Trait("Category", "12.2.1")]
        public void EvalAsLocalVarIdentifierDefinedTwiceThrowsSyntaxerrorInStrictMode()
        {
			RunTest(@"TestCases/ch12/12.2/12.2.1/12.2.1-31-s.js", false);
        }

        [Fact]
        [Trait("Category", "12.2.1")]
        public void ArgumentsAsLocalVarIdentifierDefinedTwiceAndAssignedOnceThrowsSyntaxerrorInStrictMode()
        {
			RunTest(@"TestCases/ch12/12.2/12.2.1/12.2.1-32-s.js", false);
        }

        [Fact]
        [Trait("Category", "12.2.1")]
        public void ArgumentsAsLocalVarIdentifierThrowsSyntaxerrorInStrictMode4()
        {
			RunTest(@"TestCases/ch12/12.2/12.2.1/12.2.1-33-s.js", false);
        }

        [Fact]
        [Trait("Category", "12.2.1")]
        public void ForVarEvalInThrowsSyntaxerrorInStrictMode()
        {
			RunTest(@"TestCases/ch12/12.2/12.2.1/12.2.1-34-s.js", false);
        }

        [Fact]
        [Trait("Category", "12.2.1")]
        public void ForVarEval42InThrowsSyntaxerrorInStrictMode()
        {
			RunTest(@"TestCases/ch12/12.2/12.2.1/12.2.1-35-s.js", false);
        }

        [Fact]
        [Trait("Category", "12.2.1")]
        public void ForVarArgumentsInThrowsSyntaxerrorInStrictMode()
        {
			RunTest(@"TestCases/ch12/12.2/12.2.1/12.2.1-36-s.js", false);
        }

        [Fact]
        [Trait("Category", "12.2.1")]
        public void ForVarArguments42InThrowsSyntaxerrorInStrictMode()
        {
			RunTest(@"TestCases/ch12/12.2/12.2.1/12.2.1-37-s.js", false);
        }

        [Fact]
        [Trait("Category", "12.2.1")]
        public void EvalAFunctionExprAssigningIntoEvalThrowsASyntaxerrorInStrictMode()
        {
			RunTest(@"TestCases/ch12/12.2/12.2.1/12.2.1-4-s.js", false);
        }

        [Fact]
        [Trait("Category", "12.2.1")]
        public void StrictModeSyntaxerrorIsThrownIfAVariabledeclarationnoinOccursWithinStrictCodeAndItsIdentifierIsArguments()
        {
			RunTest(@"TestCases/ch12/12.2/12.2.1/12.2.1-4gs.js", true);
        }

        [Fact]
        [Trait("Category", "12.2.1")]
        public void StrictModeAFunctionDeclaringVarNamedEvalDoesNotThrowSyntaxerror()
        {
			RunTest(@"TestCases/ch12/12.2/12.2.1/12.2.1-5-s.js", false);
        }

        [Fact]
        [Trait("Category", "12.2.1")]
        public void EvalAFunctionAssigningIntoEvalWillNotThrowAnyErrorIfContainedWithinStrictModeAndItsBodyDoesNotStartWithStrictMode()
        {
			RunTest(@"TestCases/ch12/12.2/12.2.1/12.2.1-6-s.js", false);
        }

        [Fact]
        [Trait("Category", "12.2.1")]
        public void EvalADirectEvalDeclaringAVarNamedEvalThrowsSyntaxerrorInStrictMode()
        {
			RunTest(@"TestCases/ch12/12.2/12.2.1/12.2.1-7-s.js", false);
        }

        [Fact]
        [Trait("Category", "12.2.1")]
        public void EvalADirectEvalAssigningIntoEvalThrowsSyntaxerrorInStrictMode()
        {
			RunTest(@"TestCases/ch12/12.2/12.2.1/12.2.1-8-s.js", false);
        }

        [Fact(Skip = "Indirect eval call also imply changes to the parser logic")]
        [Trait("Category", "12.2.1")]
        public void StrictModeAnIndirectEvalDeclaringAVarNamedEvalDoesNotThrow()
        {
			RunTest(@"TestCases/ch12/12.2/12.2.1/12.2.1-9-s.js", false);
        }


    }
}
