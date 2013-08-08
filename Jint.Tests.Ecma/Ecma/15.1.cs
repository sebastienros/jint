using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_1 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.1")]
        public void TheGlobalObjectDoesNotHaveAConstructProperty()
        {
			RunTest(@"TestCases/ch15/15.1/S15.1_A1_T1.js", true);
        }

        [Fact]
        [Trait("Category", "15.1")]
        public void TheGlobalObjectDoesNotHaveAConstructProperty2()
        {
			RunTest(@"TestCases/ch15/15.1/S15.1_A1_T2.js", true);
        }

        [Fact]
        [Trait("Category", "15.1")]
        public void TheGlobalObjectDoesNotHaveACallProperty()
        {
			RunTest(@"TestCases/ch15/15.1/S15.1_A2_T1.js", true);
        }


    }
}
