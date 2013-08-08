using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_6_4 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.6.4")]
        public void TheBooleanPrototypeObjectIsItselfABooleanObjectItsClassIsBooleanWhoseValueIsFalse()
        {
			RunTest(@"TestCases/ch15/15.6/15.6.4/S15.6.4_A1.js", false);
        }

        [Fact]
        [Trait("Category", "15.6.4")]
        public void TheValueOfTheInternalPrototypePropertyOfTheBooleanPrototypeObjectIsTheObjectPrototypeObject()
        {
			RunTest(@"TestCases/ch15/15.6/15.6.4/S15.6.4_A2.js", false);
        }


    }
}
