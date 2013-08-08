using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_12_3 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.12.3")]
        public void TheNameJsonMustBeBoundToAnObjectSection15SaysThatEveryBuiltInFunctionObjectDescribedInThisSectionWhetherAsAConstructorAnOrdinaryFunctionOrBothHasALengthPropertyWhoseValueIsAnIntegerUnlessOtherwiseSpecifiedThisValueIsEqualToTheLargestNumberOfNamedArgumentsShownInTheSectionHeadingsForTheFunctionDescriptionIncludingOptionalParametersThisDefaultAppliesToJsonStringifyAndItMustExistAsAFunctionTaking3Parameters()
        {
			RunTest(@"TestCases/ch15/15.12/15.12.3/15.12.3-0-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.12.3")]
        public void TheNameJsonMustBeBoundToAnObjectSection15SaysThatEveryBuiltInFunctionObjectDescribedInThisSectionWhetherAsAConstructorAnOrdinaryFunctionOrBothHasALengthPropertyWhoseValueIsAnIntegerUnlessOtherwiseSpecifiedThisValueIsEqualToTheLargestNumberOfNamedArgumentsShownInTheSectionHeadingsForTheFunctionDescriptionIncludingOptionalParametersThisDefaultAppliesToJsonStringifyAndItMustExistAsAFunctionTaking3Parameters2()
        {
			RunTest(@"TestCases/ch15/15.12/15.12.3/15.12.3-0-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.12.3")]
        public void ThisTestShouldBeRunWithoutAnyBuiltInsBeingAddedAugmentedTheInitialValueOfConfigurableOnJsonIsTrueThisMeansWeShouldBeAbleToDelete8625TheStringifyAndParseProperties()
        {
			RunTest(@"TestCases/ch15/15.12/15.12.3/15.12.3-0-3.js", false);
        }

        [Fact]
        [Trait("Category", "15.12.3")]
        public void JsonStringifyUndefinedReturnsUndefined()
        {
			RunTest(@"TestCases/ch15/15.12/15.12.3/15.12.3-11-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.12.3")]
        public void AJsonStringifyReplacerFunctionAppliedToATopLevelScalarValueCanReturnUndefined()
        {
			RunTest(@"TestCases/ch15/15.12/15.12.3/15.12.3-11-10.js", false);
        }

        [Fact]
        [Trait("Category", "15.12.3")]
        public void AJsonStringifyReplacerFunctionAppliedToATopLevelObjectCanReturnUndefined()
        {
			RunTest(@"TestCases/ch15/15.12/15.12.3/15.12.3-11-11.js", false);
        }

        [Fact]
        [Trait("Category", "15.12.3")]
        public void AJsonStringifyReplacerFunctionAppliedToATopLevelScalarCanReturnAnArray()
        {
			RunTest(@"TestCases/ch15/15.12/15.12.3/15.12.3-11-12.js", false);
        }

        [Fact]
        [Trait("Category", "15.12.3")]
        public void AJsonStringifyReplacerFunctionAppliedToATopLevelScalarCanReturnAnObject()
        {
			RunTest(@"TestCases/ch15/15.12/15.12.3/15.12.3-11-13.js", false);
        }

        [Fact]
        [Trait("Category", "15.12.3")]
        public void ApplyingJsonStringifyToAFunctionReturnsUndefined()
        {
			RunTest(@"TestCases/ch15/15.12/15.12.3/15.12.3-11-14.js", false);
        }

        [Fact]
        [Trait("Category", "15.12.3")]
        public void ApplyingJsonStringifyWithAReplacerFunctionToAFunctionReturnsTheReplacerValue()
        {
			RunTest(@"TestCases/ch15/15.12/15.12.3/15.12.3-11-15.js", false);
        }

        [Fact]
        [Trait("Category", "15.12.3")]
        public void JsonStringifyStringifyingAnObjectWherePropertyNameIsTheUnionOfAllNullCharacterTheAbstractOperationQuoteValueStep2C()
        {
			RunTest(@"TestCases/ch15/15.12/15.12.3/15.12.3-11-16.js", false);
        }

        [Fact]
        [Trait("Category", "15.12.3")]
        public void JsonStringifyStringifyingAnObjectWherePropertyNameStartsWithTheUnionOfAllNullCharacterTheAbstractOperationQuoteValueStep2C()
        {
			RunTest(@"TestCases/ch15/15.12/15.12.3/15.12.3-11-17.js", false);
        }

        [Fact]
        [Trait("Category", "15.12.3")]
        public void JsonStringifyStringifyingAnObjectWherePropertyNameEndsWithTheUnionOfAllNullCharacterTheAbstractOperationQuoteValueStep2C()
        {
			RunTest(@"TestCases/ch15/15.12/15.12.3/15.12.3-11-18.js", false);
        }

        [Fact]
        [Trait("Category", "15.12.3")]
        public void JsonStringifyStringifyingAnObjectWherePropertyNameStartsAndEndsWithTheUnionOfAllNullCharacterTheAbstractOperationQuoteValueStep2C()
        {
			RunTest(@"TestCases/ch15/15.12/15.12.3/15.12.3-11-19.js", false);
        }

        [Fact]
        [Trait("Category", "15.12.3")]
        public void AJsonStringifyReplacerFunctionWorksIsAppliedToATopLevelUndefinedValue()
        {
			RunTest(@"TestCases/ch15/15.12/15.12.3/15.12.3-11-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.12.3")]
        public void JsonStringifyStringifyingAnObjectWherePropertyNameMiddlesWithTheUnionOfAllNullCharacterTheAbstractOperationQuoteValueStep2C()
        {
			RunTest(@"TestCases/ch15/15.12/15.12.3/15.12.3-11-20.js", false);
        }

        [Fact]
        [Trait("Category", "15.12.3")]
        public void JsonStringifyStringifyingAnObjectWherePropertyValueIsTheUnionOfAllNullCharacterTheAbstractOperationQuoteValueStep2C()
        {
			RunTest(@"TestCases/ch15/15.12/15.12.3/15.12.3-11-21.js", false);
        }

        [Fact]
        [Trait("Category", "15.12.3")]
        public void JsonStringifyStringifyingAnObjectWherePropertyValueStartsWithTheUnionOfAllNullCharacterTheAbstractOperationQuoteValueStep2C()
        {
			RunTest(@"TestCases/ch15/15.12/15.12.3/15.12.3-11-22.js", false);
        }

        [Fact]
        [Trait("Category", "15.12.3")]
        public void JsonStringifyStringifyingAnObjectWherePropertyValueEndsWithTheUnionOfAllNullCharacterTheAbstractOperationQuoteValueStep2C()
        {
			RunTest(@"TestCases/ch15/15.12/15.12.3/15.12.3-11-23.js", false);
        }

        [Fact]
        [Trait("Category", "15.12.3")]
        public void JsonStringifyStringifyingAnObjectWherePropertyValueStartsAndEndsWithTheUnionOfAllNullCharacterTheAbstractOperationQuoteValueStep2C()
        {
			RunTest(@"TestCases/ch15/15.12/15.12.3/15.12.3-11-24.js", false);
        }

        [Fact]
        [Trait("Category", "15.12.3")]
        public void JsonStringifyStringifyingAnObjectWherePropertyValueMiddlesWithTheUnionOfAllNullCharacterTheAbstractOperationQuoteValueStep2C()
        {
			RunTest(@"TestCases/ch15/15.12/15.12.3/15.12.3-11-25.js", false);
        }

        [Fact]
        [Trait("Category", "15.12.3")]
        public void JsonStringifyTheLastElementOfTheConcatenationIsTheAbstractOperationJaValueStep10BIii()
        {
			RunTest(@"TestCases/ch15/15.12/15.12.3/15.12.3-11-26.js", false);
        }

        [Fact]
        [Trait("Category", "15.12.3")]
        public void AJsonStringifyCorrectlyWorksOnTopLevelStringValues()
        {
			RunTest(@"TestCases/ch15/15.12/15.12.3/15.12.3-11-3.js", false);
        }

        [Fact]
        [Trait("Category", "15.12.3")]
        public void JsonStringifyCorrectlyWorksOnTopLevelNumberValues()
        {
			RunTest(@"TestCases/ch15/15.12/15.12.3/15.12.3-11-4.js", false);
        }

        [Fact]
        [Trait("Category", "15.12.3")]
        public void JsonStringifyCorrectlyWorksOnTopLevelBooleanValues()
        {
			RunTest(@"TestCases/ch15/15.12/15.12.3/15.12.3-11-5.js", false);
        }

        [Fact]
        [Trait("Category", "15.12.3")]
        public void JsonStringifyCorrectlyWorksOnTopLevelNullValues()
        {
			RunTest(@"TestCases/ch15/15.12/15.12.3/15.12.3-11-6.js", false);
        }

        [Fact]
        [Trait("Category", "15.12.3")]
        public void JsonStringifyCorrectlyWorksOnTopLevelNumberObjects()
        {
			RunTest(@"TestCases/ch15/15.12/15.12.3/15.12.3-11-7.js", false);
        }

        [Fact]
        [Trait("Category", "15.12.3")]
        public void JsonStringifyCorrectlyWorksOnTopLevelStringObjects()
        {
			RunTest(@"TestCases/ch15/15.12/15.12.3/15.12.3-11-8.js", false);
        }

        [Fact]
        [Trait("Category", "15.12.3")]
        public void JsonStringifyCorrectlyWorksOnTopLevelBooleanObjects()
        {
			RunTest(@"TestCases/ch15/15.12/15.12.3/15.12.3-11-9.js", false);
        }

        [Fact]
        [Trait("Category", "15.12.3")]
        public void JsonStringifyIgnoresReplacerArugumentsThatAreNotFunctionsOrArrays()
        {
			RunTest(@"TestCases/ch15/15.12/15.12.3/15.12.3-4-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.12.3")]
        public void JsonStringifyConvertsNumberWrapperObjectSpaceArugumentsToNumberValues()
        {
			RunTest(@"TestCases/ch15/15.12/15.12.3/15.12.3-5-a-i-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.12.3")]
        public void JsonStringifyConvertsStringWrapperObjectSpaceArugumentsToStringValues()
        {
			RunTest(@"TestCases/ch15/15.12/15.12.3/15.12.3-5-b-i-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.12.3")]
        public void JsonStringifyTreatsNumericSpaceArgumentsGreaterThan10TheSameAsASpaceArgumentOf10()
        {
			RunTest(@"TestCases/ch15/15.12/15.12.3/15.12.3-6-a-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.12.3")]
        public void JsonStringifyTruccatesNonIntegerNumericSpaceArgumentsToTheirIntegerPart()
        {
			RunTest(@"TestCases/ch15/15.12/15.12.3/15.12.3-6-a-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.12.3")]
        public void JsonStringifyTreatsNumericSpaceArgumentsLessThan10999999TheSameAsEmptryStringSpaceArgument()
        {
			RunTest(@"TestCases/ch15/15.12/15.12.3/15.12.3-6-b-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.12.3")]
        public void JsonStringifyTreatsNumericSpaceArgumentsLessThan10TheSameAsEmptryStringSpaceArgument()
        {
			RunTest(@"TestCases/ch15/15.12/15.12.3/15.12.3-6-b-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.12.3")]
        public void JsonStringifyTreatsNumericSpaceArgumentsLessThan15TheSameAsEmptryStringSpaceArgument()
        {
			RunTest(@"TestCases/ch15/15.12/15.12.3/15.12.3-6-b-3.js", false);
        }

        [Fact]
        [Trait("Category", "15.12.3")]
        public void JsonStringifyTreatsNumericSpaceArgumentsInTheRange110IsEquivalentToAStringOfSpacesOfThatLength()
        {
			RunTest(@"TestCases/ch15/15.12/15.12.3/15.12.3-6-b-4.js", false);
        }

        [Fact]
        [Trait("Category", "15.12.3")]
        public void JsonStringifyOnlyUsesTheFirst10CharactersOfAStringSpaceArguments()
        {
			RunTest(@"TestCases/ch15/15.12/15.12.3/15.12.3-7-a-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.12.3")]
        public void JsonStringifyTreatsAnEmptyStringSpaceArgumentTheSameAsAMissingSpaceArgument()
        {
			RunTest(@"TestCases/ch15/15.12/15.12.3/15.12.3-8-a-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.12.3")]
        public void JsonStringifyTreatsAnBooleanSpaceArgumentTheSameAsAMissingSpaceArgument()
        {
			RunTest(@"TestCases/ch15/15.12/15.12.3/15.12.3-8-a-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.12.3")]
        public void JsonStringifyTreatsAnNullSpaceArgumentTheSameAsAMissingSpaceArgument()
        {
			RunTest(@"TestCases/ch15/15.12/15.12.3/15.12.3-8-a-3.js", false);
        }

        [Fact]
        [Trait("Category", "15.12.3")]
        public void JsonStringifyTreatsAnBooleanWrapperSpaceArgumentTheSameAsAMissingSpaceArgument()
        {
			RunTest(@"TestCases/ch15/15.12/15.12.3/15.12.3-8-a-4.js", false);
        }

        [Fact]
        [Trait("Category", "15.12.3")]
        public void JsonStringifyTreatsNonNumberOrStringObjectSpaceArgumentsTheSameAsAMissingSpaceArgument()
        {
			RunTest(@"TestCases/ch15/15.12/15.12.3/15.12.3-8-a-5.js", false);
        }

        [Fact]
        [Trait("Category", "15.12.3")]
        public void JsonStringifyConvertsStringWrapperObjectsReturnedFromATojsonCallToLiteralStrings()
        {
			RunTest(@"TestCases/ch15/15.12/15.12.3/15.12.3_2-2-b-i-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.12.3")]
        public void JsonStringifyConvertsNumberWrapperObjectsReturnedFromATojsonCallToLiteralNumber()
        {
			RunTest(@"TestCases/ch15/15.12/15.12.3/15.12.3_2-2-b-i-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.12.3")]
        public void JsonStringifyConvertsBooleanWrapperObjectsReturnedFromATojsonCallToLiteralBooleanValues()
        {
			RunTest(@"TestCases/ch15/15.12/15.12.3/15.12.3_2-2-b-i-3.js", false);
        }

        [Fact]
        [Trait("Category", "15.12.3")]
        public void JsonStringifyConvertsStringWrapperObjectsReturnedFromReplacerFunctionsToLiteralStrings()
        {
			RunTest(@"TestCases/ch15/15.12/15.12.3/15.12.3_2-3-a-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.12.3")]
        public void JsonStringifyConvertsNumberWrapperObjectsReturnedFromReplacerFunctionsToLiteralNumbers()
        {
			RunTest(@"TestCases/ch15/15.12/15.12.3/15.12.3_2-3-a-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.12.3")]
        public void JsonStringifyConvertsBooleanWrapperObjectsReturnedFromReplacerFunctionsToLiteralNumbers()
        {
			RunTest(@"TestCases/ch15/15.12/15.12.3/15.12.3_2-3-a-3.js", false);
        }

        [Fact]
        [Trait("Category", "15.12.3")]
        public void JsonStringifyACircularObjectThrowsAError()
        {
			RunTest(@"TestCases/ch15/15.12/15.12.3/15.12.3_4-1-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.12.3")]
        public void JsonStringifyACircularObjectThrowsATypeerror()
        {
			RunTest(@"TestCases/ch15/15.12/15.12.3/15.12.3_4-1-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.12.3")]
        public void JsonStringifyAIndirectlyCircularObjectThrowsAError()
        {
			RunTest(@"TestCases/ch15/15.12/15.12.3/15.12.3_4-1-3.js", false);
        }


    }
}
