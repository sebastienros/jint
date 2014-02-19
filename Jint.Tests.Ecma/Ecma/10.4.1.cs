using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_10_4_1 : EcmaTest
    {
        [Fact]
        [Trait("Category", "10.4.1")]
        public void VariableInstantiationIsPerformedUsingTheGlobalObjectAsTheVariableObjectAndUsingPropertyAttributesDontdelete()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.1/S10.4.1_A1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.1")]
        public void VariableInstantiationIsPerformedUsingTheGlobalObjectAsTheVariableObjectAndUsingPropertyAttributesDontdelete2()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.1/S10.4.1_A1_T2.js", false);
        }


    }
}
