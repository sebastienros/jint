using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_9_4 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.9.4")]
        public void TheDateConstructorHasThePropertyPrototype()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.4/S15.9.4_A1.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.4")]
        public void TheDateConstructorHasThePropertyParse()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.4/S15.9.4_A2.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.4")]
        public void TheDateConstructorHasThePropertyUtc()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.4/S15.9.4_A3.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.4")]
        public void TheValueOfTheInternalPrototypePropertyOfTheDateConstructorIsTheFunctionPrototypeObject()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.4/S15.9.4_A4.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.4")]
        public void DateConstructorHasLengthPropertyWhoseValueIs7()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.4/S15.9.4_A5.js", false);
        }


    }
}
