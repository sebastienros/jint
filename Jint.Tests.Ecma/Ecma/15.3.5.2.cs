using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_3_5_2 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.3.5.2")]
        public void ThePrototypePropertyHasTheAttributesDontdelete()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.5/S15.3.5.2_A1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.5.2")]
        public void ThePrototypePropertyHasTheAttributesDontdelete2()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.5/S15.3.5.2_A1_T2.js", false);
        }


    }
}
