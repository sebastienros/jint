using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_3 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.3")]
        public void FunctionIsThePropertyOfGlobal()
        {
			RunTest(@"TestCases/ch15/15.3/S15.3_A1.js", false);
        }

        [Fact]
        [Trait("Category", "15.3")]
        public void SinceApplyingTheCallMethodToFunctionConstructorThemselfLeadsToCreatingANewFunctionInstanceTheSecondArgumentMustBeAValidFunctionBody()
        {
			RunTest(@"TestCases/ch15/15.3/S15.3_A2_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.3")]
        public void SinceApplyingTheCallMethodToFunctionConstructorThemselfLeadsToCreatingANewFunctionInstanceTheSecondArgumentMustBeAValidFunctionBody2()
        {
			RunTest(@"TestCases/ch15/15.3/S15.3_A2_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.3")]
        public void SinceWhenCallIsUsedForFunctionConstructorThemselfNewFunctionInstanceCreatesAndThenFirstArgumentThisargShouldBeIgnored()
        {
			RunTest(@"TestCases/ch15/15.3/S15.3_A3_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.3")]
        public void SinceWhenCallIsUsedForFunctionConstructorThemselfNewFunctionInstanceCreatesAndThenFirstArgumentThisargShouldBeIgnored2()
        {
			RunTest(@"TestCases/ch15/15.3/S15.3_A3_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.3")]
        public void SinceWhenCallIsUsedForFunctionConstructorThemselfNewFunctionInstanceCreatesAndThenFirstArgumentThisargShouldBeIgnored3()
        {
			RunTest(@"TestCases/ch15/15.3/S15.3_A3_T3.js", false);
        }

        [Fact]
        [Trait("Category", "15.3")]
        public void SinceWhenCallIsUsedForFunctionConstructorThemselfNewFunctionInstanceCreatesAndThenFirstArgumentThisargShouldBeIgnored4()
        {
			RunTest(@"TestCases/ch15/15.3/S15.3_A3_T4.js", false);
        }

        [Fact]
        [Trait("Category", "15.3")]
        public void SinceWhenCallIsUsedForFunctionConstructorThemselfNewFunctionInstanceCreatesAndThenFirstArgumentThisargShouldBeIgnored5()
        {
			RunTest(@"TestCases/ch15/15.3/S15.3_A3_T5.js", false);
        }

        [Fact]
        [Trait("Category", "15.3")]
        public void SinceWhenCallIsUsedForFunctionConstructorThemselfNewFunctionInstanceCreatesAndThenFirstArgumentThisargShouldBeIgnored6()
        {
			RunTest(@"TestCases/ch15/15.3/S15.3_A3_T6.js", false);
        }


    }
}
