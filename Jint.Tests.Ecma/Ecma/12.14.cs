using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_12_14 : EcmaTest
    {
        [Fact]
        [Trait("Category", "12.14")]
        public void CatchDoesnTChangeDeclarationScopeVarInitializerInCatchWithSameNameAsCatchParameterChangesParameter()
        {
			RunTest(@"TestCases/ch12/12.14/12.14-1.js", false);
        }

        [Fact]
        [Trait("Category", "12.14")]
        public void CatchIntroducesScopeNameLookupFindsFunctionParameter()
        {
			RunTest(@"TestCases/ch12/12.14/12.14-10.js", false);
        }

        [Fact]
        [Trait("Category", "12.14")]
        public void CatchIntroducesScopeNameLookupFindsInnerVariable()
        {
			RunTest(@"TestCases/ch12/12.14/12.14-11.js", false);
        }

        [Fact]
        [Trait("Category", "12.14")]
        public void CatchIntroducesScopeNameLookupFindsProperty()
        {
			RunTest(@"TestCases/ch12/12.14/12.14-12.js", false);
        }

        [Fact]
        [Trait("Category", "12.14")]
        public void CatchIntroducesScopeUpdatesAreBasedOnScope()
        {
			RunTest(@"TestCases/ch12/12.14/12.14-13.js", false);
        }

        [Fact]
        [Trait("Category", "12.14")]
        public void ExceptionObjectIsAFunctionWhenAnExceptionParameterIsCalledAsAFunctionInCatchBlockGlobalObjectIsPassedAsTheThisValue()
        {
			RunTest(@"TestCases/ch12/12.14/12.14-14.js", false);
        }

        [Fact]
        [Trait("Category", "12.14")]
        public void ExceptionObjectIsAFunctionWhichIsAPropertyOfAnObjectWhenAnExceptionParameterIsCalledAsAFunctionInCatchBlockGlobalObjectIsPassedAsTheThisValue()
        {
			RunTest(@"TestCases/ch12/12.14/12.14-15.js", false);
        }

        [Fact]
        [Trait("Category", "12.14")]
        public void ExceptionObjectIsAFunctionWhichUpdateInCatchBlockWhenAnExceptionParameterIsCalledAsAFunctionInCatchBlockGlobalObjectIsPassedAsTheThisValue()
        {
			RunTest(@"TestCases/ch12/12.14/12.14-16.js", false);
        }

        [Fact]
        [Trait("Category", "12.14")]
        public void CatchDoesnTChangeDeclarationScopeVarInitializerInCatchWithSameNameAsCatchParameterChangesParameter2()
        {
			RunTest(@"TestCases/ch12/12.14/12.14-2.js", false);
        }

        [Fact]
        [Trait("Category", "12.14")]
        public void LocalVarsMustNotBeVisibleOutsideWithBlockLocalFunctionsMustNotBeVisibleOutsideWithBlockLocalFunctionExpresssionsShouldNotBeVisibleOutsideWithBlockLocalVarsMustShadowOuterVarsLocalFunctionsMustShadowOuterFunctionsLocalFunctionExpresssionsMustShadowOuterFunctionExpressionsEvalShouldUseTheAppendedObjectToTheScopeChain()
        {
			RunTest(@"TestCases/ch12/12.14/12.14-3.js", false);
        }

        [Fact]
        [Trait("Category", "12.14")]
        public void LocalVarsMustNotBeVisibleOutsideWithBlockLocalFunctionsMustNotBeVisibleOutsideWithBlockLocalFunctionExpresssionsShouldNotBeVisibleOutsideWithBlockLocalVarsMustShadowOuterVarsLocalFunctionsMustShadowOuterFunctionsLocalFunctionExpresssionsMustShadowOuterFunctionExpressionsEvalShouldUseTheAppendedObjectToTheScopeChain2()
        {
			RunTest(@"TestCases/ch12/12.14/12.14-4.js", false);
        }

        [Fact]
        [Trait("Category", "12.14")]
        public void LocalVarsMustNotBeVisibleOutsideWithBlockLocalFunctionsMustNotBeVisibleOutsideWithBlockLocalFunctionExpresssionsShouldNotBeVisibleOutsideWithBlockLocalVarsMustShadowOuterVarsLocalFunctionsMustShadowOuterFunctionsLocalFunctionExpresssionsMustShadowOuterFunctionExpressionsEvalShouldUseTheAppendedObjectToTheScopeChain3()
        {
			RunTest(@"TestCases/ch12/12.14/12.14-6.js", false);
        }

        [Fact]
        [Trait("Category", "12.14")]
        public void LocalVarsMustNotBeVisibleOutsideWithBlockLocalFunctionsMustNotBeVisibleOutsideWithBlockLocalFunctionExpresssionsShouldNotBeVisibleOutsideWithBlockLocalVarsMustShadowOuterVarsLocalFunctionsMustShadowOuterFunctionsLocalFunctionExpresssionsMustShadowOuterFunctionExpressionsEvalShouldUseTheAppendedObjectToTheScopeChain4()
        {
			RunTest(@"TestCases/ch12/12.14/12.14-7.js", false);
        }

        [Fact]
        [Trait("Category", "12.14")]
        public void LocalVarsMustNotBeVisibleOutsideWithBlockLocalFunctionsMustNotBeVisibleOutsideWithBlockLocalFunctionExpresssionsShouldNotBeVisibleOutsideWithBlockLocalVarsMustShadowOuterVarsLocalFunctionsMustShadowOuterFunctionsLocalFunctionExpresssionsMustShadowOuterFunctionExpressionsEvalShouldUseTheAppendedObjectToTheScopeChain5()
        {
			RunTest(@"TestCases/ch12/12.14/12.14-8.js", false);
        }

        [Fact]
        [Trait("Category", "12.14")]
        public void CatchIntroducesScopeNameLookupFindsOuterVariable()
        {
			RunTest(@"TestCases/ch12/12.14/12.14-9.js", false);
        }

        [Fact]
        [Trait("Category", "12.14")]
        public void TheProductionTrystatementTryBlockCatchIsEvaluatedAsFollows2IfResult1TypeIsNotThrowReturnResult1()
        {
			RunTest(@"TestCases/ch12/12.14/S12.14_A1.js", false);
        }

        [Fact]
        [Trait("Category", "12.14")]
        public void UsingTryWithCatchOrFinallyStatementWithinWithoutAWhileStatement()
        {
			RunTest(@"TestCases/ch12/12.14/S12.14_A10_T1.js", false);
        }

        [Fact]
        [Trait("Category", "12.14")]
        public void UsingTryWithCatchOrFinallyStatementWithinWithoutAWhileStatement2()
        {
			RunTest(@"TestCases/ch12/12.14/S12.14_A10_T2.js", false);
        }

        [Fact]
        [Trait("Category", "12.14")]
        public void UsingTryWithCatchOrFinallyStatementWithinWithoutAWhileStatement3()
        {
			RunTest(@"TestCases/ch12/12.14/S12.14_A10_T3.js", false);
        }

        [Fact]
        [Trait("Category", "12.14")]
        public void UsingTryWithCatchOrFinallyStatementWithinWithoutAWhileStatement4()
        {
			RunTest(@"TestCases/ch12/12.14/S12.14_A10_T4.js", false);
        }

        [Fact]
        [Trait("Category", "12.14")]
        public void UsingTryWithCatchOrFinallyStatementWithinWithoutAWhileStatement5()
        {
			RunTest(@"TestCases/ch12/12.14/S12.14_A10_T5.js", false);
        }

        [Fact]
        [Trait("Category", "12.14")]
        public void UsingTryWithCatchOrFinallyStatementWithinWithoutAForStatement()
        {
			RunTest(@"TestCases/ch12/12.14/S12.14_A11_T1.js", false);
        }

        [Fact]
        [Trait("Category", "12.14")]
        public void UsingTryWithCatchOrFinallyStatementWithinWithoutAForStatement2()
        {
			RunTest(@"TestCases/ch12/12.14/S12.14_A11_T2.js", false);
        }

        [Fact]
        [Trait("Category", "12.14")]
        public void UsingTryWithCatchOrFinallyStatementWithinWithoutAForStatement3()
        {
			RunTest(@"TestCases/ch12/12.14/S12.14_A11_T3.js", false);
        }

        [Fact]
        [Trait("Category", "12.14")]
        public void UsingTryWithCatchOrFinallyStatementWithinWithoutAForStatement4()
        {
			RunTest(@"TestCases/ch12/12.14/S12.14_A11_T4.js", false);
        }

        [Fact]
        [Trait("Category", "12.14")]
        public void UsingTryWithCatchOrFinallyStatementWithinWithoutAForInStatement()
        {
			RunTest(@"TestCases/ch12/12.14/S12.14_A12_T1.js", false);
        }

        [Fact]
        [Trait("Category", "12.14")]
        public void UsingTryWithCatchOrFinallyStatementWithinWithoutAForInStatement2()
        {
			RunTest(@"TestCases/ch12/12.14/S12.14_A12_T2.js", false);
        }

        [Fact]
        [Trait("Category", "12.14")]
        public void UsingTryWithCatchOrFinallyStatementWithinWithoutAForInStatement3()
        {
			RunTest(@"TestCases/ch12/12.14/S12.14_A12_T3.js", false);
        }

        [Fact]
        [Trait("Category", "12.14")]
        public void UsingTryWithCatchOrFinallyStatementWithinWithoutAForInStatement4()
        {
			RunTest(@"TestCases/ch12/12.14/S12.14_A12_T4.js", false);
        }

        [Fact]
        [Trait("Category", "12.14")]
        public void UsingTryWithCatchOrFinallyStatementWithAReturnStatement()
        {
			RunTest(@"TestCases/ch12/12.14/S12.14_A13_T1.js", false);
        }

        [Fact]
        [Trait("Category", "12.14")]
        public void UsingTryWithCatchOrFinallyStatementWithAReturnStatement2()
        {
			RunTest(@"TestCases/ch12/12.14/S12.14_A13_T2.js", false);
        }

        [Fact]
        [Trait("Category", "12.14")]
        public void UsingTryWithCatchOrFinallyStatementWithAReturnStatement3()
        {
			RunTest(@"TestCases/ch12/12.14/S12.14_A13_T3.js", false);
        }

        [Fact]
        [Trait("Category", "12.14")]
        public void UsingTryWithCatchOrFinallyStatementWithinWithoutAWithStatement()
        {
			RunTest(@"TestCases/ch12/12.14/S12.14_A14.js", false);
        }

        [Fact]
        [Trait("Category", "12.14")]
        public void UsingTryWithCatchOrFinallyStatementWithinWithoutASwitchStatement()
        {
			RunTest(@"TestCases/ch12/12.14/S12.14_A15.js", false);
        }

        [Fact]
        [Trait("Category", "12.14")]
        public void TrystatementTryBlockCatchOrTryBlockFinallyOrTryBlockCatchFinally()
        {
			RunTest(@"TestCases/ch12/12.14/S12.14_A16_T1.js", true);
        }

        [Fact]
        [Trait("Category", "12.14")]
        public void TrystatementTryBlockCatchOrTryBlockFinallyOrTryBlockCatchFinally2()
        {
			RunTest(@"TestCases/ch12/12.14/S12.14_A16_T10.js", true);
        }

        [Fact]
        [Trait("Category", "12.14")]
        public void TrystatementTryBlockCatchOrTryBlockFinallyOrTryBlockCatchFinally3()
        {
			RunTest(@"TestCases/ch12/12.14/S12.14_A16_T11.js", true);
        }

        [Fact]
        [Trait("Category", "12.14")]
        public void TrystatementTryBlockCatchOrTryBlockFinallyOrTryBlockCatchFinally4()
        {
			RunTest(@"TestCases/ch12/12.14/S12.14_A16_T12.js", true);
        }

        [Fact]
        [Trait("Category", "12.14")]
        public void TrystatementTryBlockCatchOrTryBlockFinallyOrTryBlockCatchFinally5()
        {
			RunTest(@"TestCases/ch12/12.14/S12.14_A16_T13.js", true);
        }

        [Fact]
        [Trait("Category", "12.14")]
        public void TrystatementTryBlockCatchOrTryBlockFinallyOrTryBlockCatchFinally6()
        {
			RunTest(@"TestCases/ch12/12.14/S12.14_A16_T14.js", true);
        }

        [Fact]
        [Trait("Category", "12.14")]
        public void TrystatementTryBlockCatchOrTryBlockFinallyOrTryBlockCatchFinally7()
        {
			RunTest(@"TestCases/ch12/12.14/S12.14_A16_T15.js", true);
        }

        [Fact]
        [Trait("Category", "12.14")]
        public void TrystatementTryBlockCatchOrTryBlockFinallyOrTryBlockCatchFinally8()
        {
			RunTest(@"TestCases/ch12/12.14/S12.14_A16_T2.js", true);
        }

        [Fact]
        [Trait("Category", "12.14")]
        public void TrystatementTryBlockCatchOrTryBlockFinallyOrTryBlockCatchFinally9()
        {
			RunTest(@"TestCases/ch12/12.14/S12.14_A16_T3.js", true);
        }

        [Fact]
        [Trait("Category", "12.14")]
        public void TrystatementTryBlockCatchOrTryBlockFinallyOrTryBlockCatchFinally10()
        {
			RunTest(@"TestCases/ch12/12.14/S12.14_A16_T4.js", true);
        }

        [Fact]
        [Trait("Category", "12.14")]
        public void TrystatementTryBlockCatchOrTryBlockFinallyOrTryBlockCatchFinally11()
        {
			RunTest(@"TestCases/ch12/12.14/S12.14_A16_T5.js", true);
        }

        [Fact]
        [Trait("Category", "12.14")]
        public void TrystatementTryBlockCatchOrTryBlockFinallyOrTryBlockCatchFinally12()
        {
			RunTest(@"TestCases/ch12/12.14/S12.14_A16_T6.js", true);
        }

        [Fact]
        [Trait("Category", "12.14")]
        public void TrystatementTryBlockCatchOrTryBlockFinallyOrTryBlockCatchFinally13()
        {
			RunTest(@"TestCases/ch12/12.14/S12.14_A16_T7.js", true);
        }

        [Fact]
        [Trait("Category", "12.14")]
        public void TrystatementTryBlockCatchOrTryBlockFinallyOrTryBlockCatchFinally14()
        {
			RunTest(@"TestCases/ch12/12.14/S12.14_A16_T8.js", true);
        }

        [Fact]
        [Trait("Category", "12.14")]
        public void TrystatementTryBlockCatchOrTryBlockFinallyOrTryBlockCatchFinally15()
        {
			RunTest(@"TestCases/ch12/12.14/S12.14_A16_T9.js", true);
        }

        [Fact]
        [Trait("Category", "12.14")]
        public void UsingTryWithCatchOrFinallyStatementInAConstructor()
        {
			RunTest(@"TestCases/ch12/12.14/S12.14_A17.js", false);
        }

        [Fact]
        [Trait("Category", "12.14")]
        public void CatchingObjectsWithTryCatchFinallyStatement()
        {
			RunTest(@"TestCases/ch12/12.14/S12.14_A18_T1.js", false);
        }

        [Fact]
        [Trait("Category", "12.14")]
        public void CatchingObjectsWithTryCatchFinallyStatement2()
        {
			RunTest(@"TestCases/ch12/12.14/S12.14_A18_T2.js", false);
        }

        [Fact]
        [Trait("Category", "12.14")]
        public void CatchingObjectsWithTryCatchFinallyStatement3()
        {
			RunTest(@"TestCases/ch12/12.14/S12.14_A18_T3.js", false);
        }

        [Fact]
        [Trait("Category", "12.14")]
        public void CatchingObjectsWithTryCatchFinallyStatement4()
        {
			RunTest(@"TestCases/ch12/12.14/S12.14_A18_T4.js", false);
        }

        [Fact]
        [Trait("Category", "12.14")]
        public void CatchingObjectsWithTryCatchFinallyStatement5()
        {
			RunTest(@"TestCases/ch12/12.14/S12.14_A18_T5.js", false);
        }

        [Fact]
        [Trait("Category", "12.14")]
        public void CatchingObjectsWithTryCatchFinallyStatement6()
        {
			RunTest(@"TestCases/ch12/12.14/S12.14_A18_T6.js", false);
        }

        [Fact]
        [Trait("Category", "12.14")]
        public void CatchingObjectsWithTryCatchFinallyStatement7()
        {
			RunTest(@"TestCases/ch12/12.14/S12.14_A18_T7.js", false);
        }

        [Fact]
        [Trait("Category", "12.14")]
        public void CatchingSystemExceptionsOfDifferentTypesWithTryStatement()
        {
			RunTest(@"TestCases/ch12/12.14/S12.14_A19_T1.js", false);
        }

        [Fact]
        [Trait("Category", "12.14")]
        public void CatchingSystemExceptionsOfDifferentTypesWithTryStatement2()
        {
			RunTest(@"TestCases/ch12/12.14/S12.14_A19_T2.js", false);
        }

        [Fact]
        [Trait("Category", "12.14")]
        public void ThrowingExceptionWithThrowAndCatchingItWithTryStatement()
        {
			RunTest(@"TestCases/ch12/12.14/S12.14_A2.js", false);
        }

        [Fact]
        [Trait("Category", "12.14")]
        public void CatchingSystemExceptionWithTryStatement()
        {
			RunTest(@"TestCases/ch12/12.14/S12.14_A3.js", false);
        }

        [Fact]
        [Trait("Category", "12.14")]
        public void SanityTestForCatchIndetifierStatement()
        {
			RunTest(@"TestCases/ch12/12.14/S12.14_A4.js", false);
        }

        [Fact]
        [Trait("Category", "12.14")]
        public void TheProductionTrystatementTryBlockFinallyAndTheProductionTrystatementTryBlockCatchFinally()
        {
			RunTest(@"TestCases/ch12/12.14/S12.14_A5.js", false);
        }

        [Fact]
        [Trait("Category", "12.14")]
        public void TheProductionTrystatementTryBlockCatchFinally()
        {
			RunTest(@"TestCases/ch12/12.14/S12.14_A6.js", false);
        }

        [Fact]
        [Trait("Category", "12.14")]
        public void EvaluatingTheNestedProductionsTrystatement()
        {
			RunTest(@"TestCases/ch12/12.14/S12.14_A7_T1.js", false);
        }

        [Fact]
        [Trait("Category", "12.14")]
        public void EvaluatingTheNestedProductionsTrystatement2()
        {
			RunTest(@"TestCases/ch12/12.14/S12.14_A7_T2.js", false);
        }

        [Fact]
        [Trait("Category", "12.14")]
        public void EvaluatingTheNestedProductionsTrystatement3()
        {
			RunTest(@"TestCases/ch12/12.14/S12.14_A7_T3.js", false);
        }

        [Fact]
        [Trait("Category", "12.14")]
        public void TryWithCatchOrFinallyStatementWithinWithoutAnIfStatement()
        {
			RunTest(@"TestCases/ch12/12.14/S12.14_A8.js", false);
        }

        [Fact]
        [Trait("Category", "12.14")]
        public void TryWithCatchOrFinallyStatementWithinWithoutAnDoWhileStatement()
        {
			RunTest(@"TestCases/ch12/12.14/S12.14_A9_T1.js", false);
        }

        [Fact]
        [Trait("Category", "12.14")]
        public void TryWithCatchOrFinallyStatementWithinWithoutAnDoWhileStatement2()
        {
			RunTest(@"TestCases/ch12/12.14/S12.14_A9_T2.js", false);
        }

        [Fact]
        [Trait("Category", "12.14")]
        public void TryWithCatchOrFinallyStatementWithinWithoutAnDoWhileStatement3()
        {
			RunTest(@"TestCases/ch12/12.14/S12.14_A9_T3.js", false);
        }

        [Fact]
        [Trait("Category", "12.14")]
        public void TryWithCatchOrFinallyStatementWithinWithoutAnDoWhileStatement4()
        {
			RunTest(@"TestCases/ch12/12.14/S12.14_A9_T4.js", false);
        }

        [Fact]
        [Trait("Category", "12.14")]
        public void TryWithCatchOrFinallyStatementWithinWithoutAnDoWhileStatement5()
        {
			RunTest(@"TestCases/ch12/12.14/S12.14_A9_T5.js", false);
        }


    }
}
