using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_7_3 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.7.3")]
        public void NumberConstructorPrototypeIsTheFunctionPrototypeObject()
        {
			RunTest(@"TestCases/ch15/15.7/15.7.3/15.7.3-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.7.3")]
        public void NumberConstructorPrototypeIsTheFunctionPrototypeObjectUsingGetprototypeof()
        {
			RunTest(@"TestCases/ch15/15.7/15.7.3/15.7.3-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.7.3")]
        public void TheNumberConstructorHasThePropertyPrototype()
        {
			RunTest(@"TestCases/ch15/15.7/15.7.3/S15.7.3_A1.js", false);
        }

        [Fact]
        [Trait("Category", "15.7.3")]
        public void TheNumberConstructorHasThePropertyMaxValue()
        {
			RunTest(@"TestCases/ch15/15.7/15.7.3/S15.7.3_A2.js", false);
        }

        [Fact]
        [Trait("Category", "15.7.3")]
        public void TheNumberConstructorHasThePropertyMinValue()
        {
			RunTest(@"TestCases/ch15/15.7/15.7.3/S15.7.3_A3.js", false);
        }

        [Fact]
        [Trait("Category", "15.7.3")]
        public void TheNumberConstructorHasThePropertyNan()
        {
			RunTest(@"TestCases/ch15/15.7/15.7.3/S15.7.3_A4.js", false);
        }

        [Fact]
        [Trait("Category", "15.7.3")]
        public void TheNumberConstructorHasThePropertyNegativeInfinity()
        {
			RunTest(@"TestCases/ch15/15.7/15.7.3/S15.7.3_A5.js", false);
        }

        [Fact]
        [Trait("Category", "15.7.3")]
        public void TheNumberConstructorHasThePropertyPositiveInfinity()
        {
			RunTest(@"TestCases/ch15/15.7/15.7.3/S15.7.3_A6.js", false);
        }

        [Fact]
        [Trait("Category", "15.7.3")]
        public void TheValueOfTheInternalPrototypePropertyOfTheNumberConstructorIsTheFunctionPrototypeObject()
        {
			RunTest(@"TestCases/ch15/15.7/15.7.3/S15.7.3_A7.js", false);
        }

        [Fact]
        [Trait("Category", "15.7.3")]
        public void NumberConstructorHasLengthPropertyWhoseValueIs1()
        {
			RunTest(@"TestCases/ch15/15.7/15.7.3/S15.7.3_A8.js", false);
        }


    }
}
