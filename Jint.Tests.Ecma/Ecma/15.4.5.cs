using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_4_5 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.4.5")]
        public void ArrayInstancesHaveClassSetToArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.5/15.4.5-1.js", false);
        }


    }
}
