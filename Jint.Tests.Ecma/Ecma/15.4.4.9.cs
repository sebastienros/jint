using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_4_4_9 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.4.4.9")]
        public void IfLengthEqualZeroCallThePutMethodOfThisObjectWithArgumentsLengthAnd0AndReturnUndefined()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.9/S15.4.4.9_A1.1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.9")]
        public void TheFirstElementOfTheArrayIsRemovedFromTheArrayAndReturned()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.9/S15.4.4.9_A1.2_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.9")]
        public void TheShiftFunctionIsIntentionallyGenericItDoesNotRequireThatItsThisValueBeAnArrayObject()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.9/S15.4.4.9_A2_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.9")]
        public void TheShiftFunctionIsIntentionallyGenericItDoesNotRequireThatItsThisValueBeAnArrayObject2()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.9/S15.4.4.9_A2_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.9")]
        public void TheShiftFunctionIsIntentionallyGenericItDoesNotRequireThatItsThisValueBeAnArrayObject3()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.9/S15.4.4.9_A2_T3.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.9")]
        public void TheShiftFunctionIsIntentionallyGenericItDoesNotRequireThatItsThisValueBeAnArrayObject4()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.9/S15.4.4.9_A2_T4.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.9")]
        public void TheShiftFunctionIsIntentionallyGenericItDoesNotRequireThatItsThisValueBeAnArrayObject5()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.9/S15.4.4.9_A2_T5.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.9")]
        public void CheckTouint32LengthForNonArrayObjects()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.9/S15.4.4.9_A3_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.9")]
        public void CheckTouint32LengthForNonArrayObjects2()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.9/S15.4.4.9_A3_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.9")]
        public void CheckTouint32LengthForNonArrayObjects3()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.9/S15.4.4.9_A3_T3.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.9")]
        public void GetDeleteFromNotAnInheritedProperty()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.9/S15.4.4.9_A4_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.9")]
        public void GetDeleteFromNotAnInheritedProperty2()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.9/S15.4.4.9_A4_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.9")]
        public void TheLengthPropertyOfShiftHasTheAttributeDontenum()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.9/S15.4.4.9_A5.1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.9")]
        public void TheLengthPropertyOfShiftHasTheAttributeDontdelete()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.9/S15.4.4.9_A5.2.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.9")]
        public void TheLengthPropertyOfShiftHasTheAttributeReadonly()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.9/S15.4.4.9_A5.3.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.9")]
        public void TheLengthPropertyOfShiftIs0()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.9/S15.4.4.9_A5.4.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.9")]
        public void TheShiftPropertyOfArrayHasTheAttributeDontenum()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.9/S15.4.4.9_A5.5.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.9")]
        public void TheShiftPropertyOfArrayHasNotPrototypeProperty()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.9/S15.4.4.9_A5.6.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.4.9")]
        public void TheShiftPropertyOfArrayCanTBeUsedAsConstructor()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.4/15.4.4.9/S15.4.4.9_A5.7.js", false);
        }


    }
}
