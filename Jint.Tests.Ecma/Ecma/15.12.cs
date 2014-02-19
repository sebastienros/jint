using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_12 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.12")]
        public void ThisTestShouldBeRunWithoutAnyBuiltInsBeingAddedAugmentedTheNameJsonMustBeBoundToAnObject42CallsOutJsonAsOneOfTheBuiltInObjects()
        {
			RunTest(@"TestCases/ch15/15.12/15.12-0-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.12")]
        public void ThisTestShouldBeRunWithoutAnyBuiltInsBeingAddedAugmentedTheNameJsonMustBeBoundToAnObjectAndMustNotSupportConstructStep4In1122ShouldThrowATypeerrorException()
        {
			RunTest(@"TestCases/ch15/15.12/15.12-0-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.12")]
        public void ThisTestShouldBeRunWithoutAnyBuiltInsBeingAddedAugmentedTheNameJsonMustBeBoundToAnObjectAndMustNotSupportCallStep5In1123ShouldThrowATypeerrorException()
        {
			RunTest(@"TestCases/ch15/15.12/15.12-0-3.js", false);
        }

        [Fact]
        [Trait("Category", "15.12")]
        public void ThisTestShouldBeRunWithoutAnyBuiltInsBeingAddedAugmentedTheLastParagraphInSection15SaysEveryOtherPropertyDescribedInThisSectionHasTheAttributeEnumerableFalseUnlessOtherwiseSpecifiedThisDefaultAppliesToThePropertiesOnJsonAndWeShouldNotBeAbleToEnumerateThem()
        {
			RunTest(@"TestCases/ch15/15.12/15.12-0-4.js", false);
        }


    }
}
