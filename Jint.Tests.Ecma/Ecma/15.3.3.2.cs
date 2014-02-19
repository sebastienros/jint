using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_3_3_2 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.3.3.2")]
        public void FunctionLengthDataPropertyWithValue1()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.3/15.3.3.2/15.3.3.2-1.js", false);
        }


    }
}
