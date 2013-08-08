using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_4_4 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.4.4")]
        public void TheValueOfTheInternalPrototypePropertyOfTheArrayPrototypeObjectIsTheObjectPrototypeObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/S15.4.4_A1.1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4")]
        public void TheArrayPrototypeObjectIsItselfAnArrayItsClassIsArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/S15.4.4_A1.1_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4")]
        public void TheValueOfTheInternalPrototypePropertyOfTheArrayPrototypeObjectIsTheObjectPrototypeObject2()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/S15.4.4_A1.1_T3.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4")]
        public void TheClassPropertyOfTheArrayPrototypeObjectIsSetToArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/S15.4.4_A1.2_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4")]
        public void ArrayPrototypeObjectHasLengthPropertyWhoseValueIs0()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/S15.4.4_A1.3_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4")]
        public void TheArrayPrototypeObjectDoesNotHaveAValueofPropertyOfItsOwnHoweverItInheritsTheValueofPropertyFromTheValueofPropertyFromTheObjectPrototypeObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/S15.4.4_A2.1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4")]
        public void TheArrayPrototypeObjectDoesNotHaveAValueofPropertyOfItsOwnHoweverItInheritsTheValueofPropertyFromTheValueofPropertyFromTheObjectPrototypeObject2()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/S15.4.4_A2.1_T2.js", false);
        }


    }
}
