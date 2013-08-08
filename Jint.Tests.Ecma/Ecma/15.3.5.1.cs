using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_3_5_1 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.3.5.1")]
        public void TheValueOfTheLengthPropertyIsUsuallyAnIntegerThatIndicatesTheTypicalNumberOfArgumentsExpectedByTheFunction()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.5/S15.3.5.1_A1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.5.1")]
        public void TheValueOfTheLengthPropertyIsUsuallyAnIntegerThatIndicatesTheTypicalNumberOfArgumentsExpectedByTheFunction2()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.5/S15.3.5.1_A1_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.5.1")]
        public void TheValueOfTheLengthPropertyIsUsuallyAnIntegerThatIndicatesTheTypicalNumberOfArgumentsExpectedByTheFunction3()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.5/S15.3.5.1_A1_T3.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.5.1")]
        public void TheLengthPropertyHasTheAttributesDontdelete()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.5/S15.3.5.1_A2_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.5.1")]
        public void TheLengthPropertyHasTheAttributesDontdelete2()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.5/S15.3.5.1_A2_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.5.1")]
        public void TheLengthPropertyHasTheAttributesDontdelete3()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.5/S15.3.5.1_A2_T3.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.5.1")]
        public void TheLengthPropertyHasTheAttributesReadonly()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.5/S15.3.5.1_A3_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.5.1")]
        public void TheLengthPropertyHasTheAttributesReadonly2()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.5/S15.3.5.1_A3_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.5.1")]
        public void TheLengthPropertyHasTheAttributesReadonly3()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.5/S15.3.5.1_A3_T3.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.5.1")]
        public void TheLengthPropertyHasTheAttributesDontenum()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.5/S15.3.5.1_A4_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.5.1")]
        public void TheLengthPropertyHasTheAttributesDontenum2()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.5/S15.3.5.1_A4_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.5.1")]
        public void TheLengthPropertyHasTheAttributesDontenum3()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.5/S15.3.5.1_A4_T3.js", false);
        }


    }
}
