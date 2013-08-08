using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_7_3_2 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.7.3.2")]
        public void NumberMaxValueIsApproximately17976931348623157E308()
        {
			RunTest(@"TestCases/ch15/15.7/15.7.3/15.7.3.2/S15.7.3.2_A1.js", false);
        }

        [Fact]
        [Trait("Category", "15.7.3.2")]
        public void NumberMaxValueIsReadonly()
        {
			RunTest(@"TestCases/ch15/15.7/15.7.3/15.7.3.2/S15.7.3.2_A2.js", false);
        }

        [Fact]
        [Trait("Category", "15.7.3.2")]
        public void NumberMaxValueIsDontdelete()
        {
			RunTest(@"TestCases/ch15/15.7/15.7.3/15.7.3.2/S15.7.3.2_A3.js", false);
        }

        [Fact]
        [Trait("Category", "15.7.3.2")]
        public void NumberMaxValueHasTheAttributeDontenum()
        {
			RunTest(@"TestCases/ch15/15.7/15.7.3/15.7.3.2/S15.7.3.2_A4.js", false);
        }


    }
}
