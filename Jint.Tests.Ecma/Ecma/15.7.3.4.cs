using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_7_3_4 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.7.3.4")]
        public void NumberNanIsNotANumber()
        {
			RunTest(@"TestCases/ch15/15.7/15.7.3/15.7.3.4/S15.7.3.4_A1.js", false);
        }

        [Fact]
        [Trait("Category", "15.7.3.4")]
        public void NumberNanIsReadonly()
        {
			RunTest(@"TestCases/ch15/15.7/15.7.3/15.7.3.4/S15.7.3.4_A2.js", false);
        }

        [Fact]
        [Trait("Category", "15.7.3.4")]
        public void NumberNanIsDontdelete()
        {
			RunTest(@"TestCases/ch15/15.7/15.7.3/15.7.3.4/S15.7.3.4_A3.js", false);
        }

        [Fact]
        [Trait("Category", "15.7.3.4")]
        public void NumberNanHasTheAttributeDontenum()
        {
			RunTest(@"TestCases/ch15/15.7/15.7.3/15.7.3.4/S15.7.3.4_A4.js", false);
        }


    }
}
