using System.ComponentModel;
using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_10_1_7 : EcmaTest
    {
        [Fact]
        [Trait("Category", "10.1.7")]
        public void TheThisValueAssociatedWithAnExecutioncontextIsImmutable()
        {
			RunTest(@"TestCases/ch10/10.1/S10.1.7_A1_T1.js", false);
        }


    }
}
