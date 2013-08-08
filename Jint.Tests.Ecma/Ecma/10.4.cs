using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_10_4 : EcmaTest
    {
        [Fact]
        [Trait("Category", "10.4")]
        public void EveryFunctionCallEntersANewExecutionContext()
        {
			RunTest(@"TestCases/ch10/10.4/S10.4_A1.1_T1.js", false);
        }


    }
}
