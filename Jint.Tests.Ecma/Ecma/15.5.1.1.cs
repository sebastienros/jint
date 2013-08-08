using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_5_1_1 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.5.1.1")]
        public void WhenStringIsCalledAsAFunctionRatherThanAsAConstructorItPerformsATypeConversion()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.1/S15.5.1.1_A1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.1.1")]
        public void WhenStringIsCalledAsAFunctionRatherThanAsAConstructorItPerformsATypeConversion2()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.1/S15.5.1.1_A1_T10.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.1.1")]
        public void WhenStringIsCalledAsAFunctionRatherThanAsAConstructorItPerformsATypeConversion3()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.1/S15.5.1.1_A1_T11.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.1.1")]
        public void WhenStringIsCalledAsAFunctionRatherThanAsAConstructorItPerformsATypeConversion4()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.1/S15.5.1.1_A1_T12.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.1.1")]
        public void WhenStringIsCalledAsAFunctionRatherThanAsAConstructorItPerformsATypeConversion5()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.1/S15.5.1.1_A1_T13.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.1.1")]
        public void WhenStringIsCalledAsAFunctionRatherThanAsAConstructorItPerformsATypeConversion6()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.1/S15.5.1.1_A1_T14.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.1.1")]
        public void WhenStringIsCalledAsAFunctionRatherThanAsAConstructorItPerformsATypeConversion7()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.1/S15.5.1.1_A1_T15.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.1.1")]
        public void WhenStringIsCalledAsAFunctionRatherThanAsAConstructorItPerformsATypeConversion8()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.1/S15.5.1.1_A1_T16.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.1.1")]
        public void WhenStringIsCalledAsAFunctionRatherThanAsAConstructorItPerformsATypeConversion9()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.1/S15.5.1.1_A1_T17.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.1.1")]
        public void WhenStringIsCalledAsAFunctionRatherThanAsAConstructorItPerformsATypeConversion10()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.1/S15.5.1.1_A1_T18.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.1.1")]
        public void WhenStringIsCalledAsAFunctionRatherThanAsAConstructorItPerformsATypeConversion11()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.1/S15.5.1.1_A1_T19.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.1.1")]
        public void WhenStringIsCalledAsAFunctionRatherThanAsAConstructorItPerformsATypeConversion12()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.1/S15.5.1.1_A1_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.1.1")]
        public void WhenStringIsCalledAsAFunctionRatherThanAsAConstructorItPerformsATypeConversion13()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.1/S15.5.1.1_A1_T3.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.1.1")]
        public void WhenStringIsCalledAsAFunctionRatherThanAsAConstructorItPerformsATypeConversion14()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.1/S15.5.1.1_A1_T4.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.1.1")]
        public void WhenStringIsCalledAsAFunctionRatherThanAsAConstructorItPerformsATypeConversion15()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.1/S15.5.1.1_A1_T5.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.1.1")]
        public void WhenStringIsCalledAsAFunctionRatherThanAsAConstructorItPerformsATypeConversion16()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.1/S15.5.1.1_A1_T6.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.1.1")]
        public void WhenStringIsCalledAsAFunctionRatherThanAsAConstructorItPerformsATypeConversion17()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.1/S15.5.1.1_A1_T7.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.1.1")]
        public void WhenStringIsCalledAsAFunctionRatherThanAsAConstructorItPerformsATypeConversion18()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.1/S15.5.1.1_A1_T8.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.1.1")]
        public void WhenStringIsCalledAsAFunctionRatherThanAsAConstructorItPerformsATypeConversion19()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.1/S15.5.1.1_A1_T9.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.1.1")]
        public void IfValueIsNotSuppliedTheEmptyStringIsReturned()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.1/S15.5.1.1_A2_T1.js", false);
        }


    }
}
