using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_10_2_2 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.10.2.2")]
        public void PatternSyntaxerrorWasThrownWhenCompileAPattern()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.2/15.10.2.2-1.js", false);
        }


    }
}
