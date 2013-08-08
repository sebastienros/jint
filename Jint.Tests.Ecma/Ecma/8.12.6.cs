using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_8_12_6 : EcmaTest
    {
        [Fact]
        [Trait("Category", "8.12.6")]
        public void WhenTheHaspropertyMethodOfOIsCalledWithPropertyNamePAndIfOHasAPropertyWithNamePReturnTrue()
        {
			RunTest(@"TestCases/ch08/8.12/8.12.6/S8.12.6_A1.js", false);
        }

        [Fact]
        [Trait("Category", "8.12.6")]
        public void WhenTheHaspropertyMethodOfOIsCalledWithPropertyNamePAndIfOHasNotAPropertyWithNamePThenIfThePrototypeOfOIsNullReturnFalseOrCallTheHaspropertyMethodOfPrototypeWithPropertyNameP()
        {
			RunTest(@"TestCases/ch08/8.12/8.12.6/S8.12.6_A2_T1.js", false);
        }

        [Fact]
        [Trait("Category", "8.12.6")]
        public void WhenTheHaspropertyMethodOfOIsCalledWithPropertyNamePAndIfOHasNotAPropertyWithNamePThenIfThePrototypeOfOIsNullReturnFalseOrCallTheHaspropertyMethodOfPrototypeWithPropertyNameP2()
        {
			RunTest(@"TestCases/ch08/8.12/8.12.6/S8.12.6_A2_T2.js", false);
        }

        [Fact]
        [Trait("Category", "8.12.6")]
        public void HaspropertyIsSensitiveToPropertyExistenceButGetIsNot()
        {
			RunTest(@"TestCases/ch08/8.12/8.12.6/S8.12.6_A3.js", false);
        }


    }
}
