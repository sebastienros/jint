using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_4_3 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.4.3")]
        public void TheValueOfTheInternalPrototypePropertyOfTheArrayConstructorIsTheFunctionPrototypeObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.3/S15.4.3_A1.1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.3")]
        public void TheValueOfTheInternalPrototypePropertyOfTheArrayConstructorIsTheFunctionPrototypeObject2()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.3/S15.4.3_A1.1_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.3")]
        public void TheValueOfTheInternalPrototypePropertyOfTheArrayConstructorIsTheFunctionPrototypeObject3()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.3/S15.4.3_A1.1_T3.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.3")]
        public void TheLengthPropertyOfArrayHasTheAttributeDontenum()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.3/S15.4.3_A2.1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.3")]
        public void TheLengthPropertyOfArrayHasTheAttributeDontdelete()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.3/S15.4.3_A2.2.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.3")]
        public void TheLengthPropertyOfArrayHasTheAttributeReadonly()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.3/S15.4.3_A2.3.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.3")]
        public void TheLengthPropertyOfArrayIs1()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.3/S15.4.3_A2.4.js", false);
        }


    }
}
