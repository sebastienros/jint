using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_4_4_7 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.4.4.7")]
        public void TheArgumentsAreAppendedToTheEndOfTheArrayInTheOrderInWhichTheyAppearTheNewLengthOfTheArrayIsReturnedAsTheResultOfTheCall()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.7/S15.4.4.7_A1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.7")]
        public void TheArgumentsAreAppendedToTheEndOfTheArrayInTheOrderInWhichTheyAppearTheNewLengthOfTheArrayIsReturnedAsTheResultOfTheCall2()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.7/S15.4.4.7_A1_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.7")]
        public void ThePushFunctionIsIntentionallyGenericItDoesNotRequireThatItsThisValueBeAnArrayObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.7/S15.4.4.7_A2_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.7")]
        public void ThePushFunctionIsIntentionallyGenericItDoesNotRequireThatItsThisValueBeAnArrayObject2()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.7/S15.4.4.7_A2_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.7")]
        public void ThePushFunctionIsIntentionallyGenericItDoesNotRequireThatItsThisValueBeAnArrayObject3()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.7/S15.4.4.7_A2_T3.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.7")]
        public void CheckTouint32LengthForArrayObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.7/S15.4.4.7_A3.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.7")]
        public void CheckTouint32LengthForNonArrayObjects()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.7/S15.4.4.7_A4_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.7")]
        public void CheckTouint32LengthForNonArrayObjects2()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.7/S15.4.4.7_A4_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.7")]
        public void CheckTouint32LengthForNonArrayObjects3()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.7/S15.4.4.7_A4_T3.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.7")]
        public void GetFromNotAnInheritedProperty()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.7/S15.4.4.7_A5_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.7")]
        public void TheLengthPropertyOfPushHasTheAttributeDontenum()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.7/S15.4.4.7_A6.1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.7")]
        public void TheLengthPropertyOfPushHasTheAttributeDontdelete()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.7/S15.4.4.7_A6.2.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.7")]
        public void TheLengthPropertyOfPushHasTheAttributeReadonly()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.7/S15.4.4.7_A6.3.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.7")]
        public void TheLengthPropertyOfPushIs1()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.7/S15.4.4.7_A6.4.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.7")]
        public void ThePushPropertyOfArrayHasTheAttributeDontenum()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.7/S15.4.4.7_A6.5.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.7")]
        public void ThePushPropertyOfArrayHasNotPrototypeProperty()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.7/S15.4.4.7_A6.6.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.7")]
        public void ThePushPropertyOfArrayCanTBeUsedAsConstructor()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.7/S15.4.4.7_A6.7.js", false);
        }


    }
}
