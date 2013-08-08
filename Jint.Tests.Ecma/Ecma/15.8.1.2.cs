using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_8_1_2 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.8.1.2")]
        public void MathLn10IsApproximately2302585092994046()
        {
			RunTest(@"TestCases/ch15/15.8/15.8.1/15.8.1.2/S15.8.1.2_A1.js", false);
        }

        [Fact]
        [Trait("Category", "15.8.1.2")]
        public void ValuePropertyLn10OfTheMathObjectHasTheAttributeDontenum()
        {
			RunTest(@"TestCases/ch15/15.8/15.8.1/15.8.1.2/S15.8.1.2_A2.js", false);
        }

        [Fact]
        [Trait("Category", "15.8.1.2")]
        public void ValuePropertyLn10OfTheMathObjectHasTheAttributeDontdelete()
        {
			RunTest(@"TestCases/ch15/15.8/15.8.1/15.8.1.2/S15.8.1.2_A3.js", false);
        }

        [Fact]
        [Trait("Category", "15.8.1.2")]
        public void ValuePropertyLn10OfTheMathObjectHasTheAttributeReadonly()
        {
			RunTest(@"TestCases/ch15/15.8/15.8.1/15.8.1.2/S15.8.1.2_A4.js", false);
        }


    }
}
