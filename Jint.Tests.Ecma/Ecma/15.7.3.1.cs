using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_7_3_1 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.7.3.1")]
        public void NumberPrototypeIsADataPropertyWithDefaultAttributeValuesFalse()
        {
			RunTest(@"TestCases/ch15/15.7/15.7.3/15.7.3.1/15.7.3.1-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.7.3.1")]
        public void NumberPrototypeInitialValueIsTheNumberPrototypeObject()
        {
			RunTest(@"TestCases/ch15/15.7/15.7.3/15.7.3.1/15.7.3.1-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.7.3.1")]
        public void TheNumberPropertyPrototypeHasDontenumDontdeleteReadonlyAttributes()
        {
			RunTest(@"TestCases/ch15/15.7/15.7.3/15.7.3.1/S15.7.3.1_A1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.7.3.1")]
        public void TheNumberPropertyPrototypeHasDontenumDontdeleteReadonlyAttributes2()
        {
			RunTest(@"TestCases/ch15/15.7/15.7.3/15.7.3.1/S15.7.3.1_A1_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.7.3.1")]
        public void TheNumberPropertyPrototypeHasDontenumDontdeleteReadonlyAttributes3()
        {
			RunTest(@"TestCases/ch15/15.7/15.7.3/15.7.3.1/S15.7.3.1_A1_T3.js", false);
        }

        [Fact]
        [Trait("Category", "15.7.3.1")]
        public void NumberPrototypeIsItselfNumberObject()
        {
			RunTest(@"TestCases/ch15/15.7/15.7.3/15.7.3.1/S15.7.3.1_A2_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.7.3.1")]
        public void NumberPrototypeIsItselfNumberObject2()
        {
			RunTest(@"TestCases/ch15/15.7/15.7.3/15.7.3.1/S15.7.3.1_A2_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.7.3.1")]
        public void NumberPrototypeValueIs0()
        {
			RunTest(@"TestCases/ch15/15.7/15.7.3/15.7.3.1/S15.7.3.1_A3.js", false);
        }


    }
}
