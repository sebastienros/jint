using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_8_12_5 : EcmaTest
    {
        [Fact]
        [Trait("Category", "8.12.5")]
        public void ChangingTheValueOfADataPropertyShouldNotAffectItSNonValuePropertyDescriptorAttributes()
        {
			RunTest(@"TestCases/ch08/8.12/8.12.5/8.12.5-3-b_1.js", false);
        }

        [Fact]
        [Trait("Category", "8.12.5")]
        public void ChangingTheValueOfADataPropertyShouldNotAffectItSNonValuePropertyDescriptorAttributes2()
        {
			RunTest(@"TestCases/ch08/8.12/8.12.5/8.12.5-3-b_2.js", false);
        }

        [Fact]
        [Trait("Category", "8.12.5")]
        public void ChangingTheValueOfAnAccessorPropertyShouldNotAffectItSPropertyDescriptorAttributes()
        {
			RunTest(@"TestCases/ch08/8.12/8.12.5/8.12.5-5-b_1.js", false);
        }

        [Fact]
        [Trait("Category", "8.12.5")]
        public void WhenThePutMethodOfOIsCalledWithPropertyPAndValueVAndIfODoesnTHaveAPropertyWithNamePThenCreatesAPropertyWithNamePSetItsValueToVAndGiveItEmptyAttributes()
        {
			RunTest(@"TestCases/ch08/8.12/8.12.5/S8.12.5_A1.js", false);
        }

        [Fact]
        [Trait("Category", "8.12.5")]
        public void WhenThePutMethodOfOIsCalledWithPropertyPAndValueVThenSetTheValueOfThePropertyToVTheAttributesOfThePropertyAreNotChanged()
        {
			RunTest(@"TestCases/ch08/8.12/8.12.5/S8.12.5_A2.js", false);
        }


    }
}
