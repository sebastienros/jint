using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_7_3_5 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.7.3.5")]
        public void NumberNegativeInfinityIsInfinity()
        {
			RunTest(@"TestCases/ch15/15.7/15.7.3/15.7.3.5/S15.7.3.5_A1.js", false);
        }

        [Fact]
        [Trait("Category", "15.7.3.5")]
        public void NumberNegativeInfinityIsReadonly()
        {
			RunTest(@"TestCases/ch15/15.7/15.7.3/15.7.3.5/S15.7.3.5_A2.js", false);
        }

        [Fact]
        [Trait("Category", "15.7.3.5")]
        public void NumberNegativeInfinityIsDontdelete()
        {
			RunTest(@"TestCases/ch15/15.7/15.7.3/15.7.3.5/S15.7.3.5_A3.js", false);
        }

        [Fact]
        [Trait("Category", "15.7.3.5")]
        public void NumberNegativeInfinityHasTheAttributeDontenum()
        {
			RunTest(@"TestCases/ch15/15.7/15.7.3/15.7.3.5/S15.7.3.5_A4.js", false);
        }


    }
}
