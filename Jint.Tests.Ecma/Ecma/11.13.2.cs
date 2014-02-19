using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_11_13_2 : EcmaTest
    {
        [Fact]
        [Trait("Category", "11.13.2")]
        public void StrictModeReferenceerrorIsThrownIfTheLefthandsideexpressionOfACompoundAssignmentOperatorEvaluatesToAnUnresolvableReference()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/11.13.2-1-s.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void StrictModeReferenceerrorIsThrownIfTheLefthandsideexpressionOfACompoundAssignmentOperatorEvaluatesToAnUnresolvableReference2()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/11.13.2-10-s.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void StrictModeReferenceerrorIsThrownIfTheLefthandsideexpressionOfACompoundAssignmentOperatorEvaluatesToAnUnresolvableReference3()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/11.13.2-11-s.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void StrictModeReferenceerrorIsnTThrownIfTheLefthandsideexpressionOfACompoundAssignmentOperatorEvaluatesToAResolvableReference()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/11.13.2-12-s.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void StrictModeReferenceerrorIsnTThrownIfTheLefthandsideexpressionOfACompoundAssignmentOperatorEvaluatesToAResolvableReference2()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/11.13.2-13-s.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void StrictModeReferenceerrorIsnTThrownIfTheLefthandsideexpressionOfACompoundAssignmentOperatorEvaluatesToAResolvableReference3()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/11.13.2-14-s.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void StrictModeReferenceerrorIsnTThrownIfTheLefthandsideexpressionOfACompoundAssignmentOperatorEvaluatesToAResolvableReference4()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/11.13.2-15-s.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void StrictModeReferenceerrorIsnTThrownIfTheLefthandsideexpressionOfACompoundAssignmentOperatorEvaluatesToAResolvableReference5()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/11.13.2-16-s.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void StrictModeReferenceerrorIsnTThrownIfTheLefthandsideexpressionOfACompoundAssignmentOperatorEvaluatesToAResolvableReference6()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/11.13.2-17-s.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void StrictModeReferenceerrorIsnTThrownIfTheLefthandsideexpressionOfACompoundAssignmentOperatorEvaluatesToAResolvableReference7()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/11.13.2-18-s.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void StrictModeReferenceerrorIsnTThrownIfTheLefthandsideexpressionOfACompoundAssignmentOperatorEvaluatesToAResolvableReference8()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/11.13.2-19-s.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void StrictModeReferenceerrorIsThrownIfTheLefthandsideexpressionOfACompoundAssignmentOperatorEvaluatesToAnUnresolvableReference4()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/11.13.2-2-s.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void StrictModeReferenceerrorIsnTThrownIfTheLefthandsideexpressionOfACompoundAssignmentOperatorEvaluatesToAResolvableReference9()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/11.13.2-20-s.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void StrictModeReferenceerrorIsnTThrownIfTheLefthandsideexpressionOfACompoundAssignmentOperatorEvaluatesToAResolvableReference10()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/11.13.2-21-s.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void StrictModeReferenceerrorIsnTThrownIfTheLefthandsideexpressionOfACompoundAssignmentOperatorEvaluatesToAResolvableReference11()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/11.13.2-22-s.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void StrictModeTypeerrorIsThrownIfTheLefthandsideOfACompoundAssignmentOperatorIsAReferenceToADataPropertyWithTheAttributeValueWritableFalse()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/11.13.2-23-s.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void StrictModeTypeerrorIsThrownIfTheLefthandsideOfACompoundAssignmentOperatorIsAReferenceToADataPropertyWithTheAttributeValueWritableFalse2()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/11.13.2-24-s.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void StrictModeTypeerrorIsThrownIfTheLefthandsideOfACompoundAssignmentOperatorIsAReferenceToADataPropertyWithTheAttributeValueWritableFalse3()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/11.13.2-25-s.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void StrictModeTypeerrorIsThrownIfTheLefthandsideOfACompoundAssignmentOperatorIsAReferenceToADataPropertyWithTheAttributeValueWritableFalse4()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/11.13.2-26-s.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void StrictModeTypeerrorIsThrownIfTheLefthandsideOfACompoundAssignmentOperatorIsAReferenceToADataPropertyWithTheAttributeValueWritableFalse5()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/11.13.2-27-s.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void StrictModeTypeerrorIsThrownIfTheLefthandsideOfACompoundAssignmentOperatorIsAReferenceToADataPropertyWithTheAttributeValueWritableFalse6()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/11.13.2-28-s.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void StrictModeTypeerrorIsThrownIfTheLefthandsideOfACompoundAssignmentOperatorIsAReferenceToADataPropertyWithTheAttributeValueWritableFalse7()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/11.13.2-29-s.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void StrictModeReferenceerrorIsThrownIfTheLefthandsideexpressionOfACompoundAssignmentOperatorEvaluatesToAnUnresolvableReference5()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/11.13.2-3-s.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void StrictModeTypeerrorIsThrownIfTheLefthandsideOfACompoundAssignmentOperatorIsAReferenceToADataPropertyWithTheAttributeValueWritableFalse8()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/11.13.2-30-s.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void StrictModeTypeerrorIsThrownIfTheLefthandsideOfACompoundAssignmentOperatorIsAReferenceToADataPropertyWithTheAttributeValueWritableFalse9()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/11.13.2-31-s.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void StrictModeTypeerrorIsThrownIfTheLefthandsideOfACompoundAssignmentOperatorIsAReferenceToADataPropertyWithTheAttributeValueWritableFalse10()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/11.13.2-32-s.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void StrictModeTypeerrorIsThrownIfTheLefthandsideOfACompoundAssignmentOperatorIsAReferenceToADataPropertyWithTheAttributeValueWritableFalse11()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/11.13.2-33-s.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void StrictModeTypeerrorIsThrownIfTheLefthandsideOfACompoundAssignmentOperatorIsAReferenceToAnAccessorPropertyWithTheAttributeValueSetUndefined()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/11.13.2-34-s.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void StrictModeTypeerrorIsThrownIfTheLefthandsideOfACompoundAssignmentOperatorIsAReferenceToAnAccessorPropertyWithTheAttributeValueSetUndefined2()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/11.13.2-35-s.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void StrictModeTypeerrorIsThrownIfTheLefthandsideOfACompoundAssignmentOperatorIsAReferenceToAnAccessorPropertyWithTheAttributeValueSetUndefined3()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/11.13.2-36-s.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void StrictModeTypeerrorIsThrownIfTheLefthandsideOfACompoundAssignmentOperatorIsAReferenceToAnAccessorPropertyWithTheAttributeValueSetUndefined4()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/11.13.2-37-s.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void StrictModeTypeerrorIsThrownIfTheLefthandsideOfACompoundAssignmentOperatorIsAReferenceToAnAccessorPropertyWithTheAttributeValueSetUndefined5()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/11.13.2-38-s.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void StrictModeTypeerrorIsThrownIfTheLefthandsideOfACompoundAssignmentOperatorIsAReferenceToAnAccessorPropertyWithTheAttributeValueSetUndefined6()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/11.13.2-39-s.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void StrictModeReferenceerrorIsThrownIfTheLefthandsideexpressionOfACompoundAssignmentOperatorEvaluatesToAnUnresolvableReference6()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/11.13.2-4-s.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void StrictModeTypeerrorIsThrownIfTheLefthandsideOfACompoundAssignmentOperatorIsAReferenceToAnAccessorPropertyWithTheAttributeValueSetUndefined7()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/11.13.2-40-s.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void StrictModeTypeerrorIsThrownIfTheLefthandsideOfACompoundAssignmentOperatorIsAReferenceToAnAccessorPropertyWithTheAttributeValueSetUndefined8()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/11.13.2-41-s.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void StrictModeTypeerrorIsThrownIfTheLefthandsideOfACompoundAssignmentOperatorIsAReferenceToAnAccessorPropertyWithTheAttributeValueSetUndefined9()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/11.13.2-42-s.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void StrictModeTypeerrorIsThrownIfTheLefthandsideOfACompoundAssignmentOperatorIsAReferenceToAnAccessorPropertyWithTheAttributeValueSetUndefined10()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/11.13.2-43-s.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void StrictModeTypeerrorIsThrownIfTheLefthandsideOfACompoundAssignmentOperatorIsAReferenceOfToAnAccessorPropertyWithTheAttributeValueSetUndefined()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/11.13.2-44-s.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void StrictModeTypeerrorIsThrownIfTheLefthandsideOfACompoundAssignmentOperatorIsAReferenceToANonExistentPropertyOfAnObjectWhoseExtensibleInternalPropertyIfFalse()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/11.13.2-45-s.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void StrictModeTypeerrorIsThrownIfTheLefthandsideOfACompoundAssignmentOperatorIsAReferenceToANonExistentPropertyOfAnObjectWhoseExtensibleInternalPropertyIfFalse2()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/11.13.2-46-s.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void StrictModeTypeerrorIsThrownIfTheLefthandsideOfACompoundAssignmentOperatorIsAReferenceToANonExistentPropertyOfAnObjectWhoseExtensibleInternalPropertyIfFalse3()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/11.13.2-47-s.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void StrictModeTypeerrorIsThrownIfTheLefthandsideOfACompoundAssignmentOperatorIsAReferenceToANonExistentPropertyOfAnObjectWhoseExtensibleInternalPropertyIfFalse4()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/11.13.2-48-s.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void StrictModeTypeerrorIsThrownIfTheLefthandsideOfACompoundAssignmentOperatorIsAReferenceToANonExistentPropertyOfAnObjectWhoseExtensibleInternalPropertyIfFalse5()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/11.13.2-49-s.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void StrictModeReferenceerrorIsThrownIfTheLefthandsideexpressionOfACompoundAssignmentOperatorEvaluatesToAnUnresolvableReference7()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/11.13.2-5-s.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void StrictModeTypeerrorIsThrownIfTheLefthandsideOfACompoundAssignmentOperatorIsAReferenceToANonExistentPropertyOfAnObjectWhoseExtensibleInternalPropertyIfFalse6()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/11.13.2-50-s.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void StrictModeTypeerrorIsThrownIfTheLefthandsideOfACompoundAssignmentOperatorIsAReferenceToANonExistentPropertyOfAnObjectWhoseExtensibleInternalPropertyIfFalse7()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/11.13.2-51-s.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void StrictModeTypeerrorIsThrownIfTheLefthandsideOfACompoundAssignmentOperatorIsAReferenceToANonExistentPropertyOfAnObjectWhoseExtensibleInternalPropertyIfFalse8()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/11.13.2-52-s.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void StrictModeTypeerrorIsThrownIfTheLefthandsideOfACompoundAssignmentOperatorIsAReferenceToANonExistentPropertyOfAnObjectWhoseExtensibleInternalPropertyIfFalse9()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/11.13.2-53-s.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void StrictModeTypeerrorIsThrownIfTheLefthandsideOfACompoundAssignmentOperatorIsAReferenceToANonExistentPropertyOfAnObjectWhoseExtensibleInternalPropertyIfFalse10()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/11.13.2-54-s.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void StrictModeTypeerrorIsThrownIfTheLefthandsideOfACompoundAssignmentOperatorIsAReferenceToANonExistentPropertyOfAnObjectWhoseExtensibleInternalPropertyIfFalse11()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/11.13.2-55-s.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void StrictModeSyntaxerrorIsThrownIfTheIdentifierEvalAppearAsTheLefthandsideexpressionOfACompoundAssignmentOperator()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/11.13.2-6-1-s.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void StrictModeSyntaxerrorIsThrownIfTheIdentifierEvalAppearAsTheLefthandsideexpressionOfACompoundAssignmentOperator2()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/11.13.2-6-10-s.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void StrictModeSyntaxerrorIsThrownIfTheIdentifierEvalAppearAsTheLefthandsideexpressionOfACompoundAssignmentOperator3()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/11.13.2-6-11-s.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void StrictModeSyntaxerrorIsThrownIfTheIdentifierArgumentsAppearAsTheLefthandsideexpressionOfACompoundAssignmentOperator()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/11.13.2-6-12-s.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void StrictModeSyntaxerrorIsThrownIfTheIdentifierArgumentsAppearAsTheLefthandsideexpressionOfACompoundAssignmentOperator2()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/11.13.2-6-13-s.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void StrictModeSyntaxerrorIsThrownIfTheIdentifierArgumentsAppearAsTheLefthandsideexpressionOfACompoundAssignmentOperator3()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/11.13.2-6-14-s.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void StrictModeSyntaxerrorIsThrownIfTheIdentifierArgumentsAppearAsTheLefthandsideexpressionOfACompoundAssignmentOperator4()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/11.13.2-6-15-s.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void StrictModeSyntaxerrorIsThrownIfTheIdentifierArgumentsAppearAsTheLefthandsideexpressionOfACompoundAssignmentOperator5()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/11.13.2-6-16-s.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void StrictModeSyntaxerrorIsThrownIfTheIdentifierArgumentsAppearAsTheLefthandsideexpressionOfACompoundAssignmentOperator6()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/11.13.2-6-17-s.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void StrictModeSyntaxerrorIsThrownIfTheIdentifierArgumentsAppearAsTheLefthandsideexpressionOfACompoundAssignmentOperator7()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/11.13.2-6-18-s.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void StrictModeSyntaxerrorIsThrownIfTheIdentifierArgumentsAppearAsTheLefthandsideexpressionOfACompoundAssignmentOperator8()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/11.13.2-6-19-s.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void StrictModeSyntaxerrorIsThrowIfTheIdentifierEvalAppearsAsTheLefthandsideexpressionOfACompoundAssignmentOperator()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/11.13.2-6-1gs.js", true);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void StrictModeSyntaxerrorIsThrownIfTheIdentifierEvalAppearAsTheLefthandsideexpressionOfACompoundAssignmentOperator4()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/11.13.2-6-2-s.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void StrictModeSyntaxerrorIsThrownIfTheIdentifierArgumentsAppearAsTheLefthandsideexpressionOfACompoundAssignmentOperator9()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/11.13.2-6-20-s.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void StrictModeSyntaxerrorIsThrownIfTheIdentifierArgumentsAppearAsTheLefthandsideexpressionOfACompoundAssignmentOperator10()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/11.13.2-6-21-s.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void StrictModeSyntaxerrorIsThrownIfTheIdentifierArgumentsAppearAsTheLefthandsideexpressionOfACompoundAssignmentOperator11()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/11.13.2-6-22-s.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void StrictModeSyntaxerrorIsThrownIfTheIdentifierEvalAppearAsTheLefthandsideexpressionOfACompoundAssignmentOperator5()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/11.13.2-6-3-s.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void StrictModeSyntaxerrorIsThrownIfTheIdentifierEvalAppearAsTheLefthandsideexpressionOfACompoundAssignmentOperator6()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/11.13.2-6-4-s.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void StrictModeSyntaxerrorIsThrownIfTheIdentifierEvalAppearAsTheLefthandsideexpressionOfACompoundAssignmentOperator7()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/11.13.2-6-5-s.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void StrictModeSyntaxerrorIsThrownIfTheIdentifierEvalAppearAsTheLefthandsideexpressionOfACompoundAssignmentOperator8()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/11.13.2-6-6-s.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void StrictModeSyntaxerrorIsThrownIfTheIdentifierEvalAppearAsTheLefthandsideexpressionOfACompoundAssignmentOperator9()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/11.13.2-6-7-s.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void StrictModeSyntaxerrorIsThrownIfTheIdentifierEvalAppearAsTheLefthandsideexpressionOfACompoundAssignmentOperator10()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/11.13.2-6-8-s.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void StrictModeSyntaxerrorIsThrownIfTheIdentifierEvalAppearAsTheLefthandsideexpressionOfACompoundAssignmentOperator11()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/11.13.2-6-9-s.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void StrictModeReferenceerrorIsThrownIfTheLefthandsideexpressionOfACompoundAssignmentOperatorEvaluatesToAnUnresolvableReference8()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/11.13.2-6-s.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void StrictModeReferenceerrorIsThrownIfTheLefthandsideexpressionOfACompoundAssignmentOperatorEvaluatesToAnUnresolvableReference9()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/11.13.2-7-s.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void StrictModeReferenceerrorIsThrownIfTheLefthandsideexpressionOfACompoundAssignmentOperatorEvaluatesToAnUnresolvableReference10()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/11.13.2-8-s.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void StrictModeReferenceerrorIsThrownIfTheLefthandsideexpressionOfACompoundAssignmentOperatorEvaluatesToAnUnresolvableReference11()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/11.13.2-9-s.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void WhiteSpaceAndLineTerminatorBetweenLefthandsideexpressionAndOrBetweenAndAssignmentexpressionAreAllowed()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/S11.13.2_A1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void WhiteSpaceAndLineTerminatorBetweenLefthandsideexpressionAndOrBetweenAndAssignmentexpressionAreAllowed2()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/S11.13.2_A1_T10.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void WhiteSpaceAndLineTerminatorBetweenLefthandsideexpressionAndOrBetweenAndAssignmentexpressionAreAllowed3()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/S11.13.2_A1_T11.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void WhiteSpaceAndLineTerminatorBetweenLefthandsideexpressionAndOrBetweenAndAssignmentexpressionAreAllowed4()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/S11.13.2_A1_T2.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void WhiteSpaceAndLineTerminatorBetweenLefthandsideexpressionAndOrBetweenAndAssignmentexpressionAreAllowed5()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/S11.13.2_A1_T3.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void WhiteSpaceAndLineTerminatorBetweenLefthandsideexpressionAndOrBetweenAndAssignmentexpressionAreAllowed6()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/S11.13.2_A1_T4.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void WhiteSpaceAndLineTerminatorBetweenLefthandsideexpressionAndOrBetweenAndAssignmentexpressionAreAllowed7()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/S11.13.2_A1_T5.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void WhiteSpaceAndLineTerminatorBetweenLefthandsideexpressionAndOrBetweenAndAssignmentexpressionAreAllowed8()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/S11.13.2_A1_T6.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void WhiteSpaceAndLineTerminatorBetweenLefthandsideexpressionAndOrBetweenAndAssignmentexpressionAreAllowed9()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/S11.13.2_A1_T7.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void WhiteSpaceAndLineTerminatorBetweenLefthandsideexpressionAndOrBetweenAndAssignmentexpressionAreAllowed10()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/S11.13.2_A1_T8.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void WhiteSpaceAndLineTerminatorBetweenLefthandsideexpressionAndOrBetweenAndAssignmentexpressionAreAllowed11()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/S11.13.2_A1_T9.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void OperatorUsesGetvalue()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/S11.13.2_A2.1_T1.1.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void OperatorUsesGetvalue2()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/S11.13.2_A2.1_T1.10.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void OperatorUsesGetvalue3()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/S11.13.2_A2.1_T1.11.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void OperatorUsesGetvalue4()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/S11.13.2_A2.1_T1.2.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void OperatorUsesGetvalue5()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/S11.13.2_A2.1_T1.3.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void OperatorUsesGetvalue6()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/S11.13.2_A2.1_T1.4.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void OperatorUsesGetvalue7()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/S11.13.2_A2.1_T1.5.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void OperatorUsesGetvalue8()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/S11.13.2_A2.1_T1.6.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void OperatorUsesGetvalue9()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/S11.13.2_A2.1_T1.7.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void OperatorUsesGetvalue10()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/S11.13.2_A2.1_T1.8.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void OperatorUsesGetvalue11()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/S11.13.2_A2.1_T1.9.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void OperatorUsesGetvalue12()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/S11.13.2_A2.1_T2.1.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void OperatorUsesGetvalue13()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/S11.13.2_A2.1_T2.10.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void OperatorUsesGetvalue14()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/S11.13.2_A2.1_T2.11.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void OperatorUsesGetvalue15()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/S11.13.2_A2.1_T2.2.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void OperatorUsesGetvalue16()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/S11.13.2_A2.1_T2.3.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void OperatorUsesGetvalue17()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/S11.13.2_A2.1_T2.4.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void OperatorUsesGetvalue18()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/S11.13.2_A2.1_T2.5.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void OperatorUsesGetvalue19()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/S11.13.2_A2.1_T2.6.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void OperatorUsesGetvalue20()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/S11.13.2_A2.1_T2.7.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void OperatorUsesGetvalue21()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/S11.13.2_A2.1_T2.8.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void OperatorUsesGetvalue22()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/S11.13.2_A2.1_T2.9.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void OperatorUsesGetvalue23()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/S11.13.2_A2.1_T3.1.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void OperatorUsesGetvalue24()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/S11.13.2_A2.1_T3.10.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void OperatorUsesGetvalue25()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/S11.13.2_A2.1_T3.11.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void OperatorUsesGetvalue26()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/S11.13.2_A2.1_T3.2.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void OperatorUsesGetvalue27()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/S11.13.2_A2.1_T3.3.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void OperatorUsesGetvalue28()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/S11.13.2_A2.1_T3.4.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void OperatorUsesGetvalue29()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/S11.13.2_A2.1_T3.5.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void OperatorUsesGetvalue30()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/S11.13.2_A2.1_T3.6.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void OperatorUsesGetvalue31()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/S11.13.2_A2.1_T3.7.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void OperatorUsesGetvalue32()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/S11.13.2_A2.1_T3.8.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void OperatorUsesGetvalue33()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/S11.13.2_A2.1_T3.9.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void OperatorUsesPutvalue()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/S11.13.2_A2.2_T1.js", true);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void OperatorUsesPutvalue2()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/S11.13.2_A2.2_T10.js", true);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void OperatorUsesPutvalue3()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/S11.13.2_A2.2_T11.js", true);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void OperatorUsesPutvalue4()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/S11.13.2_A2.2_T2.js", true);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void OperatorUsesPutvalue5()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/S11.13.2_A2.2_T3.js", true);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void OperatorUsesPutvalue6()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/S11.13.2_A2.2_T4.js", true);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void OperatorUsesPutvalue7()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/S11.13.2_A2.2_T5.js", true);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void OperatorUsesPutvalue8()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/S11.13.2_A2.2_T6.js", true);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void OperatorUsesPutvalue9()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/S11.13.2_A2.2_T7.js", true);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void OperatorUsesPutvalue10()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/S11.13.2_A2.2_T8.js", true);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void OperatorUsesPutvalue11()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/S11.13.2_A2.2_T9.js", true);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void OperatorXYUsesPutvalueXXY()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/S11.13.2_A3.1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void OperatorXYUsesPutvalueXXY2()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/S11.13.2_A3.1_T10.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void OperatorXYUsesPutvalueXXY3()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/S11.13.2_A3.1_T11.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void OperatorXYUsesPutvalueXXY4()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/S11.13.2_A3.1_T2.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void OperatorXYUsesPutvalueXXY5()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/S11.13.2_A3.1_T3.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void OperatorXYUsesPutvalueXXY6()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/S11.13.2_A3.1_T4.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void OperatorXYUsesPutvalueXXY7()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/S11.13.2_A3.1_T5.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void OperatorXYUsesPutvalueXXY8()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/S11.13.2_A3.1_T6.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void OperatorXYUsesPutvalueXXY9()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/S11.13.2_A3.1_T7.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void OperatorXYUsesPutvalueXXY10()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/S11.13.2_A3.1_T8.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void OperatorXYUsesPutvalueXXY11()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/S11.13.2_A3.1_T9.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void OperatorXYReturnsXY()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/S11.13.2_A3.2_T1.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void OperatorXYReturnsXY2()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/S11.13.2_A3.2_T10.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void OperatorXYReturnsXY3()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/S11.13.2_A3.2_T11.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void OperatorXYReturnsXY4()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/S11.13.2_A3.2_T2.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void OperatorXYReturnsXY5()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/S11.13.2_A3.2_T3.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void OperatorXYReturnsXY6()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/S11.13.2_A3.2_T4.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void OperatorXYReturnsXY7()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/S11.13.2_A3.2_T5.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void OperatorXYReturnsXY8()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/S11.13.2_A3.2_T6.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void OperatorXYReturnsXY9()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/S11.13.2_A3.2_T7.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void OperatorXYReturnsXY10()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/S11.13.2_A3.2_T8.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void OperatorXYReturnsXY11()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/S11.13.2_A3.2_T9.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void TheProductionXYIsTheSameAsXXY()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/S11.13.2_A4.10_T1.1.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void TheProductionXYIsTheSameAsXXY2()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/S11.13.2_A4.10_T1.2.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void TheProductionXYIsTheSameAsXXY3()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/S11.13.2_A4.10_T1.3.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void TheProductionXYIsTheSameAsXXY4()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/S11.13.2_A4.10_T1.4.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void TheProductionXYIsTheSameAsXXY5()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/S11.13.2_A4.10_T2.1.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void TheProductionXYIsTheSameAsXXY6()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/S11.13.2_A4.10_T2.2.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void TheProductionXYIsTheSameAsXXY7()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/S11.13.2_A4.10_T2.3.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void TheProductionXYIsTheSameAsXXY8()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/S11.13.2_A4.10_T2.4.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void TheProductionXYIsTheSameAsXXY9()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/S11.13.2_A4.10_T2.5.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void TheProductionXYIsTheSameAsXXY10()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/S11.13.2_A4.10_T2.6.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void TheProductionXYIsTheSameAsXXY11()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/S11.13.2_A4.10_T2.7.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void TheProductionXYIsTheSameAsXXY12()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/S11.13.2_A4.10_T2.8.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void TheProductionXYIsTheSameAsXXY13()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/S11.13.2_A4.10_T2.9.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void TheProductionXYIsTheSameAsXXY14()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/S11.13.2_A4.11_T1.1.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void TheProductionXYIsTheSameAsXXY15()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/S11.13.2_A4.11_T1.2.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void TheProductionXYIsTheSameAsXXY16()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/S11.13.2_A4.11_T1.3.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void TheProductionXYIsTheSameAsXXY17()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/S11.13.2_A4.11_T1.4.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void TheProductionXYIsTheSameAsXXY18()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/S11.13.2_A4.11_T2.1.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void TheProductionXYIsTheSameAsXXY19()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/S11.13.2_A4.11_T2.2.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void TheProductionXYIsTheSameAsXXY20()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/S11.13.2_A4.11_T2.3.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void TheProductionXYIsTheSameAsXXY21()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/S11.13.2_A4.11_T2.4.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void TheProductionXYIsTheSameAsXXY22()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/S11.13.2_A4.11_T2.5.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void TheProductionXYIsTheSameAsXXY23()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/S11.13.2_A4.11_T2.6.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void TheProductionXYIsTheSameAsXXY24()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/S11.13.2_A4.11_T2.7.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void TheProductionXYIsTheSameAsXXY25()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/S11.13.2_A4.11_T2.8.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void TheProductionXYIsTheSameAsXXY26()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/S11.13.2_A4.11_T2.9.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void TheProductionXYIsTheSameAsTheProductionXXY()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/S11.13.2_A4.1_T1.1.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void TheProductionXYIsTheSameAsTheProductionXXY2()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/S11.13.2_A4.1_T1.2.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void TheProductionXYIsTheSameAsTheProductionXXY3()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/S11.13.2_A4.1_T1.3.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void TheProductionXYIsTheSameAsTheProductionXXY4()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/S11.13.2_A4.1_T1.4.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void TheProductionXYIsTheSameAsTheProductionXXY5()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/S11.13.2_A4.1_T2.1.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void TheProductionXYIsTheSameAsTheProductionXXY6()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/S11.13.2_A4.1_T2.2.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void TheProductionXYIsTheSameAsTheProductionXXY7()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/S11.13.2_A4.1_T2.3.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void TheProductionXYIsTheSameAsTheProductionXXY8()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/S11.13.2_A4.1_T2.4.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void TheProductionXYIsTheSameAsTheProductionXXY9()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/S11.13.2_A4.1_T2.5.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void TheProductionXYIsTheSameAsTheProductionXXY10()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/S11.13.2_A4.1_T2.6.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void TheProductionXYIsTheSameAsTheProductionXXY11()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/S11.13.2_A4.1_T2.7.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void TheProductionXYIsTheSameAsTheProductionXXY12()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/S11.13.2_A4.1_T2.8.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void TheProductionXYIsTheSameAsTheProductionXXY13()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/S11.13.2_A4.1_T2.9.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void TheProductionXYIsTheSameAsXXY27()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/S11.13.2_A4.2_T1.1.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void TheProductionXYIsTheSameAsXXY28()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/S11.13.2_A4.2_T1.2.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void TheProductionXYIsTheSameAsXXY29()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/S11.13.2_A4.2_T1.3.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void TheProductionXYIsTheSameAsXXY30()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/S11.13.2_A4.2_T1.4.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void TheProductionXYIsTheSameAsXXY31()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/S11.13.2_A4.2_T2.1.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void TheProductionXYIsTheSameAsXXY32()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/S11.13.2_A4.2_T2.2.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void TheProductionXYIsTheSameAsXXY33()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/S11.13.2_A4.2_T2.3.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void TheProductionXYIsTheSameAsXXY34()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/S11.13.2_A4.2_T2.4.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void TheProductionXYIsTheSameAsXXY35()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/S11.13.2_A4.2_T2.5.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void TheProductionXYIsTheSameAsXXY36()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/S11.13.2_A4.2_T2.6.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void TheProductionXYIsTheSameAsXXY37()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/S11.13.2_A4.2_T2.7.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void TheProductionXYIsTheSameAsXXY38()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/S11.13.2_A4.2_T2.8.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void TheProductionXYIsTheSameAsXXY39()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/S11.13.2_A4.2_T2.9.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void TheProductionXYIsTheSameAsXXY40()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/S11.13.2_A4.3_T1.1.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void TheProductionXYIsTheSameAsXXY41()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/S11.13.2_A4.3_T1.2.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void TheProductionXYIsTheSameAsXXY42()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/S11.13.2_A4.3_T1.3.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void TheProductionXYIsTheSameAsXXY43()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/S11.13.2_A4.3_T1.4.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void TheProductionXYIsTheSameAsXXY44()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/S11.13.2_A4.3_T2.1.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void TheProductionXYIsTheSameAsXXY45()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/S11.13.2_A4.3_T2.2.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void TheProductionXYIsTheSameAsXXY46()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/S11.13.2_A4.3_T2.3.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void TheProductionXYIsTheSameAsXXY47()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/S11.13.2_A4.3_T2.4.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void TheProductionXYIsTheSameAsXXY48()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/S11.13.2_A4.3_T2.5.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void TheProductionXYIsTheSameAsXXY49()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/S11.13.2_A4.3_T2.6.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void TheProductionXYIsTheSameAsXXY50()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/S11.13.2_A4.3_T2.7.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void TheProductionXYIsTheSameAsXXY51()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/S11.13.2_A4.3_T2.8.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void TheProductionXYIsTheSameAsXXY52()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/S11.13.2_A4.3_T2.9.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void TheProductionXYIsTheSameAsXXY53()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/S11.13.2_A4.4_T1.1.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void TheProductionXYIsTheSameAsXXY54()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/S11.13.2_A4.4_T1.2.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void TheProductionXYIsTheSameAsXXY55()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/S11.13.2_A4.4_T1.3.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void TheProductionXYIsTheSameAsXXY56()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/S11.13.2_A4.4_T1.4.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void TheProductionXYIsTheSameAsXXY57()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/S11.13.2_A4.4_T2.1.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void TheProductionXYIsTheSameAsXXY58()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/S11.13.2_A4.4_T2.2.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void TheProductionXYIsTheSameAsXXY59()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/S11.13.2_A4.4_T2.3.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void TheProductionXYIsTheSameAsXXY60()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/S11.13.2_A4.4_T2.4.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void TheProductionXYIsTheSameAsXXY61()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/S11.13.2_A4.4_T2.5.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void TheProductionXYIsTheSameAsXXY62()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/S11.13.2_A4.4_T2.6.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void TheProductionXYIsTheSameAsXXY63()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/S11.13.2_A4.4_T2.7.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void TheProductionXYIsTheSameAsXXY64()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/S11.13.2_A4.4_T2.8.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void TheProductionXYIsTheSameAsXXY65()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/S11.13.2_A4.4_T2.9.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void TheProductionXYIsTheSameAsXXY66()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/S11.13.2_A4.5_T1.1.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void TheProductionXYIsTheSameAsXXY67()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/S11.13.2_A4.5_T1.2.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void TheProductionXYIsTheSameAsXXY68()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/S11.13.2_A4.5_T1.3.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void TheProductionXYIsTheSameAsXXY69()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/S11.13.2_A4.5_T1.4.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void TheProductionXYIsTheSameAsXXY70()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/S11.13.2_A4.5_T2.1.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void TheProductionXYIsTheSameAsXXY71()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/S11.13.2_A4.5_T2.2.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void TheProductionXYIsTheSameAsXXY72()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/S11.13.2_A4.5_T2.3.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void TheProductionXYIsTheSameAsXXY73()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/S11.13.2_A4.5_T2.4.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void TheProductionXYIsTheSameAsXXY74()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/S11.13.2_A4.5_T2.5.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void TheProductionXYIsTheSameAsXXY75()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/S11.13.2_A4.5_T2.6.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void TheProductionXYIsTheSameAsXXY76()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/S11.13.2_A4.5_T2.7.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void TheProductionXYIsTheSameAsXXY77()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/S11.13.2_A4.5_T2.8.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void TheProductionXYIsTheSameAsXXY78()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/S11.13.2_A4.5_T2.9.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void TheProductionXYIsTheSameAsXXY79()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/S11.13.2_A4.6_T1.1.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void TheProductionXYIsTheSameAsXXY80()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/S11.13.2_A4.6_T1.2.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void TheProductionXYIsTheSameAsXXY81()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/S11.13.2_A4.6_T1.3.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void TheProductionXYIsTheSameAsXXY82()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/S11.13.2_A4.6_T1.4.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void TheProductionXYIsTheSameAsXXY83()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/S11.13.2_A4.6_T2.1.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void TheProductionXYIsTheSameAsXXY84()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/S11.13.2_A4.6_T2.2.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void TheProductionXYIsTheSameAsXXY85()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/S11.13.2_A4.6_T2.3.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void TheProductionXYIsTheSameAsXXY86()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/S11.13.2_A4.6_T2.4.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void TheProductionXYIsTheSameAsXXY87()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/S11.13.2_A4.6_T2.5.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void TheProductionXYIsTheSameAsXXY88()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/S11.13.2_A4.6_T2.6.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void TheProductionXYIsTheSameAsXXY89()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/S11.13.2_A4.6_T2.7.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void TheProductionXYIsTheSameAsXXY90()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/S11.13.2_A4.6_T2.8.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void TheProductionXYIsTheSameAsXXY91()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/S11.13.2_A4.6_T2.9.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void TheProductionXYIsTheSameAsXXY92()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/S11.13.2_A4.7_T1.1.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void TheProductionXYIsTheSameAsXXY93()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/S11.13.2_A4.7_T1.2.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void TheProductionXYIsTheSameAsXXY94()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/S11.13.2_A4.7_T1.3.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void TheProductionXYIsTheSameAsXXY95()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/S11.13.2_A4.7_T1.4.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void TheProductionXYIsTheSameAsXXY96()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/S11.13.2_A4.7_T2.1.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void TheProductionXYIsTheSameAsXXY97()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/S11.13.2_A4.7_T2.2.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void TheProductionXYIsTheSameAsXXY98()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/S11.13.2_A4.7_T2.3.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void TheProductionXYIsTheSameAsXXY99()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/S11.13.2_A4.7_T2.4.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void TheProductionXYIsTheSameAsXXY100()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/S11.13.2_A4.7_T2.5.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void TheProductionXYIsTheSameAsXXY101()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/S11.13.2_A4.7_T2.6.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void TheProductionXYIsTheSameAsXXY102()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/S11.13.2_A4.7_T2.7.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void TheProductionXYIsTheSameAsXXY103()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/S11.13.2_A4.7_T2.8.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void TheProductionXYIsTheSameAsXXY104()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/S11.13.2_A4.7_T2.9.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void TheProductionXYIsTheSameAsXXY105()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/S11.13.2_A4.8_T1.1.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void TheProductionXYIsTheSameAsXXY106()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/S11.13.2_A4.8_T1.2.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void TheProductionXYIsTheSameAsXXY107()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/S11.13.2_A4.8_T1.3.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void TheProductionXYIsTheSameAsXXY108()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/S11.13.2_A4.8_T1.4.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void TheProductionXYIsTheSameAsXXY109()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/S11.13.2_A4.8_T2.1.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void TheProductionXYIsTheSameAsXXY110()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/S11.13.2_A4.8_T2.2.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void TheProductionXYIsTheSameAsXXY111()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/S11.13.2_A4.8_T2.3.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void TheProductionXYIsTheSameAsXXY112()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/S11.13.2_A4.8_T2.4.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void TheProductionXYIsTheSameAsXXY113()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/S11.13.2_A4.8_T2.5.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void TheProductionXYIsTheSameAsXXY114()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/S11.13.2_A4.8_T2.6.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void TheProductionXYIsTheSameAsXXY115()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/S11.13.2_A4.8_T2.7.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void TheProductionXYIsTheSameAsXXY116()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/S11.13.2_A4.8_T2.8.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void TheProductionXYIsTheSameAsXXY117()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/S11.13.2_A4.8_T2.9.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void TheProductionXYIsTheSameAsXXY118()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/S11.13.2_A4.9_T1.1.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void TheProductionXYIsTheSameAsXXY119()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/S11.13.2_A4.9_T1.2.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void TheProductionXYIsTheSameAsXXY120()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/S11.13.2_A4.9_T1.3.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void TheProductionXYIsTheSameAsXXY121()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/S11.13.2_A4.9_T1.4.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void TheProductionXYIsTheSameAsXXY122()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/S11.13.2_A4.9_T2.1.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void TheProductionXYIsTheSameAsXXY123()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/S11.13.2_A4.9_T2.2.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void TheProductionXYIsTheSameAsXXY124()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/S11.13.2_A4.9_T2.3.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void TheProductionXYIsTheSameAsXXY125()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/S11.13.2_A4.9_T2.4.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void TheProductionXYIsTheSameAsXXY126()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/S11.13.2_A4.9_T2.5.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void TheProductionXYIsTheSameAsXXY127()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/S11.13.2_A4.9_T2.6.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void TheProductionXYIsTheSameAsXXY128()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/S11.13.2_A4.9_T2.7.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void TheProductionXYIsTheSameAsXXY129()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/S11.13.2_A4.9_T2.8.js", false);
        }

        [Fact]
        [Trait("Category", "11.13.2")]
        public void TheProductionXYIsTheSameAsXXY130()
        {
			RunTest(@"TestCases/ch11/11.13/11.13.2/S11.13.2_A4.9_T2.9.js", false);
        }


    }
}
