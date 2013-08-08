using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_4_4_3 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.4.4.3")]
        public void TheElementsOfTheArrayAreConvertedToStringsUsingTheirTolocalestringMethodsAndTheseStringsAreThenConcatenatedSeparatedByOccurrencesOfASeparatorStringThatHasBeenDerivedInAnImplementationDefinedLocaleSpecificWay()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.3/S15.4.4.3_A1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.3")]
        public void GetFromNotAnInheritedProperty()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.3/S15.4.4.3_A3_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.3")]
        public void TheLengthPropertyOfTolocalestringHasTheAttributeDontenum()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.3/S15.4.4.3_A4.1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.3")]
        public void TheLengthPropertyOfTolocalestringHasTheAttributeDontdelete()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.3/S15.4.4.3_A4.2.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.3")]
        public void TheLengthPropertyOfTolocalestringHasTheAttributeReadonly()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.3/S15.4.4.3_A4.3.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.3")]
        public void TheLengthPropertyOfTolocalestringIs0()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.3/S15.4.4.3_A4.4.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.3")]
        public void TheTolocalestringPropertyOfArrayHasTheAttributeDontenum()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.3/S15.4.4.3_A4.5.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.3")]
        public void TheTolocalestringPropertyOfArrayHasNotPrototypeProperty()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.3/S15.4.4.3_A4.6.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.3")]
        public void TheTolocalestringPropertyOfArrayCanTBeUsedAsConstructor()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.3/S15.4.4.3_A4.7.js", false);
        }


    }
}
