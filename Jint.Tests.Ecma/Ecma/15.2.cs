using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_2 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.2")]
        public void ObjectIsThePropertyOfGlobal()
        {
			RunTest(@"TestCases/ch15/15.2/S15.2_A1.js", false);
        }


    }
}
