using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_5_2_1 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.5.2.1")]
        public void WhenStringIsCalledAsPartOfANewExpressionItIsAConstructorItInitialisesTheNewlyCreatedObjectAndTheValuePropertyOfTheNewlyConstructedObjectIsSetToTostringValueOrToTheEmptyStringIfValueIsNotSupplied()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.2/S15.5.2.1_A1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.2.1")]
        public void WhenStringIsCalledAsPartOfANewExpressionItIsAConstructorItInitialisesTheNewlyCreatedObjectAndTheValuePropertyOfTheNewlyConstructedObjectIsSetToTostringValueOrToTheEmptyStringIfValueIsNotSupplied2()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.2/S15.5.2.1_A1_T10.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.2.1")]
        public void WhenStringIsCalledAsPartOfANewExpressionItIsAConstructorItInitialisesTheNewlyCreatedObjectAndTheValuePropertyOfTheNewlyConstructedObjectIsSetToTostringValueOrToTheEmptyStringIfValueIsNotSupplied3()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.2/S15.5.2.1_A1_T11.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.2.1")]
        public void WhenStringIsCalledAsPartOfANewExpressionItIsAConstructorItInitialisesTheNewlyCreatedObjectAndTheValuePropertyOfTheNewlyConstructedObjectIsSetToTostringValueOrToTheEmptyStringIfValueIsNotSupplied4()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.2/S15.5.2.1_A1_T12.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.2.1")]
        public void WhenStringIsCalledAsPartOfANewExpressionItIsAConstructorItInitialisesTheNewlyCreatedObjectAndTheValuePropertyOfTheNewlyConstructedObjectIsSetToTostringValueOrToTheEmptyStringIfValueIsNotSupplied5()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.2/S15.5.2.1_A1_T13.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.2.1")]
        public void WhenStringIsCalledAsPartOfANewExpressionItIsAConstructorItInitialisesTheNewlyCreatedObjectAndTheValuePropertyOfTheNewlyConstructedObjectIsSetToTostringValueOrToTheEmptyStringIfValueIsNotSupplied6()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.2/S15.5.2.1_A1_T16.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.2.1")]
        public void WhenStringIsCalledAsPartOfANewExpressionItIsAConstructorItInitialisesTheNewlyCreatedObjectAndTheValuePropertyOfTheNewlyConstructedObjectIsSetToTostringValueOrToTheEmptyStringIfValueIsNotSupplied7()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.2/S15.5.2.1_A1_T17.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.2.1")]
        public void WhenStringIsCalledAsPartOfANewExpressionItIsAConstructorItInitialisesTheNewlyCreatedObjectAndTheValuePropertyOfTheNewlyConstructedObjectIsSetToTostringValueOrToTheEmptyStringIfValueIsNotSupplied8()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.2/S15.5.2.1_A1_T18.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.2.1")]
        public void WhenStringIsCalledAsPartOfANewExpressionItIsAConstructorItInitialisesTheNewlyCreatedObjectAndTheValuePropertyOfTheNewlyConstructedObjectIsSetToTostringValueOrToTheEmptyStringIfValueIsNotSupplied9()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.2/S15.5.2.1_A1_T19.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.2.1")]
        public void WhenStringIsCalledAsPartOfANewExpressionItIsAConstructorItInitialisesTheNewlyCreatedObjectAndTheValuePropertyOfTheNewlyConstructedObjectIsSetToTostringValueOrToTheEmptyStringIfValueIsNotSupplied10()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.2/S15.5.2.1_A1_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.2.1")]
        public void WhenStringIsCalledAsPartOfANewExpressionItIsAConstructorItInitialisesTheNewlyCreatedObjectAndTheValuePropertyOfTheNewlyConstructedObjectIsSetToTostringValueOrToTheEmptyStringIfValueIsNotSupplied11()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.2/S15.5.2.1_A1_T3.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.2.1")]
        public void WhenStringIsCalledAsPartOfANewExpressionItIsAConstructorItInitialisesTheNewlyCreatedObjectAndTheValuePropertyOfTheNewlyConstructedObjectIsSetToTostringValueOrToTheEmptyStringIfValueIsNotSupplied12()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.2/S15.5.2.1_A1_T4.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.2.1")]
        public void WhenStringIsCalledAsPartOfANewExpressionItIsAConstructorItInitialisesTheNewlyCreatedObjectAndTheValuePropertyOfTheNewlyConstructedObjectIsSetToTostringValueOrToTheEmptyStringIfValueIsNotSupplied13()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.2/S15.5.2.1_A1_T5.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.2.1")]
        public void WhenStringIsCalledAsPartOfANewExpressionItIsAConstructorItInitialisesTheNewlyCreatedObjectAndTheValuePropertyOfTheNewlyConstructedObjectIsSetToTostringValueOrToTheEmptyStringIfValueIsNotSupplied14()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.2/S15.5.2.1_A1_T6.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.2.1")]
        public void WhenStringIsCalledAsPartOfANewExpressionItIsAConstructorItInitialisesTheNewlyCreatedObjectAndTheValuePropertyOfTheNewlyConstructedObjectIsSetToTostringValueOrToTheEmptyStringIfValueIsNotSupplied15()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.2/S15.5.2.1_A1_T7.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.2.1")]
        public void WhenStringIsCalledAsPartOfANewExpressionItIsAConstructorItInitialisesTheNewlyCreatedObjectAndTheValuePropertyOfTheNewlyConstructedObjectIsSetToTostringValueOrToTheEmptyStringIfValueIsNotSupplied16()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.2/S15.5.2.1_A1_T8.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.2.1")]
        public void WhenStringIsCalledAsPartOfANewExpressionItIsAConstructorItInitialisesTheNewlyCreatedObjectAndTheValuePropertyOfTheNewlyConstructedObjectIsSetToTostringValueOrToTheEmptyStringIfValueIsNotSupplied17()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.2/S15.5.2.1_A1_T9.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.2.1")]
        public void ThePrototypePropertyOfTheNewlyConstructedObjectIsSetToTheOriginalStringPrototypeObject()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.2/S15.5.2.1_A2_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.2.1")]
        public void ThePrototypePropertyOfTheNewlyConstructedObjectIsSetToTheOriginalStringPrototypeObject2()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.2/S15.5.2.1_A2_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.2.1")]
        public void TheClassPropertyOfTheNewlyConstructedObjectIsSetToString()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.2/S15.5.2.1_A3.js", false);
        }


    }
}
