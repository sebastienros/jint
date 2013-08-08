using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_7_4 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.7.4")]
        public void NumberPrototypeObjectItsClassMustBeNumber()
        {
			RunTest(@"TestCases/ch15/15.7/15.7.4/15.7.4-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.7.4")]
        public void TheNumberPrototypeObjectIsItselfANumberObjectItsClassIsNumberWhoseValueIs0()
        {
			RunTest(@"TestCases/ch15/15.7/15.7.4/S15.7.4_A1.js", false);
        }

        [Fact]
        [Trait("Category", "15.7.4")]
        public void TheValueOfTheInternalPrototypePropertyOfTheNumberPrototypeObjectIsTheObjectPrototypeObject()
        {
			RunTest(@"TestCases/ch15/15.7/15.7.4/S15.7.4_A2.js", false);
        }

        [Fact]
        [Trait("Category", "15.7.4")]
        public void TheNumberPrototypeObjectHasThePropertyConstructor()
        {
			RunTest(@"TestCases/ch15/15.7/15.7.4/S15.7.4_A3.1.js", false);
        }

        [Fact]
        [Trait("Category", "15.7.4")]
        public void TheNumberPrototypeObjectHasThePropertyTostring()
        {
			RunTest(@"TestCases/ch15/15.7/15.7.4/S15.7.4_A3.2.js", false);
        }

        [Fact]
        [Trait("Category", "15.7.4")]
        public void TheNumberPrototypeObjectHasThePropertyTolocalestring()
        {
			RunTest(@"TestCases/ch15/15.7/15.7.4/S15.7.4_A3.3.js", false);
        }

        [Fact]
        [Trait("Category", "15.7.4")]
        public void TheNumberPrototypeObjectHasThePropertyValueof()
        {
			RunTest(@"TestCases/ch15/15.7/15.7.4/S15.7.4_A3.4.js", false);
        }

        [Fact]
        [Trait("Category", "15.7.4")]
        public void TheNumberPrototypeObjectHasThePropertyTofixed()
        {
			RunTest(@"TestCases/ch15/15.7/15.7.4/S15.7.4_A3.5.js", false);
        }

        [Fact]
        [Trait("Category", "15.7.4")]
        public void TheNumberPrototypeObjectHasThePropertyToexponential()
        {
			RunTest(@"TestCases/ch15/15.7/15.7.4/S15.7.4_A3.6.js", false);
        }

        [Fact]
        [Trait("Category", "15.7.4")]
        public void TheNumberPrototypeObjectHasThePropertyToprecision()
        {
			RunTest(@"TestCases/ch15/15.7/15.7.4/S15.7.4_A3.7.js", false);
        }


    }
}
