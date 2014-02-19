using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_8_7_2 : EcmaTest
    {
        [Fact]
        [Trait("Category", "8.7.2")]
        public void GetvalueVMastFail()
        {
			RunTest(@"TestCases/ch08/8.7/S8.7.2_A1_T1.js", true);
        }

        [Fact]
        [Trait("Category", "8.7.2")]
        public void GetvalueVMastFail2()
        {
			RunTest(@"TestCases/ch08/8.7/S8.7.2_A1_T2.js", true);
        }

        [Fact]
        [Trait("Category", "8.7.2")]
        public void XCallsGetvalueThenPutvalueSoAfterApplyingPostfixIncrementActuallyConreteOperatorTypeIsUnimportantWeMustHaveReferenceToDefinedValue()
        {
			RunTest(@"TestCases/ch08/8.7/S8.7.2_A2.js", false);
        }

        [Fact]
        [Trait("Category", "8.7.2")]
        public void ThisXCallsGetvalueThenPutvalueSoAfterApplyingPostfixIncrementActuallyConreteOperatorTypeIsUnimportanWeMustHaveReferenceToDefinedValue()
        {
			RunTest(@"TestCases/ch08/8.7/S8.7.2_A3.js", false);
        }

        [Fact]
        [Trait("Category", "8.7.2")]
        public void StrictModeReferenceerrorIsThrownIfLefthandsideEvaluatesToAnUnresolvableReference()
        {
			RunTest(@"TestCases/ch08/8.7/8.7.2/8.7.2-1-s.js", false);
        }

        [Fact]
        [Trait("Category", "8.7.2")]
        public void StrictModeReferenceerrorIsnTThrownIfLefthandsideEvaluatesToAResolvableReference()
        {
			RunTest(@"TestCases/ch08/8.7/8.7.2/8.7.2-2-s.js", false);
        }

        [Fact]
        [Trait("Category", "8.7.2")]
        public void EvalAPropertyNamedEvalIsPermitted()
        {
			RunTest(@"TestCases/ch08/8.7/8.7.2/8.7.2-3-1-s.js", false);
        }

        [Fact]
        [Trait("Category", "8.7.2")]
        public void StrictModeReferenceerrorIsThrownIfLefthandsideEvaluateToAnUnresolvableReference()
        {
			RunTest(@"TestCases/ch08/8.7/8.7.2/8.7.2-3-a-1gs.js", true);
        }

        [Fact]
        [Trait("Category", "8.7.2")]
        public void StrictModeRuntimeErrorIsThrownBeforeLefthandsideEvaluatesToAnUnresolvableReference()
        {
			RunTest(@"TestCases/ch08/8.7/8.7.2/8.7.2-3-a-2gs.js", true);
        }

        [Fact]
        [Trait("Category", "8.7.2")]
        public void StrictModeTypeerrorIsThrownIfLefthandsideIsAReferenceToANonWritableDataProperty()
        {
			RunTest(@"TestCases/ch08/8.7/8.7.2/8.7.2-3-s.js", false);
        }

        [Fact]
        [Trait("Category", "8.7.2")]
        public void StrictModeTypeerrorIsThrownIfLefthandsideIsAReferenceToAnAccessorPropertyWithNoSetter()
        {
			RunTest(@"TestCases/ch08/8.7/8.7.2/8.7.2-4-s.js", false);
        }

        [Fact]
        [Trait("Category", "8.7.2")]
        public void StrictModeTypeerrorIsThrownIfLefthandsideIsAReferenceToANonExistentPropertyOfAnNonExtensibleObject()
        {
			RunTest(@"TestCases/ch08/8.7/8.7.2/8.7.2-5-s.js", false);
        }

        [Fact]
        [Trait("Category", "8.7.2")]
        public void StrictModeTypeerrorIsnTThrownIfLefthandsideIsAReferenceToAWritableDataProperty()
        {
			RunTest(@"TestCases/ch08/8.7/8.7.2/8.7.2-6-s.js", false);
        }

        [Fact]
        [Trait("Category", "8.7.2")]
        public void StrictModeTypeerrorIsnTThrownIfLefthandsideIsAReferenceToAnAccessorPropertyWithSetter()
        {
			RunTest(@"TestCases/ch08/8.7/8.7.2/8.7.2-7-s.js", false);
        }

        [Fact]
        [Trait("Category", "8.7.2")]
        public void StrictModeTypeerrorIsnTThrownIfLefthandsideIsAReferenceToAPropertyOfAnExtensibleObject()
        {
			RunTest(@"TestCases/ch08/8.7/8.7.2/8.7.2-8-s.js", false);
        }


    }
}
