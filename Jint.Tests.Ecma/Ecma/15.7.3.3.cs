using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_7_3_3 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.7.3.3")]
        public void NumberMinValueIsApproximately5E324()
        {
			RunTest(@"TestCases/ch15/15.7/15.7.3/15.7.3.3/S15.7.3.3_A1.js", false);
        }

        [Fact]
        [Trait("Category", "15.7.3.3")]
        public void NumberMinValueIsReadonly()
        {
			RunTest(@"TestCases/ch15/15.7/15.7.3/15.7.3.3/S15.7.3.3_A2.js", false);
        }

        [Fact]
        [Trait("Category", "15.7.3.3")]
        public void NumberMinValueIsDontdelete()
        {
			RunTest(@"TestCases/ch15/15.7/15.7.3/15.7.3.3/S15.7.3.3_A3.js", false);
        }

        [Fact]
        [Trait("Category", "15.7.3.3")]
        public void NumberMinValueHasTheAttributeDontenum()
        {
			RunTest(@"TestCases/ch15/15.7/15.7.3/15.7.3.3/S15.7.3.3_A4.js", false);
        }


    }
}
