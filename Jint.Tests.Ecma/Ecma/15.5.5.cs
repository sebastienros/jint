using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_5_5 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.5.5")]
        public void StringInstanceHasNotCallProperty()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.5/S15.5.5_A1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.5")]
        public void StringInstanceHasNotCallProperty2()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.5/S15.5.5_A1_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.5")]
        public void StringInstanceHasNotConstructProperty()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.5/S15.5.5_A2_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.5")]
        public void StringInstanceHasNotConstructProperty2()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.5/S15.5.5_A2_T2.js", false);
        }


    }
}
