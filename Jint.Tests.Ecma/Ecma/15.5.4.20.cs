using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_5_4_20 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.5.4.20")]
        public void StringPrototypeTrimMustExistAsAFunction()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.20/15.5.4.20-0-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.20")]
        public void StringPrototypeTrimMustExistAsAFunctionTaking0Parameters()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.20/15.5.4.20-0-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.20")]
        public void StringPrototypeTrimThrowsTypeerrorWhenStringIsUndefined()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.20/15.5.4.20-1-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.20")]
        public void StringPrototypeTrimThrowsTypeerrorWhenStringIsNull()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.20/15.5.4.20-1-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.20")]
        public void StringPrototypeTrimWorksForPrimitiveTypeBoolean()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.20/15.5.4.20-1-3.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.20")]
        public void StringPrototypeTrimWorksForPrimitiveTypeNumber()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.20/15.5.4.20-1-4.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.20")]
        public void StringPrototypeTrimWorksForAnObject()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.20/15.5.4.20-1-5.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.20")]
        public void StringPrototypeTrimWorksForAnString()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.20/15.5.4.20-1-6.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.20")]
        public void StringPrototypeTrimWorksForAPrimitiveString()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.20/15.5.4.20-1-7.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.20")]
        public void StringPrototypeTrimWorksForAPrimitiveStringValueIsAbc()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.20/15.5.4.20-1-8.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.20")]
        public void StringPrototypeTrimWorksForAStringObjectWhichValueIsUndefined()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.20/15.5.4.20-1-9.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.20")]
        public void StringPrototypeTrimArgumentThisIsABooleanWhoseValueIsFalse()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.20/15.5.4.20-2-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.20")]
        public void StringPrototypeTrimArgumentThisIsANumberThatConvertsToAStringValueIsInfinity()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.20/15.5.4.20-2-10.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.20")]
        public void StringPrototypeTrimArgumentThisIsANumberThatConvertsToAStringValueIsInfinity2()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.20/15.5.4.20-2-11.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.20")]
        public void StringPrototypeTrimArgumentThisIsANumberThatConvertsToAStringValueIs1Following20Zeros()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.20/15.5.4.20-2-12.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.20")]
        public void StringPrototypeTrimArgumentThisIsANumberThatConvertsToAStringValueIs1Following21Zeros()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.20/15.5.4.20-2-13.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.20")]
        public void StringPrototypeTrimArgumentThisIsANumberThatConvertsToAStringValueIs1Following22Zeros()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.20/15.5.4.20-2-14.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.20")]
        public void StringPrototypeTrimArgumentThisIsANumberThatConvertsToAStringValueIs1E20()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.20/15.5.4.20-2-15.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.20")]
        public void StringPrototypeTrimArgumentThisIsANumberThatConvertsToStringValueIs1E21()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.20/15.5.4.20-2-16.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.20")]
        public void StringPrototypeTrimArgumentThisIsANumberThatConvertsToStringValueIs1E22()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.20/15.5.4.20-2-17.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.20")]
        public void StringPrototypeTrimArgumentThisIsANumberThatConvertsToAStringValueIs0000001()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.20/15.5.4.20-2-18.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.20")]
        public void StringPrototypeTrimArgumentArgumentThisIsANumberThatConvertsToAStringValueIs00000001()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.20/15.5.4.20-2-19.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.20")]
        public void StringPrototypeTrimArgumentThisIsABooleanWhoseValueIsTrue()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.20/15.5.4.20-2-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.20")]
        public void StringPrototypeTrimArgumentThisIsANumberThatConvertsToAStringValueIs000000001()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.20/15.5.4.20-2-20.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.20")]
        public void StringPrototypeTrimArgumentThisIsANumberThatConvertsToAStringValueIs1E7()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.20/15.5.4.20-2-21.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.20")]
        public void StringPrototypeTrimArgumentThisIsANumberThatConvertsToAStringValueIs1E6()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.20/15.5.4.20-2-22.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.20")]
        public void StringPrototypeTrimArgumentThisIsANumberThatConvertsToAStringValueIs1E5()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.20/15.5.4.20-2-23.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.20")]
        public void StringPrototypeTrimArgumentThisIsAnIntegerThatConvertsToAStringValueIs123()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.20/15.5.4.20-2-24.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.20")]
        public void StringPrototypeTrimArgumentThisIsADecimalThatConvertsToAStringValueIs123456()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.20/15.5.4.20-2-25.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.20")]
        public void StringPrototypeTrimArgumentThisIsANumberThatConvertsToAStringValueIs1Following20Zeros123()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.20/15.5.4.20-2-26.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.20")]
        public void StringPrototypeTrimArgumentThisIsANumberThatConvertsToAStringValueIs1231234567()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.20/15.5.4.20-2-27.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.20")]
        public void StringPrototypeTrimArgumentThisIsAnEmptyString()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.20/15.5.4.20-2-28.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.20")]
        public void StringPrototypeTrimArgumentThisIsAStringValueIsAbCd()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.20/15.5.4.20-2-29.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.20")]
        public void StringPrototypeTrimArgumentThisIsANumberThatConvertsToAStringValueIsNan()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.20/15.5.4.20-2-3.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.20")]
        public void StringPrototypeTrimArgumentThisIsAStringValueIsUndefined()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.20/15.5.4.20-2-30.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.20")]
        public void StringPrototypeTrimArgumentThisIsAStringValueIsNull()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.20/15.5.4.20-2-31.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.20")]
        public void StringPrototypeTrimArgumentThisIsAStringValueIs123Abc()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.20/15.5.4.20-2-32.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.20")]
        public void StringPrototypeTrimArgumentThisIsAStringValueIs1()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.20/15.5.4.20-2-33.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.20")]
        public void StringPrototypeTrimThisIsAnArrayThatConvertsToAString()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.20/15.5.4.20-2-34.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.20")]
        public void StringPrototypeTrimThisIsAStringObjectThatConvertsToAString()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.20/15.5.4.20-2-35.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.20")]
        public void StringPrototypeTrimThisIsABooleanObjectThatConvertsToAString()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.20/15.5.4.20-2-36.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.20")]
        public void StringPrototypeTrimThisIsANumberObjectThatConvertsToAString()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.20/15.5.4.20-2-37.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.20")]
        public void StringPrototypeTrimThisIsAnObjectWhichHasAnOwnTostringMethod()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.20/15.5.4.20-2-38.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.20")]
        public void StringPrototypeTrimThisIsAnObjectWhichHasAnOwnValueofMethod()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.20/15.5.4.20-2-39.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.20")]
        public void StringPrototypeTrimArgumentThisIsANumberThatConvertsToAStringValueIs0()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.20/15.5.4.20-2-4.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.20")]
        public void StringPrototypeTrimThisIsAnObjectThatHasAnOwnTostringMethodThatReturnsAnObjectAndValueofMethodThatReturnsAPrimitiveValue()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.20/15.5.4.20-2-40.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.20")]
        public void StringPrototypeTrimThisIsAnObjectWhichHasAnOwnTostringAndValueofMethod()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.20/15.5.4.20-2-41.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.20")]
        public void StringPrototypeTrimTypeerrorExceptionWasThrownWhenThisIsAnObjectThatBothTostringAndValueofWouldnTReturnPrimitiveValue()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.20/15.5.4.20-2-42.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.20")]
        public void StringPrototypeTrimThisIsAnObjectWithAnOwnValueofAndInheritedTostringMethodsWithHintStringVerifyInheritedTostringMethodWillBeCalledFirst()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.20/15.5.4.20-2-43.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.20")]
        public void StringPrototypeTrimThisIsAStringThatContainsEastAsianCharactersValueIsSd()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.20/15.5.4.20-2-44.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.20")]
        public void StringPrototypeTrimThisIsAStringThatContainsWhiteSpaceCharacterNumberObjectAndNullCharacters()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.20/15.5.4.20-2-45.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.20")]
        public void StringPrototypeTrimThisIsAFunctionObjectThatConvertsToAString()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.20/15.5.4.20-2-46.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.20")]
        public void StringPrototypeTrimThisIsAObjectObjectThatConvertsToAString()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.20/15.5.4.20-2-47.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.20")]
        public void StringPrototypeTrimThisIsARegexpObjectThatConvertsToAString()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.20/15.5.4.20-2-49.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.20")]
        public void StringPrototypeTrimArgumentThisIsANumberThatConvertsToAStringValueIs02()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.20/15.5.4.20-2-5.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.20")]
        public void StringPrototypeTrimThisIsAErrorObjectThatConvertsToAString()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.20/15.5.4.20-2-50.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.20")]
        public void StringPrototypeTrimThisIsAArgumentsObjectThatConvertsToAString()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.20/15.5.4.20-2-51.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.20")]
        public void StringPrototypeTrimArgumentThisIsANumberThatConvertsToAStringValueIs03()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.20/15.5.4.20-2-6.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.20")]
        public void StringPrototypeTrimArgumentThisIsANumberThatConvertsToAStringValueIsPositiveNumber()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.20/15.5.4.20-2-7.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.20")]
        public void StringPrototypeTrimArgumentThisIsANumberThatConvertsToAStringValueIsNegativeNumber()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.20/15.5.4.20-2-8.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.20")]
        public void StringPrototypeTrimArgumentThisIsANumberThatConvertsToAStringValueIsInfinity3()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.20/15.5.4.20-2-9.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.20")]
        public void StringPrototypeTrimSIsAStringWithAllLineterminator()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.20/15.5.4.20-3-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.20")]
        public void StringPrototypeTrimSIsAStringWithNullCharacterU0000()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.20/15.5.4.20-3-10.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.20")]
        public void StringPrototypeTrimSIsAStringThatStartsWithNullCharacter()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.20/15.5.4.20-3-11.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.20")]
        public void StringPrototypeTrimSIsAStringThatEndsWithNullCharacter()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.20/15.5.4.20-3-12.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.20")]
        public void StringPrototypeTrimSIsAStringThatStartsWithNullCharacterAndEndsWithNullCharacter()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.20/15.5.4.20-3-13.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.20")]
        public void StringPrototypeTrimSIsAStringThatHasNullCharacterInTheMiddle()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.20/15.5.4.20-3-14.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.20")]
        public void StringPrototypeTrimSIsAStringWithAllWhitespace()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.20/15.5.4.20-3-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.20")]
        public void StringPrototypeTrimSIsAStringWithAllUnionOfWhitespaceAndLineterminator()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.20/15.5.4.20-3-3.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.20")]
        public void StringPrototypeTrimSIsAStringStartWithUnionOfAllLineterminatorAndAllWhitespace()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.20/15.5.4.20-3-4.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.20")]
        public void StringPrototypeTrimSIsAStringEndWithUnionOfAllLineterminatorAndAllWhitespace()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.20/15.5.4.20-3-5.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.20")]
        public void StringPrototypeTrimSIsAStringStartWithUnionOfAllLineterminatorAndAllWhitespaceAndEndWithUnionOfAllLineterminatorAndAllWhitespace()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.20/15.5.4.20-3-6.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.20")]
        public void StringPrototypeTrimSIsAStringThatUnionOfLineterminatorAndWhitespaceInTheMiddle()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.20/15.5.4.20-3-7.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.20")]
        public void StringPrototypeTrimSIsAStringWithAllNullCharacter()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.20/15.5.4.20-3-8.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.20")]
        public void StringPrototypeTrimSIsAStringWithNullCharacter0()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.20/15.5.4.20-3-9.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.20")]
        public void StringPrototypeTrimHandlesMultilineStringWithWhitepaceAndLineterminators()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.20/15.5.4.20-4-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.20")]
        public void StringPrototypeTrimHandlesWhitepaceAndLineterminatorsUfeffabc()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.20/15.5.4.20-4-10.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.20")]
        public void StringPrototypeTrimHandlesWhitepaceAndLineterminatorsAbcU0009()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.20/15.5.4.20-4-11.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.20")]
        public void StringPrototypeTrimHandlesWhitepaceAndLineterminatorsAbcU000B()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.20/15.5.4.20-4-12.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.20")]
        public void StringPrototypeTrimHandlesWhitepaceAndLineterminatorsAbcU000C()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.20/15.5.4.20-4-13.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.20")]
        public void StringPrototypeTrimHandlesWhitepaceAndLineterminatorsAbcU0020()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.20/15.5.4.20-4-14.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.20")]
        public void StringPrototypeTrimHandlesWhitepaceAndLineterminatorsAbcU00A0()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.20/15.5.4.20-4-16.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.20")]
        public void StringPrototypeTrimHandlesWhitepaceAndLineterminatorsAbcUfeff()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.20/15.5.4.20-4-18.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.20")]
        public void StringPrototypeTrimHandlesWhitepaceAndLineterminatorsU0009AbcU0009()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.20/15.5.4.20-4-19.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.20")]
        public void StringPrototypeTrimHandlesWhitepaceAndLineterminatorsU0009AbcU00092()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.20/15.5.4.20-4-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.20")]
        public void StringPrototypeTrimHandlesWhitepaceAndLineterminatorsU000BabcU000B()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.20/15.5.4.20-4-20.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.20")]
        public void StringPrototypeTrimHandlesWhitepaceAndLineterminatorsU000CabcU000C()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.20/15.5.4.20-4-21.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.20")]
        public void StringPrototypeTrimHandlesWhitepaceAndLineterminatorsU0020AbcU0020()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.20/15.5.4.20-4-22.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.20")]
        public void StringPrototypeTrimHandlesWhitepaceAndLineterminatorsU00A0AbcU00A0()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.20/15.5.4.20-4-24.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.20")]
        public void StringPrototypeTrimHandlesWhitepaceAndLineterminatorsU0009U0009()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.20/15.5.4.20-4-27.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.20")]
        public void StringPrototypeTrimHandlesWhitepaceAndLineterminatorsU000BU000B()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.20/15.5.4.20-4-28.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.20")]
        public void StringPrototypeTrimHandlesWhitepaceAndLineterminatorsU000CU000C()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.20/15.5.4.20-4-29.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.20")]
        public void StringPrototypeTrimHandlesWhitepaceAndLineterminatorsU0009Abc()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.20/15.5.4.20-4-3.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.20")]
        public void StringPrototypeTrimHandlesWhitepaceAndLineterminatorsU0020U0020()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.20/15.5.4.20-4-30.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.20")]
        public void StringPrototypeTrimHandlesWhitepaceAndLineterminatorsU00A0U00A0()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.20/15.5.4.20-4-32.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.20")]
        public void StringPrototypeTrimHandlesWhitepaceAndLineterminatorsUfeffUfeff()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.20/15.5.4.20-4-34.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.20")]
        public void StringPrototypeTrimHandlesWhitepaceAndLineterminatorsAbU0009C()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.20/15.5.4.20-4-35.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.20")]
        public void StringPrototypeTrimHandlesWhitepaceAndLineterminatorsAbU000Bc()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.20/15.5.4.20-4-36.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.20")]
        public void StringPrototypeTrimHandlesWhitepaceAndLineterminatorsAbU000Cc()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.20/15.5.4.20-4-37.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.20")]
        public void StringPrototypeTrimHandlesWhitepaceAndLineterminatorsAbU0020C()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.20/15.5.4.20-4-38.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.20")]
        public void StringPrototypeTrimHandlesWhitepaceAndLineterminatorsAbU0085C()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.20/15.5.4.20-4-39.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.20")]
        public void StringPrototypeTrimHandlesWhitepaceAndLineterminatorsU000Babc()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.20/15.5.4.20-4-4.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.20")]
        public void StringPrototypeTrimHandlesWhitepaceAndLineterminatorsAbU00A0C()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.20/15.5.4.20-4-40.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.20")]
        public void StringPrototypeTrimHandlesWhitepaceAndLineterminatorsAbU200Bc()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.20/15.5.4.20-4-41.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.20")]
        public void StringPrototypeTrimHandlesWhitepaceAndLineterminatorsAbUfeffc()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.20/15.5.4.20-4-42.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.20")]
        public void StringPrototypeTrimHandlesWhitepaceAndLineterminatorsU000Aabc()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.20/15.5.4.20-4-43.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.20")]
        public void StringPrototypeTrimHandlesWhitepaceAndLineterminatorsU000Dabc()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.20/15.5.4.20-4-44.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.20")]
        public void StringPrototypeTrimHandlesWhitepaceAndLineterminatorsU2028Abc()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.20/15.5.4.20-4-45.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.20")]
        public void StringPrototypeTrimHandlesWhitepaceAndLineterminatorsU2029Abc()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.20/15.5.4.20-4-46.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.20")]
        public void StringPrototypeTrimHandlesWhitepaceAndLineterminatorsAbcU000A()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.20/15.5.4.20-4-47.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.20")]
        public void StringPrototypeTrimHandlesWhitepaceAndLineterminatorsAbcU000D()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.20/15.5.4.20-4-48.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.20")]
        public void StringPrototypeTrimHandlesWhitepaceAndLineterminatorsAbcU2028()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.20/15.5.4.20-4-49.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.20")]
        public void StringPrototypeTrimHandlesWhitepaceAndLineterminatorsU000Cabc()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.20/15.5.4.20-4-5.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.20")]
        public void StringPrototypeTrimHandlesWhitepaceAndLineterminatorsAbcU2029()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.20/15.5.4.20-4-50.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.20")]
        public void StringPrototypeTrimHandlesWhitepaceAndLineterminatorsU000AabcU000A()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.20/15.5.4.20-4-51.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.20")]
        public void StringPrototypeTrimHandlesWhitepaceAndLineterminatorsU000DabcU000D()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.20/15.5.4.20-4-52.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.20")]
        public void StringPrototypeTrimHandlesWhitepaceAndLineterminatorsU2028AbcU2028()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.20/15.5.4.20-4-53.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.20")]
        public void StringPrototypeTrimHandlesWhitepaceAndLineterminatorsU2029AbcU2029()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.20/15.5.4.20-4-54.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.20")]
        public void StringPrototypeTrimHandlesWhitepaceAndLineterminatorsU000AU000A()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.20/15.5.4.20-4-55.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.20")]
        public void StringPrototypeTrimHandlesWhitepaceAndLineterminatorsU000DU000D()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.20/15.5.4.20-4-56.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.20")]
        public void StringPrototypeTrimHandlesWhitepaceAndLineterminatorsU2028U2028()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.20/15.5.4.20-4-57.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.20")]
        public void StringPrototypeTrimHandlesWhitepaceAndLineterminatorsU2029U2029()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.20/15.5.4.20-4-58.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.20")]
        public void StringPrototypeTrimHandlesWhitepaceAndLineterminatorsU2029AbcAsAMultilineString()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.20/15.5.4.20-4-59.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.20")]
        public void StringPrototypeTrimHandlesWhitepaceAndLineterminatorsU0020Abc()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.20/15.5.4.20-4-6.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.20")]
        public void StringPrototypeTrimHandlesWhitepaceAndLineterminatorsStringWithJustBlanks()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.20/15.5.4.20-4-60.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.20")]
        public void StringPrototypeTrimHandlesWhitepaceAndLineterminatorsU00A0Abc()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.20/15.5.4.20-4-8.js", false);
        }


    }
}
