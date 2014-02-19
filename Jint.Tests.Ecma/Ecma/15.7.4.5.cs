using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_7_4_5 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.7.4.5")]
        public void Step1LetFBeTointegerFractiondigitsIfFractiondigitsIsUndefinedThisStepProducesTheValue0()
        {
			RunTest(@"TestCases/ch15/15.7/15.7.4/15.7.4.5/S15.7.4.5_A1.1_T01.js", false);
        }

        [Fact]
        [Trait("Category", "15.7.4.5")]
        public void Step1LetFBeTointegerFractiondigitsIfFractiondigitsIsUndefinedThisStepProducesTheValue02()
        {
			RunTest(@"TestCases/ch15/15.7/15.7.4/15.7.4.5/S15.7.4.5_A1.1_T02.js", false);
        }

        [Fact]
        [Trait("Category", "15.7.4.5")]
        public void Step4IfThisNumberValueIsNanReturnTheStringNan()
        {
			RunTest(@"TestCases/ch15/15.7/15.7.4/15.7.4.5/S15.7.4.5_A1.3_T01.js", false);
        }

        [Fact]
        [Trait("Category", "15.7.4.5")]
        public void Step4IfThisNumberValueIsNanReturnTheStringNan2()
        {
			RunTest(@"TestCases/ch15/15.7/15.7.4/15.7.4.5/S15.7.4.5_A1.3_T02.js", false);
        }

        [Fact]
        [Trait("Category", "15.7.4.5")]
        public void Step9IfX1021LetMTostringX()
        {
			RunTest(@"TestCases/ch15/15.7/15.7.4/15.7.4.5/S15.7.4.5_A1.4_T01.js", false);
        }

        [Fact]
        [Trait("Category", "15.7.4.5")]
        public void TheLengthPropertyOfTheTofixedMethodIs1()
        {
			RunTest(@"TestCases/ch15/15.7/15.7.4/15.7.4.5/S15.7.4.5_A2_T01.js", false);
        }


    }
}
