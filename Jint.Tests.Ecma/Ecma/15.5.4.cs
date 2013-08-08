using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_5_4 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.5.4")]
        public void TheStringPrototypeObjectIsItselfAStringObjectItsClassIsString()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/S15.5.4_A1.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4")]
        public void TheStringPrototypeObjectIsItselfAStringObjectWhoseValueIsAnEmptyString()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/S15.5.4_A2.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4")]
        public void TheValueOfTheInternalPrototypePropertyOfTheStringPrototypeObjectIsTheObjectPrototypeObject15231()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/S15.5.4_A3.js", false);
        }


    }
}
