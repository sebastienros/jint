using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_8_12_7 : EcmaTest
    {
        [Fact]
        [Trait("Category", "8.12.7")]
        public void WhenTheDeleteMethodOfOIsCalledWithPropertyNamePAndIfThePropertyHasTheDontdeleteAttributeReturnFalse()
        {
			RunTest(@"TestCases/ch08/8.12/8.12.7/S8.12.7_A1.js", false);
        }

        [Fact]
        [Trait("Category", "8.12.7")]
        public void WhenTheDeleteMethodOfOIsCalledWithPropertyNamePAndIfODoesnTHaveAPropertyWithNamePReturnTrue()
        {
			RunTest(@"TestCases/ch08/8.12/8.12.7/S8.12.7_A2_T1.js", false);
        }

        [Fact]
        [Trait("Category", "8.12.7")]
        public void WhenTheDeleteMethodOfOIsCalledWithPropertyNamePAndIfODoesnTHaveAPropertyWithNamePReturnTrue2()
        {
			RunTest(@"TestCases/ch08/8.12/8.12.7/S8.12.7_A2_T2.js", false);
        }

        [Fact]
        [Trait("Category", "8.12.7")]
        public void WhenTheDeleteMethodOfOIsCalledWithPropertyNamePRemovesThePropertyWithNamePFromOAndReturnTrue()
        {
			RunTest(@"TestCases/ch08/8.12/8.12.7/S8.12.7_A3.js", false);
        }


    }
}
