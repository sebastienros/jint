using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_3_4_2 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.3.4.2")]
        public void TheFunctionPrototypeTostringLengthPropertyHasTheAttributeReadonly()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.4/15.3.4.2/S15.3.4.2_A10.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.4.2")]
        public void TheLengthPropertyOfTheTostringMethodIs0()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.4/15.3.4.2/S15.3.4.2_A11.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.4.2")]
        public void TheFunctionPrototypeTostringFunctionIsNotGenericItThrowsATypeerrorExceptionIfItsThisValueIsNotAFunctionObject()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.4/15.3.4.2/S15.3.4.2_A12.js", true);
        }

        [Fact]
        [Trait("Category", "15.3.4.2")]
        public void TheTostringFunctionIsNotGenericItThrowsATypeerrorExceptionIfItsThisValueIsNotAFunctionObject()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.4/15.3.4.2/S15.3.4.2_A13.js", true);
        }

        [Fact]
        [Trait("Category", "15.3.4.2")]
        public void TheTostringFunctionIsNotGenericItThrowsATypeerrorExceptionIfItsThisValueIsNotAFunctionObject2()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.4/15.3.4.2/S15.3.4.2_A14.js", true);
        }

        [Fact]
        [Trait("Category", "15.3.4.2")]
        public void TheTostringFunctionIsNotGenericItThrowsATypeerrorExceptionIfItsThisValueIsNotAFunctionObject3()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.4/15.3.4.2/S15.3.4.2_A15.js", true);
        }

        [Fact]
        [Trait("Category", "15.3.4.2")]
        public void TheTostringFunctionIsNotGenericItThrowsATypeerrorExceptionIfItsThisValueIsNotAFunctionObject4()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.4/15.3.4.2/S15.3.4.2_A16.js", true);
        }

        [Fact]
        [Trait("Category", "15.3.4.2")]
        public void FunctionPrototypeTostringHasNotPrototypeProperty()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.4/15.3.4.2/S15.3.4.2_A6.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.4.2")]
        public void FunctionPrototypeTostringCanTBeUsedAsConstructor()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.4/15.3.4.2/S15.3.4.2_A7.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.4.2")]
        public void TheFunctionPrototypeTostringLengthPropertyHasTheAttributeDontenum()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.4/15.3.4.2/S15.3.4.2_A8.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.4.2")]
        public void TheFunctionPrototypeTostringLengthPropertyHasTheAttributeDontdelete()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.4/15.3.4.2/S15.3.4.2_A9.js", false);
        }


    }
}
