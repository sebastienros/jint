using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_11_4 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.11.4")]
        public void TheValueOfTheInternalPrototypePropertyOfTheErrorPrototypeObjectIsTheObjectPrototypeObject15231()
        {
			RunTest(@"TestCases/ch15/15.11/15.11.4/S15.11.4_A1.js", false);
        }

        [Fact]
        [Trait("Category", "15.11.4")]
        public void TheValueOfTheInternalClassPropertyOfErrorPrototypeObjectIsError()
        {
			RunTest(@"TestCases/ch15/15.11/15.11.4/S15.11.4_A2.js", false);
        }

        [Fact]
        [Trait("Category", "15.11.4")]
        public void SinceErrorPrototypeObjectIsNotFunctionItHasNotCallMethod()
        {
			RunTest(@"TestCases/ch15/15.11/15.11.4/S15.11.4_A3.js", false);
        }

        [Fact]
        [Trait("Category", "15.11.4")]
        public void SinceErrorPrototypeObjectIsNotFunctionItHasNotCreateMethod()
        {
			RunTest(@"TestCases/ch15/15.11/15.11.4/S15.11.4_A4.js", false);
        }


    }
}
