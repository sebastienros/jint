using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_8_1_1 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.8.1.1")]
        public void MathEIsApproximately27182818284590452354()
        {
			RunTest(@"TestCases/ch15/15.8/15.8.1/15.8.1.1/S15.8.1.1_A1.js", false);
        }

        [Fact]
        [Trait("Category", "15.8.1.1")]
        public void ValuePropertyEOfTheMathObjectHasTheAttributeDontenum()
        {
			RunTest(@"TestCases/ch15/15.8/15.8.1/15.8.1.1/S15.8.1.1_A2.js", false);
        }

        [Fact]
        [Trait("Category", "15.8.1.1")]
        public void ValuePropertyEOfTheMathObjectHasTheAttributeDontdelete()
        {
			RunTest(@"TestCases/ch15/15.8/15.8.1/15.8.1.1/S15.8.1.1_A3.js", false);
        }

        [Fact]
        [Trait("Category", "15.8.1.1")]
        public void ValuePropertyEOfTheMathObjectHasTheAttributeReadonly()
        {
			RunTest(@"TestCases/ch15/15.8/15.8.1/15.8.1.1/S15.8.1.1_A4.js", false);
        }


    }
}
