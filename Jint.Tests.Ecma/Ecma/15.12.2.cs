using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_12_2 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.12.2")]
        public void TheNameJsonMustBeBoundToAnObjectSection15SaysThatEveryBuiltInFunctionObjectDescribedInThisSectionWhetherAsAConstructorAnOrdinaryFunctionOrBothHasALengthPropertyWhoseValueIsAnIntegerUnlessOtherwiseSpecifiedThisValueIsEqualToTheLargestNumberOfNamedArgumentsShownInTheSectionHeadingsForTheFunctionDescriptionIncludingOptionalParametersThisDefaultAppliesToJsonParseAndItMustExistAsAFunctionTaking2Parameters()
        {
			RunTest(@"TestCases/ch15/15.12/15.12.2/15.12.2-0-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.12.2")]
        public void TheNameJsonMustBeBoundToAnObjectSection15SaysThatEveryBuiltInFunctionObjectDescribedInThisSectionWhetherAsAConstructorAnOrdinaryFunctionOrBothHasALengthPropertyWhoseValueIsAnIntegerUnlessOtherwiseSpecifiedThisValueIsEqualToTheLargestNumberOfNamedArgumentsShownInTheSectionHeadingsForTheFunctionDescriptionIncludingOptionalParametersThisDefaultAppliesToJsonParseAndItMustExistAsAFunctionTaking2Parameters2()
        {
			RunTest(@"TestCases/ch15/15.12/15.12.2/15.12.2-0-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.12.2")]
        public void TheInitialValueOfConfigurableOnJsonIsTrueThisMeansWeShouldBeAbleToDelete8625TheStringifyAndParseProperties()
        {
			RunTest(@"TestCases/ch15/15.12/15.12.2/15.12.2-0-3.js", false);
        }

        [Fact]
        [Trait("Category", "15.12.2")]
        public void JsonParseParsingAnObjectWherePropertyNameIsANullCharacter()
        {
			RunTest(@"TestCases/ch15/15.12/15.12.2/15.12.2-2-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.12.2")]
        public void JsonParseParsingAnObjectWherePropertyValueMiddlesWithANullCharacter()
        {
			RunTest(@"TestCases/ch15/15.12/15.12.2/15.12.2-2-10.js", false);
        }

        [Fact]
        [Trait("Category", "15.12.2")]
        public void JsonParseParsingAnObjectWherePropertyNameStartsWithANullCharacter()
        {
			RunTest(@"TestCases/ch15/15.12/15.12.2/15.12.2-2-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.12.2")]
        public void JsonParseParsingAnObjectWherePropertyNameEndsWithANullCharacter()
        {
			RunTest(@"TestCases/ch15/15.12/15.12.2/15.12.2-2-3.js", false);
        }

        [Fact]
        [Trait("Category", "15.12.2")]
        public void JsonParseParsingAnObjectWherePropertyNameStartsAndEndsWithANullCharacter()
        {
			RunTest(@"TestCases/ch15/15.12/15.12.2/15.12.2-2-4.js", false);
        }

        [Fact]
        [Trait("Category", "15.12.2")]
        public void JsonParseParsingAnObjectWherePropertyNameMiddlesWithANullCharacter()
        {
			RunTest(@"TestCases/ch15/15.12/15.12.2/15.12.2-2-5.js", false);
        }

        [Fact]
        [Trait("Category", "15.12.2")]
        public void JsonParseParsingAnObjectWherePropertyValueIsANullCharacter()
        {
			RunTest(@"TestCases/ch15/15.12/15.12.2/15.12.2-2-6.js", false);
        }

        [Fact]
        [Trait("Category", "15.12.2")]
        public void JsonParseParsingAnObjectWherePropertyValueStartsWithANullCharacter()
        {
			RunTest(@"TestCases/ch15/15.12/15.12.2/15.12.2-2-7.js", false);
        }

        [Fact]
        [Trait("Category", "15.12.2")]
        public void JsonParseParsingAnObjectWherePropertyValueEndsWithANullCharacter()
        {
			RunTest(@"TestCases/ch15/15.12/15.12.2/15.12.2-2-8.js", false);
        }

        [Fact]
        [Trait("Category", "15.12.2")]
        public void JsonParseParsingAnObjectWherePropertyValueStartsAndEndsWithANullCharacter()
        {
			RunTest(@"TestCases/ch15/15.12/15.12.2/15.12.2-2-9.js", false);
        }

        [Fact]
        [Trait("Category", "15.12.2")]
        public void JsonParseMustCreateAPropertyWithTheGivenPropertyName()
        {
			RunTest(@"TestCases/ch15/15.12/15.12.2/S15.12.2_A1.js", false);
        }


    }
}
