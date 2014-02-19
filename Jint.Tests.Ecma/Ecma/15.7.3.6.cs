using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_7_3_6 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.7.3.6")]
        public void NumberPositiveInfinityIsInfinity()
        {
			RunTest(@"TestCases/ch15/15.7/15.7.3/15.7.3.6/S15.7.3.6_A1.js", false);
        }

        [Fact]
        [Trait("Category", "15.7.3.6")]
        public void NumberPositiveInfinityIsReadonly()
        {
			RunTest(@"TestCases/ch15/15.7/15.7.3/15.7.3.6/S15.7.3.6_A2.js", false);
        }

        [Fact]
        [Trait("Category", "15.7.3.6")]
        public void NumberPositiveInfinityIsDontdelete()
        {
			RunTest(@"TestCases/ch15/15.7/15.7.3/15.7.3.6/S15.7.3.6_A3.js", false);
        }

        [Fact]
        [Trait("Category", "15.7.3.6")]
        public void NumberPositiveInfinityHasTheAttributeDontenum()
        {
			RunTest(@"TestCases/ch15/15.7/15.7.3/15.7.3.6/S15.7.3.6_A4.js", false);
        }


    }
}
