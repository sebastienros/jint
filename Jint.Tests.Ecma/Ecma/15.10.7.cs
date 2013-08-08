using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_10_7 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.10.7")]
        public void RegexpInstanceHasNotCallProperty()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.7/S15.10.7_A1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.7")]
        public void RegexpInstanceHasNotCallProperty2()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.7/S15.10.7_A1_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.7")]
        public void RegexpInstanceHasNotConstructProperty()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.7/S15.10.7_A2_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.7")]
        public void RegexpInstanceHasNotConstructProperty2()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.7/S15.10.7_A2_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.7")]
        public void RegexpInstanceTypeIsRegexp()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.7/S15.10.7_A3_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.7")]
        public void RegexpInstanceTypeIsRegexp2()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.7/S15.10.7_A3_T2.js", false);
        }


    }
}
