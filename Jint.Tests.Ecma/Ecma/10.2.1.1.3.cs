using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_10_2_1_1_3 : EcmaTest
    {
        [Fact]
        [Trait("Category", "10.2.1.1.3")]
        public void StrictModeTypeerrorIsThrownWhenChangingTheValueOfAValuePropertyOfTheGlobalObjectUnderStrictModeNan()
        {
			RunTest(@"TestCases/ch10/10.2/10.2.1/10.2.1.1/10.2.1.1.3/10.2.1.1.3-4-16-s.js", false);
        }

        [Fact]
        [Trait("Category", "10.2.1.1.3")]
        public void StrictModeTypeerrorIsThrownWhenChangingTheValueOfAValuePropertyOfTheGlobalObjectUnderStrictModeUndefined()
        {
			RunTest(@"TestCases/ch10/10.2/10.2.1/10.2.1.1/10.2.1.1.3/10.2.1.1.3-4-18-s.js", false);
        }

        [Fact]
        [Trait("Category", "10.2.1.1.3")]
        public void StrictModeTypeerrorIsNotThrownWhenChangingTheValueOfTheConstructorPropertiesOfTheGlobalObjectUnderStrictModeObject()
        {
			RunTest(@"TestCases/ch10/10.2/10.2.1/10.2.1.1/10.2.1.1.3/10.2.1.1.3-4-22-s.js", false);
        }

        [Fact]
        [Trait("Category", "10.2.1.1.3")]
        public void StrictModeTypeerrorIsNotThrownWhenChangingTheValueOfTheConstructorPropertiesOfTheGlobalObjectUnderStrictModeNumber()
        {
			RunTest(@"TestCases/ch10/10.2/10.2.1/10.2.1.1/10.2.1.1.3/10.2.1.1.3-4-27-s.js", false);
        }


    }
}
