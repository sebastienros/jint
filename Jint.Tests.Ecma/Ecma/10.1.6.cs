using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_10_1_6 : EcmaTest
    {
        [Fact]
        [Trait("Category", "10.1.6")]
        public void TheActivationObjectIsInitialisedWithAPropertyWithNameArgumentsAndAttributesDontdelete()
        {
			RunTest(@"TestCases/ch10/10.1/S10.1.6_A1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "10.1.6")]
        public void TheActivationObjectIsInitialisedWithAPropertyWithNameArgumentsAndAttributesDontdelete2()
        {
			RunTest(@"TestCases/ch10/10.1/S10.1.6_A1_T2.js", false);
        }

        [Fact]
        [Trait("Category", "10.1.6")]
        public void TheActivationObjectIsInitialisedWithAPropertyWithNameArgumentsAndAttributesDontdelete3()
        {
			RunTest(@"TestCases/ch10/10.1/S10.1.6_A1_T3.js", false);
        }


    }
}
