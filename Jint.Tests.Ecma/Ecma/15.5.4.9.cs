using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_5_4_9 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.5.4.9")]
        public void TestsThatStringPrototypeLocalecompareTreatsAMissingThatArgumentUndefinedAndUndefinedAsEquivalent()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.9/15.5.4.9_3.js", false);
        }

        [Fact(Skip = "String must be normalized for this unit test to pass. string.Normalize() is not available for Portable library project")]
        [Trait("Category", "15.5.4.9")]
        public void TestsThatStringPrototypeLocalecompareReturns0WhenComparingStringsThatAreConsideredCanonicallyEquivalentByTheUnicodeStandard()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.9/15.5.4.9_CE.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.9")]
        public void TheStringPrototypeLocalecompareLengthPropertyHasTheAttributeReadonly()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.9/S15.5.4.9_A10.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.9")]
        public void TheLengthPropertyOfTheLocalecompareMethodIs1()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.9/S15.5.4.9_A11.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.9")]
        public void StringPrototypeLocalecompareThat()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.9/S15.5.4.9_A1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.9")]
        public void StringPrototypeLocalecompareThat2()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.9/S15.5.4.9_A1_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.9")]
        public void StringPrototypeLocalecompareHasNotPrototypeProperty()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.9/S15.5.4.9_A6.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.9")]
        public void StringPrototypeLocalecompareCanTBeUsedAsConstructor()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.9/S15.5.4.9_A7.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.9")]
        public void TheStringPrototypeLocalecompareLengthPropertyHasTheAttributeDontenum()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.9/S15.5.4.9_A8.js", false);
        }

        [Fact]
        [Trait("Category", "15.5.4.9")]
        public void TheStringPrototypeLocalecompareLengthPropertyHasTheAttributeDontdelete()
        {
			RunTest(@"TestCases/ch15/15.5/15.5.4/15.5.4.9/S15.5.4.9_A9.js", false);
        }


    }
}
