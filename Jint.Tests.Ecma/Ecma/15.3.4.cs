using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_3_4 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.3.4")]
        public void TheFunctionPrototypeObjectIsItselfAFunctionObjectItsClassIsFunction()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.4/S15.3.4_A1.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.4")]
        public void TheFunctionPrototypeObjectIsItselfAFunctionObjectThatWhenInvokedAcceptsAnyArgumentsAndReturnsUndefined()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.4/S15.3.4_A2_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.4")]
        public void TheFunctionPrototypeObjectIsItselfAFunctionObjectThatWhenInvokedAcceptsAnyArgumentsAndReturnsUndefined2()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.4/S15.3.4_A2_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.4")]
        public void TheFunctionPrototypeObjectIsItselfAFunctionObjectThatWhenInvokedAcceptsAnyArgumentsAndReturnsUndefined3()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.4/S15.3.4_A2_T3.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.4")]
        public void TheValueOfTheInternalPrototypePropertyOfTheFunctionPrototypeObjectIsTheObjectPrototypeObject1534()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.4/S15.3.4_A3_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.4")]
        public void TheValueOfTheInternalPrototypePropertyOfTheFunctionPrototypeObjectIsTheObjectPrototypeObject15321()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.4/S15.3.4_A3_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.4")]
        public void TheFunctionPrototypeObjectDoesNotHaveAValueofPropertyOfItsOwnHoweverItInheritsTheValueofPropertyFromTheObjectPrototypeObject()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.4/S15.3.4_A4.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.4")]
        public void TheFunctionPrototypeObjectIsItselfAFunctionObjectWithoutCreateProperty()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.4/S15.3.4_A5.js", false);
        }


    }
}
