using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_5_3_2 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.5.3.2")]
        public void TheLengthPropertyOfTheFromcharcodeFunctionIs1()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.3/S15.5.3.2_A1.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.3.2")]
        public void StringFromcharcodeReturnsEmptyString()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.3/S15.5.3.2_A2.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.3.2")]
        public void StringFromcharcodeChar0Char1()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.3/S15.5.3.2_A3_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.3.2")]
        public void StringFromcharcodeChar0Char12()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.3/S15.5.3.2_A3_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.3.2")]
        public void StringFromcharcodeHasNotConstructMethod()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.3/S15.5.3.2_A4.js", false);
        }


    }
}
