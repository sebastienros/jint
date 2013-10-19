using Xunit;

namespace Jint.Tests.Ecma
{
    [Trait("Category","Pass")]
    public class Test_11_4_1 : EcmaTest
    {
        [Fact]
        [Trait("Category", "11.4.1")]
        public void ThisTestIsActuallyTestingTheDeleteInternalMethod8128SinceTheLanguageProvidesNoWayToDirectlyExerciseDeleteTheTestsArePlacedHere()
        {
			RunTest(@"TestCases/ch11/11.4/11.4.1/11.4.1-0-1.js", false);
        }

        [Fact]
        [Trait("Category", "11.4.1")]
        public void DeleteOperatorReturnsTrueWhenDeletingANonReferenceNumber()
        {
			RunTest(@"TestCases/ch11/11.4/11.4.1/11.4.1-2-1.js", false);
        }

        [Fact]
        [Trait("Category", "11.4.1")]
        public void DeleteOperatorReturnsTrueWhenDeletingReturnedValueFromAFunction()
        {
			RunTest(@"TestCases/ch11/11.4/11.4.1/11.4.1-2-2.js", false);
        }

        [Fact]
        [Trait("Category", "11.4.1")]
        public void DeleteOperatorReturnsTrueWhenDeletingANonReferenceBoolean()
        {
			RunTest(@"TestCases/ch11/11.4/11.4.1/11.4.1-2-3.js", false);
        }

        [Fact]
        [Trait("Category", "11.4.1")]
        public void DeleteOperatorReturnsTrueWhenDeletingANonReferenceString()
        {
			RunTest(@"TestCases/ch11/11.4/11.4.1/11.4.1-2-4.js", false);
        }

        [Fact]
        [Trait("Category", "11.4.1")]
        public void DeleteOperatorReturnsTrueWhenDeletingANonReferenceObj()
        {
			RunTest(@"TestCases/ch11/11.4/11.4.1/11.4.1-2-5.js", false);
        }

        [Fact]
        [Trait("Category", "11.4.1")]
        public void DeleteOperatorReturnsTrueWhenDeletingANonReferenceNull()
        {
			RunTest(@"TestCases/ch11/11.4/11.4.1/11.4.1-2-6.js", false);
        }

        [Fact]
        [Trait("Category", "11.4.1")]
        public void DeleteOperatorReturnsTrueWhenDeletingAnUnresolvableReference()
        {
			RunTest(@"TestCases/ch11/11.4/11.4.1/11.4.1-3-1.js", false);
        }

        [Fact]
        [Trait("Category", "11.4.1")]
        public void DeleteOperatorThrowsReferenceerrorWhenDeletingAnExplicitlyQualifiedYetUnresolvableReferenceBaseObjUndefined()
        {
			RunTest(@"TestCases/ch11/11.4/11.4.1/11.4.1-3-2.js", false);
        }

        [Fact]
        [Trait("Category", "11.4.1")]
        public void DeleteOperatorReturnsTrueWhenDeletingAnExplicitlyQualifiedYetUnresolvableReferencePropertyUndefinedForBaseObj()
        {
			RunTest(@"TestCases/ch11/11.4/11.4.1/11.4.1-3-3.js", false);
        }

        [Fact]
        [Trait("Category", "11.4.1")]
        public void StrictModeSyntaxerrorIsThrownWhenDeletingAnUnResolvableReference()
        {
			RunTest(@"TestCases/ch11/11.4/11.4.1/11.4.1-3-a-1-s.js", false);
        }

        [Fact]
        [Trait("Category", "11.4.1")]
        public void StrictModeTypeerrorIsThrownWhenDeletingNonConfigurableDataProperty()
        {
			RunTest(@"TestCases/ch11/11.4/11.4.1/11.4.1-4-a-1-s.js", false);
        }

        [Fact]
        [Trait("Category", "11.4.1")]
        public void StrictModeTypeerrorIsThrownWhenDeletingNonConfigurableAccessorProperty()
        {
			RunTest(@"TestCases/ch11/11.4/11.4.1/11.4.1-4-a-2-s.js", false);
        }

        [Fact]
        [Trait("Category", "11.4.1")]
        public void StrictModeTypeerrorIsnTThrownWhenDeletingConfigurableDataProperty()
        {
			RunTest(@"TestCases/ch11/11.4/11.4.1/11.4.1-4-a-3-s.js", false);
        }

        [Fact]
        [Trait("Category", "11.4.1")]
        public void StrictModeTypeerrorIsnTThrownWhenDeletingConfigurableAccessorProperty()
        {
			RunTest(@"TestCases/ch11/11.4/11.4.1/11.4.1-4-a-4-s.js", false);
        }

        [Fact]
        [Trait("Category", "11.4.1")]
        public void ThisTestIsActuallyTestingTheDeleteInternalMethod8128SinceTheLanguageProvidesNoWayToDirectlyExerciseDeleteTheTestsArePlacedHere2()
        {
			RunTest(@"TestCases/ch11/11.4/11.4.1/11.4.1-4.a-1.js", false);
        }

        [Fact]
        [Trait("Category", "11.4.1")]
        public void ThisTestIsActuallyTestingTheDeleteInternalMethod8128SinceTheLanguageProvidesNoWayToDirectlyExerciseDeleteTheTestsArePlacedHere3()
        {
			RunTest(@"TestCases/ch11/11.4/11.4.1/11.4.1-4.a-10.js", false);
        }

        [Fact]
        [Trait("Category", "11.4.1")]
        public void ThisTestIsActuallyTestingTheDeleteInternalMethod8128SinceTheLanguageProvidesNoWayToDirectlyExerciseDeleteTheTestsArePlacedHere4()
        {
			RunTest(@"TestCases/ch11/11.4/11.4.1/11.4.1-4.a-11.js", false);
        }

        [Fact]
        [Trait("Category", "11.4.1")]
        public void ThisTestIsActuallyTestingTheDeleteInternalMethod8128SinceTheLanguageProvidesNoWayToDirectlyExerciseDeleteTheTestsArePlacedHere5()
        {
			RunTest(@"TestCases/ch11/11.4/11.4.1/11.4.1-4.a-12.js", false);
        }

        [Fact]
        [Trait("Category", "11.4.1")]
        public void ThisTestIsActuallyTestingTheDeleteInternalMethod8128SinceTheLanguageProvidesNoWayToDirectlyExerciseDeleteTheTestsArePlacedHere6()
        {
			RunTest(@"TestCases/ch11/11.4/11.4.1/11.4.1-4.a-13.js", false);
        }

        [Fact]
        [Trait("Category", "11.4.1")]
        public void ThisTestIsActuallyTestingTheDeleteInternalMethod8128SinceTheLanguageProvidesNoWayToDirectlyExerciseDeleteTheTestsArePlacedHere7()
        {
			RunTest(@"TestCases/ch11/11.4/11.4.1/11.4.1-4.a-14.js", false);
        }

        [Fact]
        [Trait("Category", "11.4.1")]
        public void ThisTestIsActuallyTestingTheDeleteInternalMethod8128SinceTheLanguageProvidesNoWayToDirectlyExerciseDeleteTheTestsArePlacedHere8()
        {
			RunTest(@"TestCases/ch11/11.4/11.4.1/11.4.1-4.a-15.js", false);
        }

        [Fact]
        [Trait("Category", "11.4.1")]
        public void ThisTestIsActuallyTestingTheDeleteInternalMethod8128SinceTheLanguageProvidesNoWayToDirectlyExerciseDeleteTheTestsArePlacedHere9()
        {
			RunTest(@"TestCases/ch11/11.4/11.4.1/11.4.1-4.a-16.js", false);
        }

        [Fact]
        [Trait("Category", "11.4.1")]
        public void ThisTestIsActuallyTestingTheDeleteInternalMethod8128SinceTheLanguageProvidesNoWayToDirectlyExerciseDeleteTheTestsArePlacedHere10()
        {
			RunTest(@"TestCases/ch11/11.4/11.4.1/11.4.1-4.a-17.js", false);
        }

        [Fact]
        [Trait("Category", "11.4.1")]
        public void ThisTestIsActuallyTestingTheDeleteInternalMethod8128SinceTheLanguageProvidesNoWayToDirectlyExerciseDeleteTheTestsArePlacedHere11()
        {
			RunTest(@"TestCases/ch11/11.4/11.4.1/11.4.1-4.a-2.js", false);
        }

        [Fact]
        [Trait("Category", "11.4.1")]
        public void ThisTestIsActuallyTestingTheDeleteInternalMethod8128SinceTheLanguageProvidesNoWayToDirectlyExerciseDeleteTheTestsArePlacedHere12()
        {
			RunTest(@"TestCases/ch11/11.4/11.4.1/11.4.1-4.a-3-s.js", false);
        }

        [Fact]
        [Trait("Category", "11.4.1")]
        public void ThisTestIsActuallyTestingTheDeleteInternalMethod8128SinceTheLanguageProvidesNoWayToDirectlyExerciseDeleteTheTestsArePlacedHere13()
        {
			RunTest(@"TestCases/ch11/11.4/11.4.1/11.4.1-4.a-3.js", false);
        }

        [Fact]
        [Trait("Category", "11.4.1")]
        public void ThisTestIsActuallyTestingTheDeleteInternalMethod8128SinceTheLanguageProvidesNoWayToDirectlyExerciseDeleteTheTestsArePlacedHere14()
        {
			RunTest(@"TestCases/ch11/11.4/11.4.1/11.4.1-4.a-4.js", false);
        }

        [Fact]
        [Trait("Category", "11.4.1")]
        public void ThisTestIsActuallyTestingTheDeleteInternalMethod8128SinceTheLanguageProvidesNoWayToDirectlyExerciseDeleteTheTestsArePlacedHere15()
        {
			RunTest(@"TestCases/ch11/11.4/11.4.1/11.4.1-4.a-5.js", false);
        }

        [Fact]
        [Trait("Category", "11.4.1")]
        public void ThisTestIsActuallyTestingTheDeleteInternalMethod8128SinceTheLanguageProvidesNoWayToDirectlyExerciseDeleteTheTestsArePlacedHere16()
        {
			RunTest(@"TestCases/ch11/11.4/11.4.1/11.4.1-4.a-6.js", false);
        }

        [Fact]
        [Trait("Category", "11.4.1")]
        public void ThisTestIsActuallyTestingTheDeleteInternalMethod8128SinceTheLanguageProvidesNoWayToDirectlyExerciseDeleteTheTestsArePlacedHere17()
        {
			RunTest(@"TestCases/ch11/11.4/11.4.1/11.4.1-4.a-7.js", false);
        }

        [Fact]
        [Trait("Category", "11.4.1")]
        public void ThisTestIsActuallyTestingTheDeleteInternalMethod8128SinceTheLanguageProvidesNoWayToDirectlyExerciseDeleteTheTestsArePlacedHere18()
        {
			RunTest(@"TestCases/ch11/11.4/11.4.1/11.4.1-4.a-8-s.js", false);
        }

        [Fact]
        [Trait("Category", "11.4.1")]
        public void ThisTestIsActuallyTestingTheDeleteInternalMethod8128SinceTheLanguageProvidesNoWayToDirectlyExerciseDeleteTheTestsArePlacedHere19()
        {
			RunTest(@"TestCases/ch11/11.4/11.4.1/11.4.1-4.a-8.js", false);
        }

        [Fact]
        [Trait("Category", "11.4.1")]
        public void ThisTestIsActuallyTestingTheDeleteInternalMethod8128SinceTheLanguageProvidesNoWayToDirectlyExerciseDeleteTheTestsArePlacedHere20()
        {
			RunTest(@"TestCases/ch11/11.4/11.4.1/11.4.1-4.a-9-s.js", false);
        }

        [Fact]
        [Trait("Category", "11.4.1")]
        public void ThisTestIsActuallyTestingTheDeleteInternalMethod8128SinceTheLanguageProvidesNoWayToDirectlyExerciseDeleteTheTestsArePlacedHere21()
        {
			RunTest(@"TestCases/ch11/11.4/11.4.1/11.4.1-4.a-9.js", false);
        }

        [Fact]
        [Trait("Category", "11.4.1")]
        public void DeleteOperatorReturnsFalseWhenDeletingADirectReferenceToAVar()
        {
			RunTest(@"TestCases/ch11/11.4/11.4.1/11.4.1-5-1.js", false);
        }

        [Fact]
        [Trait("Category", "11.4.1")]
        public void DeleteOperatorReturnsFalseWhenDeletingADirectReferenceToAFunctionArgument()
        {
			RunTest(@"TestCases/ch11/11.4/11.4.1/11.4.1-5-2.js", false);
        }

        [Fact]
        [Trait("Category", "11.4.1")]
        public void DeleteOperatorReturnsFalseWhenDeletingADirectReferenceToAFunctionName()
        {
			RunTest(@"TestCases/ch11/11.4/11.4.1/11.4.1-5-3.js", false);
        }

        [Fact]
        [Trait("Category", "11.4.1")]
        public void StrictModeSyntaxerrorIsThrownWhenDeletingAVariableWhichIsAPrimitiveValueTypeNumber()
        {
			RunTest(@"TestCases/ch11/11.4/11.4.1/11.4.1-5-a-1-s.js", false);
        }

        [Fact]
        [Trait("Category", "11.4.1")]
        public void StrictModeSyntaxerrorIsThrownWhenDeletingAVariableOfTypeArray()
        {
			RunTest(@"TestCases/ch11/11.4/11.4.1/11.4.1-5-a-10-s.js", false);
        }

        [Fact]
        [Trait("Category", "11.4.1")]
        public void StrictModeSyntaxerrorIsThrownWhenDeletingAVariableOfTypeString()
        {
			RunTest(@"TestCases/ch11/11.4/11.4.1/11.4.1-5-a-11-s.js", false);
        }

        [Fact]
        [Trait("Category", "11.4.1")]
        public void StrictModeSyntaxerrorIsThrownWhenDeletingAVariableOfTypeBoolean()
        {
			RunTest(@"TestCases/ch11/11.4/11.4.1/11.4.1-5-a-12-s.js", false);
        }

        [Fact]
        [Trait("Category", "11.4.1")]
        public void StrictModeSyntaxerrorIsThrownWhenDeletingAVariableOfTypeNumber()
        {
			RunTest(@"TestCases/ch11/11.4/11.4.1/11.4.1-5-a-13-s.js", false);
        }

        [Fact]
        [Trait("Category", "11.4.1")]
        public void StrictModeSyntaxerrorIsThrownWhenDeletingAVariableOfTypeDate()
        {
			RunTest(@"TestCases/ch11/11.4/11.4.1/11.4.1-5-a-14-s.js", false);
        }

        [Fact]
        [Trait("Category", "11.4.1")]
        public void StrictModeSyntaxerrorIsThrownWhenDeletingAVariableOfTypeRegexp()
        {
			RunTest(@"TestCases/ch11/11.4/11.4.1/11.4.1-5-a-15-s.js", false);
        }

        [Fact]
        [Trait("Category", "11.4.1")]
        public void StrictModeSyntaxerrorIsThrownWhenDeletingAVariableOfTypeError()
        {
			RunTest(@"TestCases/ch11/11.4/11.4.1/11.4.1-5-a-16-s.js", false);
        }

        [Fact]
        [Trait("Category", "11.4.1")]
        public void StrictModeSyntaxerrorIsThrownWhenDeletingAVariableOfTypeArguments()
        {
			RunTest(@"TestCases/ch11/11.4/11.4.1/11.4.1-5-a-17-s.js", false);
        }

        [Fact]
        [Trait("Category", "11.4.1")]
        public void StrictModeSyntaxerrorIsThrownWhenDeletingABuiltInObject()
        {
			RunTest(@"TestCases/ch11/11.4/11.4.1/11.4.1-5-a-18-s.js", false);
        }

        [Fact]
        [Trait("Category", "11.4.1")]
        public void StrictModeSyntaxerrorIsThrownWhenDeletingABuiltInFunction()
        {
			RunTest(@"TestCases/ch11/11.4/11.4.1/11.4.1-5-a-19-s.js", false);
        }

        [Fact]
        [Trait("Category", "11.4.1")]
        public void StrictModeSyntaxerrorIsThrownWhenDeletingAFunctionParameter()
        {
			RunTest(@"TestCases/ch11/11.4/11.4.1/11.4.1-5-a-2-s.js", false);
        }

        [Fact]
        [Trait("Category", "11.4.1")]
        public void StrictModeSyntaxerrorIsThrownWhenDeletingABuiltInArray()
        {
			RunTest(@"TestCases/ch11/11.4/11.4.1/11.4.1-5-a-20-s.js", false);
        }

        [Fact]
        [Trait("Category", "11.4.1")]
        public void StrictModeSyntaxerrorIsThrownWhenDeletingABuiltInString()
        {
			RunTest(@"TestCases/ch11/11.4/11.4.1/11.4.1-5-a-21-s.js", false);
        }

        [Fact]
        [Trait("Category", "11.4.1")]
        public void StrictModeSyntaxerrorIsThrownWhenDeletingABuiltInBoolean()
        {
			RunTest(@"TestCases/ch11/11.4/11.4.1/11.4.1-5-a-22-s.js", false);
        }

        [Fact]
        [Trait("Category", "11.4.1")]
        public void StrictModeSyntaxerrorIsThrownWhenDeletingABuiltInNumber()
        {
			RunTest(@"TestCases/ch11/11.4/11.4.1/11.4.1-5-a-23-s.js", false);
        }

        [Fact]
        [Trait("Category", "11.4.1")]
        public void StrictModeSyntaxerrorIsThrownWhenDeletingABuiltInDate()
        {
			RunTest(@"TestCases/ch11/11.4/11.4.1/11.4.1-5-a-24-s.js", false);
        }

        [Fact]
        [Trait("Category", "11.4.1")]
        public void StrictModeSyntaxerrorIsThrownWhenDeletingABuiltInRegexp()
        {
			RunTest(@"TestCases/ch11/11.4/11.4.1/11.4.1-5-a-25-s.js", false);
        }

        [Fact]
        [Trait("Category", "11.4.1")]
        public void StrictModeSyntaxerrorIsThrownWhenDeletingABuiltInError()
        {
			RunTest(@"TestCases/ch11/11.4/11.4.1/11.4.1-5-a-26-s.js", false);
        }

        [Fact]
        [Trait("Category", "11.4.1")]
        public void StrictModeTypeerrorIsThrownAfterDeletingAPropertyCallingPreventextensionsAndAttemptingToReassignTheProperty()
        {
			RunTest(@"TestCases/ch11/11.4/11.4.1/11.4.1-5-a-27-s.js", false);
        }

        [Fact]
        [Trait("Category", "11.4.1")]
        public void StrictModeTypeerrorIsThrownWhenDeletingRegexpLength()
        {
			RunTest(@"TestCases/ch11/11.4/11.4.1/11.4.1-5-a-28-s.js", false);
        }

        [Fact]
        [Trait("Category", "11.4.1")]
        public void StrictModeSyntaxerrorIsThrownWhenDeletingAFunctionName()
        {
			RunTest(@"TestCases/ch11/11.4/11.4.1/11.4.1-5-a-3-s.js", false);
        }

        [Fact]
        [Trait("Category", "11.4.1")]
        public void StrictModeSyntaxerrorIsThrownWhenDeletingAFunctionParameter2()
        {
			RunTest(@"TestCases/ch11/11.4/11.4.1/11.4.1-5-a-4-s.js", false);
        }

        [Fact]
        [Trait("Category", "11.4.1")]
        public void StrictModeSyntaxerrorIsThrownWhenDeletingAVariableWhichIsAPrimitiveTypeBoolean()
        {
			RunTest(@"TestCases/ch11/11.4/11.4.1/11.4.1-5-a-5-s.js", false);
        }

        [Fact]
        [Trait("Category", "11.4.1")]
        public void StrictModeSyntaxerrorIsThrownWhenDeletingAVariableWhichIsPrimitiveTypeBoolean()
        {
			RunTest(@"TestCases/ch11/11.4/11.4.1/11.4.1-5-a-5gs.js", true);
        }

        [Fact]
        [Trait("Category", "11.4.1")]
        public void StrictModeSyntaxerrorIsThrownWhenDeletingAVariableWhichIsAPrimitiveTypeString()
        {
			RunTest(@"TestCases/ch11/11.4/11.4.1/11.4.1-5-a-6-s.js", false);
        }

        [Fact]
        [Trait("Category", "11.4.1")]
        public void StrictModeSyntaxerrorIsThrownWhenDeletingAVariableOfTypeObject()
        {
			RunTest(@"TestCases/ch11/11.4/11.4.1/11.4.1-5-a-7-s.js", false);
        }

        [Fact]
        [Trait("Category", "11.4.1")]
        public void StrictModeSyntaxerrorIsThrownWhenDeletingAFunctionObject()
        {
			RunTest(@"TestCases/ch11/11.4/11.4.1/11.4.1-5-a-8-s.js", false);
        }

        [Fact]
        [Trait("Category", "11.4.1")]
        public void StrictModeSyntaxerrorIsThrownWhenDeletingAVariableOfTypeFunctionDeclaration()
        {
			RunTest(@"TestCases/ch11/11.4/11.4.1/11.4.1-5-a-9-s.js", false);
        }

        [Fact]
        [Trait("Category", "11.4.1")]
        public void WhiteSpaceAndLineTerminatorBetweenDeleteAndUnaryexpressionAreAllowed()
        {
			RunTest(@"TestCases/ch11/11.4/11.4.1/S11.4.1_A1.js", false);
        }

        [Fact]
        [Trait("Category", "11.4.1")]
        public void IfTypeXIsNotReferenceReturnTrue()
        {
			RunTest(@"TestCases/ch11/11.4/11.4.1/S11.4.1_A2.1.js", false);
        }

        [Fact]
        [Trait("Category", "11.4.1")]
        public void IfGetbaseXDoesnTHaveAPropertyGetpropertynameXReturnTrue()
        {
			RunTest(@"TestCases/ch11/11.4/11.4.1/S11.4.1_A2.2_T1.js", false);
        }

        [Fact]
        [Trait("Category", "11.4.1")]
        public void IfGetbaseXDoesnTHaveAPropertyGetpropertynameXReturnTrue2()
        {
			RunTest(@"TestCases/ch11/11.4/11.4.1/S11.4.1_A2.2_T2.js", false);
        }

        [Fact]
        [Trait("Category", "11.4.1")]
        public void IfThePropertyHasTheDontdeleteAttributeReturnFalse()
        {
			RunTest(@"TestCases/ch11/11.4/11.4.1/S11.4.1_A3.1.js", false);
        }

        [Fact]
        [Trait("Category", "11.4.1")]
        public void IfThePropertyDoesnTHaveTheDontdeleteAttributeReturnTrue()
        {
			RunTest(@"TestCases/ch11/11.4/11.4.1/S11.4.1_A3.2.js", false);
        }

        [Fact]
        [Trait("Category", "11.4.1")]
        public void IfThePropertyDoesnTHaveTheDontdeleteAttributeRemoveTheProperty()
        {
			RunTest(@"TestCases/ch11/11.4/11.4.1/S11.4.1_A3.3.js", false);
        }

        [Fact]
        [Trait("Category", "11.4.1")]
        public void DeleteOperatorRemovesPropertyWhichIsReferenceToTheObjectNotTheObject()
        {
			RunTest(@"TestCases/ch11/11.4/11.4.1/S11.4.1_A4.js", false);
        }

        [Fact]
        [Trait("Category", "11.4.1")]
        public void AStrictDeleteShouldEitherSucceedReturningTrueOrItShouldFailByThrowingATypeerrorUnderNoCircumstancesShouldAStrictDeleteReturnFalse()
        {
			RunTest(@"TestCases/ch11/11.4/11.4.1/S11.4.1_A5.js", false);
        }


    }
}
