using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_11_13_1 : EcmaTest
    {
        [Fact]
        [Trait("Category", "11.13.1")]
        public void PutvalueOperatesOnlyOnReferencesSeeStep1()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.1/11.13.1-1-1.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.1")]
        public void PutvalueOperatesOnlyOnReferencesSeeStep12()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.1/11.13.1-1-2.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.1")]
        public void PutvalueOperatesOnlyOnReferencesSeeStep13()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.1/11.13.1-1-3.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.1")]
        public void PutvalueOperatesOnlyOnReferencesSeeStep14()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.1/11.13.1-1-4.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.1")]
        public void PutvalueOperatesOnlyOnReferencesSeeStep3A()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.1/11.13.1-1-6-s.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.1")]
        public void StrictModeTypeerrorIsThrownIfTheLefthandsideIsAReferenceToADataPropertyWithTheAttributeValueWritableFalseUnderStrictMode()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.1/11.13.1-1-s.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.1")]
        public void StrictModeTypeerrorIsThrownIfTheLefthandsideIsAReferenceToAnAccessorPropertyWithTheAttributeValueSetUndefinedUnderStrictMode()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.1/11.13.1-2-s.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.1")]
        public void StrictModeTypeerrorIsThrownIfTheLefthandsideIsAReferenceToANonExistentPropertyOfAnObjectWhoseExtensibleInternalPropertyHasTheValueFalseUnderStrictMode()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.1/11.13.1-3-s.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.1")]
        public void PutvalueOperatesOnlyOnReferencesSeeStep3B()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.1/11.13.1-4-1.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.1")]
        public void SimpleAssignmentThrowsTypeerrorIfLefthandsideIsAReadonlyPropertyInStrictModeNumberMaxValue()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.1/11.13.1-4-14-s.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.1")]
        public void SimpleAssignmentThrowsTypeerrorIfLefthandsideIsAReadonlyPropertyInStrictModeGlobalUndefined()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.1/11.13.1-4-27-s.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.1")]
        public void StrictModeSyntaxerrorIsThrownIfTheIdentifierEvalAppearsAsTheLefthandsideexpressionOfSimpleAssignmentUnderStrictMode()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.1/11.13.1-4-28-s.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.1")]
        public void StrictModeSyntaxerrorIsThrownIfTheIdentifierMathPiAppearsAsTheLefthandsideexpressionOfSimpleAssignment()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.1/11.13.1-4-28gs.js", true);
        }

        [Fact]
        [Trait("Category", "11.13.1")]
        public void StrictModeSyntaxerrorIsThrownIfTheIdentifierArgumentsAppearsAsTheLefthandsideexpressionOfSimpleAssignmentUnderStrictMode()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.1/11.13.1-4-29-s.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.1")]
        public void StrictModeSyntaxerrorIsThrownIfTheIdentifierMathPiAppearsAsTheLefthandsideexpressionOfSimpleAssignment2()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.1/11.13.1-4-29gs.js", true);
        }

        [Fact]
        [Trait("Category", "11.13.1")]
        public void SimpleAssignmentThrowsTypeerrorIfLefthandsideIsAReadonlyPropertyInStrictModeGlobalInfinity()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.1/11.13.1-4-3-s.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.1")]
        public void StrictModeSyntaxerrorIsThrownIfTheIdentifierEvalAppearsAsTheLefthandsideexpressionPrimaryexpressionOfSimpleAssignmentUnderStrictMode()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.1/11.13.1-4-30-s.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.1")]
        public void StrictModeSyntaxerrorIsThrownIfTheIdentifierArgumentsAppearsAsTheLefthandsideexpressionPrimaryexpressionOfSimpleAssignmentUnderStrictMode()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.1/11.13.1-4-31-s.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.1")]
        public void SimpleAssignmentThrowsTypeerrorIfLefthandsideIsAReadonlyPropertyInStrictModeFunctionLength()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.1/11.13.1-4-6-s.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.1")]
        public void WhiteSpaceAndLineTerminatorBetweenLefthandsideexpressionAndOrBetweenAndAssignmentexpressionAreAllowed()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.1/S11.13.1_A1.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.1")]
        public void OperatorXYUsesGetvalueAndPutvalue()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.1/S11.13.1_A2.1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.1")]
        public void OperatorXYUsesGetvalueAndPutvalue2()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.1/S11.13.1_A2.1_T2.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.1")]
        public void OperatorXYUsesGetvalueAndPutvalue3()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.1/S11.13.1_A2.1_T3.js", true);
        }

        [Fact]
        [Trait("Category", "11.13.1")]
        public void OperatorXYPutvalueXY()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.1/S11.13.1_A3.1.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.1")]
        public void OperatorXYReturnsGetvalueY()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.1/S11.13.1_A3.2.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.1")]
        public void AssignmentexpressionLefthandsideexpressionAssignmentexpression()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.1/S11.13.1_A4_T1.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.1")]
        public void AssignmentexpressionLefthandsideexpressionAssignmentexpression2()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.1/S11.13.1_A4_T2.js", false);
        }


    }
}
