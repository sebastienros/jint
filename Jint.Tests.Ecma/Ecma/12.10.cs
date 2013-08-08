using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_12_10 : EcmaTest
    {
        [Fact]
        [Trait("Category", "12.10")]
        public void WithDoesNotChangeDeclarationScopeVarsInWithAreVisibleOutside()
        {
			RunTest(@"TestCases/ch12/12.10/12.10-0-1.js", false);
        }

        [Fact]
        [Trait("Category", "12.10")]
        public void WithIntroducesScopeNameLookupFindsFunctionParameter()
        {
			RunTest(@"TestCases/ch12/12.10/12.10-0-10.js", false);
        }

        [Fact]
        [Trait("Category", "12.10")]
        public void WithIntroducesScopeNameLookupFindsInnerVariable()
        {
			RunTest(@"TestCases/ch12/12.10/12.10-0-11.js", false);
        }

        [Fact]
        [Trait("Category", "12.10")]
        public void WithIntroducesScopeNameLookupFindsProperty()
        {
			RunTest(@"TestCases/ch12/12.10/12.10-0-12.js", false);
        }

        [Fact]
        [Trait("Category", "12.10")]
        public void WithIntroducesScopeThatIsCapturedByFunctionExpression()
        {
			RunTest(@"TestCases/ch12/12.10/12.10-0-3.js", false);
        }

        [Fact]
        [Trait("Category", "12.10")]
        public void WithIntroducesScopeScopeRemovedWhenExitingWithStatement()
        {
			RunTest(@"TestCases/ch12/12.10/12.10-0-7.js", false);
        }

        [Fact]
        [Trait("Category", "12.10")]
        public void WithIntroducesScopeVarInitializerSetsLikeNamedProperty()
        {
			RunTest(@"TestCases/ch12/12.10/12.10-0-8.js", false);
        }

        [Fact]
        [Trait("Category", "12.10")]
        public void WithIntroducesScopeNameLookupFindsOuterVariable()
        {
			RunTest(@"TestCases/ch12/12.10/12.10-0-9.js", false);
        }

        [Fact]
        [Trait("Category", "12.10")]
        public void WithExpressionBeingNumber()
        {
			RunTest(@"TestCases/ch12/12.10/12.10-2-1.js", false);
        }

        [Fact]
        [Trait("Category", "12.10")]
        public void WithExpressionBeingBoolean()
        {
			RunTest(@"TestCases/ch12/12.10/12.10-2-2.js", false);
        }

        [Fact]
        [Trait("Category", "12.10")]
        public void WithExpressionBeingString()
        {
			RunTest(@"TestCases/ch12/12.10/12.10-2-3.js", false);
        }

        [Fact]
        [Trait("Category", "12.10")]
        public void WithIntroducesScopeRestoresTheEarlierEnvironmentOnExit()
        {
			RunTest(@"TestCases/ch12/12.10/12.10-7-1.js", false);
        }

        [Fact]
        [Trait("Category", "12.10")]
        public void TheWithStatementAddsAComputedObjectToTheFrontOfTheScopeChainOfTheCurrentExecutionContext()
        {
			RunTest(@"TestCases/ch12/12.10/S12.10_A1.10_T1.js", false);
        }

        [Fact]
        [Trait("Category", "12.10")]
        public void TheWithStatementAddsAComputedObjectToTheFrontOfTheScopeChainOfTheCurrentExecutionContext2()
        {
			RunTest(@"TestCases/ch12/12.10/S12.10_A1.10_T2.js", false);
        }

        [Fact]
        [Trait("Category", "12.10")]
        public void TheWithStatementAddsAComputedObjectToTheFrontOfTheScopeChainOfTheCurrentExecutionContext3()
        {
			RunTest(@"TestCases/ch12/12.10/S12.10_A1.10_T3.js", false);
        }

        [Fact]
        [Trait("Category", "12.10")]
        public void TheWithStatementAddsAComputedObjectToTheFrontOfTheScopeChainOfTheCurrentExecutionContext4()
        {
			RunTest(@"TestCases/ch12/12.10/S12.10_A1.10_T4.js", false);
        }

        [Fact]
        [Trait("Category", "12.10")]
        public void TheWithStatementAddsAComputedObjectToTheFrontOfTheScopeChainOfTheCurrentExecutionContext5()
        {
			RunTest(@"TestCases/ch12/12.10/S12.10_A1.10_T5.js", false);
        }

        [Fact]
        [Trait("Category", "12.10")]
        public void TheWithStatementAddsAComputedObjectToTheFrontOfTheScopeChainOfTheCurrentExecutionContext6()
        {
			RunTest(@"TestCases/ch12/12.10/S12.10_A1.11_T1.js", false);
        }

        [Fact]
        [Trait("Category", "12.10")]
        public void TheWithStatementAddsAComputedObjectToTheFrontOfTheScopeChainOfTheCurrentExecutionContext7()
        {
			RunTest(@"TestCases/ch12/12.10/S12.10_A1.11_T2.js", false);
        }

        [Fact]
        [Trait("Category", "12.10")]
        public void TheWithStatementAddsAComputedObjectToTheFrontOfTheScopeChainOfTheCurrentExecutionContext8()
        {
			RunTest(@"TestCases/ch12/12.10/S12.10_A1.11_T3.js", false);
        }

        [Fact]
        [Trait("Category", "12.10")]
        public void TheWithStatementAddsAComputedObjectToTheFrontOfTheScopeChainOfTheCurrentExecutionContext9()
        {
			RunTest(@"TestCases/ch12/12.10/S12.10_A1.11_T4.js", false);
        }

        [Fact]
        [Trait("Category", "12.10")]
        public void TheWithStatementAddsAComputedObjectToTheFrontOfTheScopeChainOfTheCurrentExecutionContext10()
        {
			RunTest(@"TestCases/ch12/12.10/S12.10_A1.11_T5.js", false);
        }

        [Fact]
        [Trait("Category", "12.10")]
        public void TheWithStatementAddsAComputedObjectToTheFrontOfTheScopeChainOfTheCurrentExecutionContext11()
        {
			RunTest(@"TestCases/ch12/12.10/S12.10_A1.12_T1.js", false);
        }

        [Fact]
        [Trait("Category", "12.10")]
        public void TheWithStatementAddsAComputedObjectToTheFrontOfTheScopeChainOfTheCurrentExecutionContext12()
        {
			RunTest(@"TestCases/ch12/12.10/S12.10_A1.12_T2.js", false);
        }

        [Fact]
        [Trait("Category", "12.10")]
        public void TheWithStatementAddsAComputedObjectToTheFrontOfTheScopeChainOfTheCurrentExecutionContext13()
        {
			RunTest(@"TestCases/ch12/12.10/S12.10_A1.12_T3.js", false);
        }

        [Fact]
        [Trait("Category", "12.10")]
        public void TheWithStatementAddsAComputedObjectToTheFrontOfTheScopeChainOfTheCurrentExecutionContext14()
        {
			RunTest(@"TestCases/ch12/12.10/S12.10_A1.12_T4.js", false);
        }

        [Fact]
        [Trait("Category", "12.10")]
        public void TheWithStatementAddsAComputedObjectToTheFrontOfTheScopeChainOfTheCurrentExecutionContext15()
        {
			RunTest(@"TestCases/ch12/12.10/S12.10_A1.12_T5.js", false);
        }

        [Fact]
        [Trait("Category", "12.10")]
        public void TheWithStatementAddsAComputedObjectToTheFrontOfTheScopeChainOfTheCurrentExecutionContext16()
        {
			RunTest(@"TestCases/ch12/12.10/S12.10_A1.1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "12.10")]
        public void TheWithStatementAddsAComputedObjectToTheFrontOfTheScopeChainOfTheCurrentExecutionContext17()
        {
			RunTest(@"TestCases/ch12/12.10/S12.10_A1.1_T2.js", false);
        }

        [Fact]
        [Trait("Category", "12.10")]
        public void TheWithStatementAddsAComputedObjectToTheFrontOfTheScopeChainOfTheCurrentExecutionContext18()
        {
			RunTest(@"TestCases/ch12/12.10/S12.10_A1.1_T3.js", false);
        }

        [Fact]
        [Trait("Category", "12.10")]
        public void TheWithStatementAddsAComputedObjectToTheFrontOfTheScopeChainOfTheCurrentExecutionContext19()
        {
			RunTest(@"TestCases/ch12/12.10/S12.10_A1.2_T1.js", false);
        }

        [Fact]
        [Trait("Category", "12.10")]
        public void TheWithStatementAddsAComputedObjectToTheFrontOfTheScopeChainOfTheCurrentExecutionContext20()
        {
			RunTest(@"TestCases/ch12/12.10/S12.10_A1.2_T2.js", false);
        }

        [Fact]
        [Trait("Category", "12.10")]
        public void TheWithStatementAddsAComputedObjectToTheFrontOfTheScopeChainOfTheCurrentExecutionContext21()
        {
			RunTest(@"TestCases/ch12/12.10/S12.10_A1.2_T3.js", false);
        }

        [Fact]
        [Trait("Category", "12.10")]
        public void TheWithStatementAddsAComputedObjectToTheFrontOfTheScopeChainOfTheCurrentExecutionContext22()
        {
			RunTest(@"TestCases/ch12/12.10/S12.10_A1.2_T4.js", false);
        }

        [Fact]
        [Trait("Category", "12.10")]
        public void TheWithStatementAddsAComputedObjectToTheFrontOfTheScopeChainOfTheCurrentExecutionContext23()
        {
			RunTest(@"TestCases/ch12/12.10/S12.10_A1.2_T5.js", false);
        }

        [Fact]
        [Trait("Category", "12.10")]
        public void TheWithStatementAddsAComputedObjectToTheFrontOfTheScopeChainOfTheCurrentExecutionContext24()
        {
			RunTest(@"TestCases/ch12/12.10/S12.10_A1.3_T1.js", false);
        }

        [Fact]
        [Trait("Category", "12.10")]
        public void TheWithStatementAddsAComputedObjectToTheFrontOfTheScopeChainOfTheCurrentExecutionContext25()
        {
			RunTest(@"TestCases/ch12/12.10/S12.10_A1.3_T2.js", false);
        }

        [Fact]
        [Trait("Category", "12.10")]
        public void TheWithStatementAddsAComputedObjectToTheFrontOfTheScopeChainOfTheCurrentExecutionContext26()
        {
			RunTest(@"TestCases/ch12/12.10/S12.10_A1.3_T3.js", false);
        }

        [Fact]
        [Trait("Category", "12.10")]
        public void TheWithStatementAddsAComputedObjectToTheFrontOfTheScopeChainOfTheCurrentExecutionContext27()
        {
			RunTest(@"TestCases/ch12/12.10/S12.10_A1.3_T4.js", false);
        }

        [Fact]
        [Trait("Category", "12.10")]
        public void TheWithStatementAddsAComputedObjectToTheFrontOfTheScopeChainOfTheCurrentExecutionContext28()
        {
			RunTest(@"TestCases/ch12/12.10/S12.10_A1.3_T5.js", false);
        }

        [Fact]
        [Trait("Category", "12.10")]
        public void TheWithStatementAddsAComputedObjectToTheFrontOfTheScopeChainOfTheCurrentExecutionContext29()
        {
			RunTest(@"TestCases/ch12/12.10/S12.10_A1.4_T1.js", false);
        }

        [Fact]
        [Trait("Category", "12.10")]
        public void TheWithStatementAddsAComputedObjectToTheFrontOfTheScopeChainOfTheCurrentExecutionContext30()
        {
			RunTest(@"TestCases/ch12/12.10/S12.10_A1.4_T2.js", false);
        }

        [Fact]
        [Trait("Category", "12.10")]
        public void TheWithStatementAddsAComputedObjectToTheFrontOfTheScopeChainOfTheCurrentExecutionContext31()
        {
			RunTest(@"TestCases/ch12/12.10/S12.10_A1.4_T3.js", false);
        }

        [Fact]
        [Trait("Category", "12.10")]
        public void TheWithStatementAddsAComputedObjectToTheFrontOfTheScopeChainOfTheCurrentExecutionContext32()
        {
			RunTest(@"TestCases/ch12/12.10/S12.10_A1.4_T4.js", false);
        }

        [Fact]
        [Trait("Category", "12.10")]
        public void TheWithStatementAddsAComputedObjectToTheFrontOfTheScopeChainOfTheCurrentExecutionContext33()
        {
			RunTest(@"TestCases/ch12/12.10/S12.10_A1.4_T5.js", false);
        }

        [Fact]
        [Trait("Category", "12.10")]
        public void TheWithStatementAddsAComputedObjectToTheFrontOfTheScopeChainOfTheCurrentExecutionContext34()
        {
			RunTest(@"TestCases/ch12/12.10/S12.10_A1.5_T1.js", false);
        }

        [Fact]
        [Trait("Category", "12.10")]
        public void TheWithStatementAddsAComputedObjectToTheFrontOfTheScopeChainOfTheCurrentExecutionContext35()
        {
			RunTest(@"TestCases/ch12/12.10/S12.10_A1.5_T2.js", false);
        }

        [Fact]
        [Trait("Category", "12.10")]
        public void TheWithStatementAddsAComputedObjectToTheFrontOfTheScopeChainOfTheCurrentExecutionContext36()
        {
			RunTest(@"TestCases/ch12/12.10/S12.10_A1.5_T3.js", false);
        }

        [Fact]
        [Trait("Category", "12.10")]
        public void TheWithStatementAddsAComputedObjectToTheFrontOfTheScopeChainOfTheCurrentExecutionContext37()
        {
			RunTest(@"TestCases/ch12/12.10/S12.10_A1.5_T4.js", false);
        }

        [Fact]
        [Trait("Category", "12.10")]
        public void TheWithStatementAddsAComputedObjectToTheFrontOfTheScopeChainOfTheCurrentExecutionContext38()
        {
			RunTest(@"TestCases/ch12/12.10/S12.10_A1.5_T5.js", false);
        }

        [Fact]
        [Trait("Category", "12.10")]
        public void TheWithStatementAddsAComputedObjectToTheFrontOfTheScopeChainOfTheCurrentExecutionContext39()
        {
			RunTest(@"TestCases/ch12/12.10/S12.10_A1.6_T1.js", false);
        }

        [Fact]
        [Trait("Category", "12.10")]
        public void TheWithStatementAddsAComputedObjectToTheFrontOfTheScopeChainOfTheCurrentExecutionContext40()
        {
			RunTest(@"TestCases/ch12/12.10/S12.10_A1.6_T2.js", false);
        }

        [Fact]
        [Trait("Category", "12.10")]
        public void TheWithStatementAddsAComputedObjectToTheFrontOfTheScopeChainOfTheCurrentExecutionContext41()
        {
			RunTest(@"TestCases/ch12/12.10/S12.10_A1.6_T3.js", false);
        }

        [Fact]
        [Trait("Category", "12.10")]
        public void TheWithStatementAddsAComputedObjectToTheFrontOfTheScopeChainOfTheCurrentExecutionContext42()
        {
			RunTest(@"TestCases/ch12/12.10/S12.10_A1.7_T1.js", false);
        }

        [Fact]
        [Trait("Category", "12.10")]
        public void TheWithStatementAddsAComputedObjectToTheFrontOfTheScopeChainOfTheCurrentExecutionContext43()
        {
			RunTest(@"TestCases/ch12/12.10/S12.10_A1.7_T2.js", false);
        }

        [Fact]
        [Trait("Category", "12.10")]
        public void TheWithStatementAddsAComputedObjectToTheFrontOfTheScopeChainOfTheCurrentExecutionContext44()
        {
			RunTest(@"TestCases/ch12/12.10/S12.10_A1.7_T3.js", false);
        }

        [Fact]
        [Trait("Category", "12.10")]
        public void TheWithStatementAddsAComputedObjectToTheFrontOfTheScopeChainOfTheCurrentExecutionContext45()
        {
			RunTest(@"TestCases/ch12/12.10/S12.10_A1.7_T4.js", false);
        }

        [Fact]
        [Trait("Category", "12.10")]
        public void TheWithStatementAddsAComputedObjectToTheFrontOfTheScopeChainOfTheCurrentExecutionContext46()
        {
			RunTest(@"TestCases/ch12/12.10/S12.10_A1.7_T5.js", false);
        }

        [Fact]
        [Trait("Category", "12.10")]
        public void TheWithStatementAddsAComputedObjectToTheFrontOfTheScopeChainOfTheCurrentExecutionContext47()
        {
			RunTest(@"TestCases/ch12/12.10/S12.10_A1.8_T1.js", false);
        }

        [Fact]
        [Trait("Category", "12.10")]
        public void TheWithStatementAddsAComputedObjectToTheFrontOfTheScopeChainOfTheCurrentExecutionContext48()
        {
			RunTest(@"TestCases/ch12/12.10/S12.10_A1.8_T2.js", false);
        }

        [Fact]
        [Trait("Category", "12.10")]
        public void TheWithStatementAddsAComputedObjectToTheFrontOfTheScopeChainOfTheCurrentExecutionContext49()
        {
			RunTest(@"TestCases/ch12/12.10/S12.10_A1.8_T3.js", false);
        }

        [Fact]
        [Trait("Category", "12.10")]
        public void TheWithStatementAddsAComputedObjectToTheFrontOfTheScopeChainOfTheCurrentExecutionContext50()
        {
			RunTest(@"TestCases/ch12/12.10/S12.10_A1.8_T4.js", false);
        }

        [Fact]
        [Trait("Category", "12.10")]
        public void TheWithStatementAddsAComputedObjectToTheFrontOfTheScopeChainOfTheCurrentExecutionContext51()
        {
			RunTest(@"TestCases/ch12/12.10/S12.10_A1.8_T5.js", false);
        }

        [Fact]
        [Trait("Category", "12.10")]
        public void TheWithStatementAddsAComputedObjectToTheFrontOfTheScopeChainOfTheCurrentExecutionContext52()
        {
			RunTest(@"TestCases/ch12/12.10/S12.10_A1.9_T1.js", false);
        }

        [Fact]
        [Trait("Category", "12.10")]
        public void TheWithStatementAddsAComputedObjectToTheFrontOfTheScopeChainOfTheCurrentExecutionContext53()
        {
			RunTest(@"TestCases/ch12/12.10/S12.10_A1.9_T2.js", false);
        }

        [Fact]
        [Trait("Category", "12.10")]
        public void TheWithStatementAddsAComputedObjectToTheFrontOfTheScopeChainOfTheCurrentExecutionContext54()
        {
			RunTest(@"TestCases/ch12/12.10/S12.10_A1.9_T3.js", false);
        }

        [Fact]
        [Trait("Category", "12.10")]
        public void NoMatterHowControlLeavesTheEmbeddedStatementTheScopeChainIsAlwaysRestoredToItsFormerState()
        {
			RunTest(@"TestCases/ch12/12.10/S12.10_A3.10_T1.js", false);
        }

        [Fact]
        [Trait("Category", "12.10")]
        public void NoMatterHowControlLeavesTheEmbeddedStatementTheScopeChainIsAlwaysRestoredToItsFormerState2()
        {
			RunTest(@"TestCases/ch12/12.10/S12.10_A3.10_T2.js", false);
        }

        [Fact]
        [Trait("Category", "12.10")]
        public void NoMatterHowControlLeavesTheEmbeddedStatementTheScopeChainIsAlwaysRestoredToItsFormerState3()
        {
			RunTest(@"TestCases/ch12/12.10/S12.10_A3.10_T3.js", false);
        }

        [Fact]
        [Trait("Category", "12.10")]
        public void NoMatterHowControlLeavesTheEmbeddedStatementTheScopeChainIsAlwaysRestoredToItsFormerState4()
        {
			RunTest(@"TestCases/ch12/12.10/S12.10_A3.10_T4.js", false);
        }

        [Fact]
        [Trait("Category", "12.10")]
        public void NoMatterHowControlLeavesTheEmbeddedStatementTheScopeChainIsAlwaysRestoredToItsFormerState5()
        {
			RunTest(@"TestCases/ch12/12.10/S12.10_A3.10_T5.js", false);
        }

        [Fact]
        [Trait("Category", "12.10")]
        public void NoMatterHowControlLeavesTheEmbeddedStatementTheScopeChainIsAlwaysRestoredToItsFormerState6()
        {
			RunTest(@"TestCases/ch12/12.10/S12.10_A3.11_T1.js", false);
        }

        [Fact]
        [Trait("Category", "12.10")]
        public void NoMatterHowControlLeavesTheEmbeddedStatementTheScopeChainIsAlwaysRestoredToItsFormerState7()
        {
			RunTest(@"TestCases/ch12/12.10/S12.10_A3.11_T2.js", false);
        }

        [Fact]
        [Trait("Category", "12.10")]
        public void NoMatterHowControlLeavesTheEmbeddedStatementTheScopeChainIsAlwaysRestoredToItsFormerState8()
        {
			RunTest(@"TestCases/ch12/12.10/S12.10_A3.11_T3.js", false);
        }

        [Fact]
        [Trait("Category", "12.10")]
        public void NoMatterHowControlLeavesTheEmbeddedStatementTheScopeChainIsAlwaysRestoredToItsFormerState9()
        {
			RunTest(@"TestCases/ch12/12.10/S12.10_A3.11_T4.js", false);
        }

        [Fact]
        [Trait("Category", "12.10")]
        public void NoMatterHowControlLeavesTheEmbeddedStatementTheScopeChainIsAlwaysRestoredToItsFormerState10()
        {
			RunTest(@"TestCases/ch12/12.10/S12.10_A3.11_T5.js", false);
        }

        [Fact]
        [Trait("Category", "12.10")]
        public void NoMatterHowControlLeavesTheEmbeddedStatementTheScopeChainIsAlwaysRestoredToItsFormerState11()
        {
			RunTest(@"TestCases/ch12/12.10/S12.10_A3.12_T1.js", false);
        }

        [Fact]
        [Trait("Category", "12.10")]
        public void NoMatterHowControlLeavesTheEmbeddedStatementTheScopeChainIsAlwaysRestoredToItsFormerState12()
        {
			RunTest(@"TestCases/ch12/12.10/S12.10_A3.12_T2.js", false);
        }

        [Fact]
        [Trait("Category", "12.10")]
        public void NoMatterHowControlLeavesTheEmbeddedStatementTheScopeChainIsAlwaysRestoredToItsFormerState13()
        {
			RunTest(@"TestCases/ch12/12.10/S12.10_A3.12_T3.js", false);
        }

        [Fact]
        [Trait("Category", "12.10")]
        public void NoMatterHowControlLeavesTheEmbeddedStatementTheScopeChainIsAlwaysRestoredToItsFormerState14()
        {
			RunTest(@"TestCases/ch12/12.10/S12.10_A3.12_T4.js", false);
        }

        [Fact]
        [Trait("Category", "12.10")]
        public void NoMatterHowControlLeavesTheEmbeddedStatementTheScopeChainIsAlwaysRestoredToItsFormerState15()
        {
			RunTest(@"TestCases/ch12/12.10/S12.10_A3.12_T5.js", false);
        }

        [Fact]
        [Trait("Category", "12.10")]
        public void NoMatterHowControlLeavesTheEmbeddedStatementTheScopeChainIsAlwaysRestoredToItsFormerState16()
        {
			RunTest(@"TestCases/ch12/12.10/S12.10_A3.1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "12.10")]
        public void NoMatterHowControlLeavesTheEmbeddedStatementTheScopeChainIsAlwaysRestoredToItsFormerState17()
        {
			RunTest(@"TestCases/ch12/12.10/S12.10_A3.1_T2.js", false);
        }

        [Fact]
        [Trait("Category", "12.10")]
        public void NoMatterHowControlLeavesTheEmbeddedStatementTheScopeChainIsAlwaysRestoredToItsFormerState18()
        {
			RunTest(@"TestCases/ch12/12.10/S12.10_A3.1_T3.js", false);
        }

        [Fact]
        [Trait("Category", "12.10")]
        public void NoMatterHowControlLeavesTheEmbeddedStatementTheScopeChainIsAlwaysRestoredToItsFormerState19()
        {
			RunTest(@"TestCases/ch12/12.10/S12.10_A3.2_T1.js", false);
        }

        [Fact]
        [Trait("Category", "12.10")]
        public void NoMatterHowControlLeavesTheEmbeddedStatementTheScopeChainIsAlwaysRestoredToItsFormerState20()
        {
			RunTest(@"TestCases/ch12/12.10/S12.10_A3.2_T2.js", false);
        }

        [Fact]
        [Trait("Category", "12.10")]
        public void NoMatterHowControlLeavesTheEmbeddedStatementTheScopeChainIsAlwaysRestoredToItsFormerState21()
        {
			RunTest(@"TestCases/ch12/12.10/S12.10_A3.2_T3.js", false);
        }

        [Fact]
        [Trait("Category", "12.10")]
        public void NoMatterHowControlLeavesTheEmbeddedStatementTheScopeChainIsAlwaysRestoredToItsFormerState22()
        {
			RunTest(@"TestCases/ch12/12.10/S12.10_A3.2_T4.js", false);
        }

        [Fact]
        [Trait("Category", "12.10")]
        public void NoMatterHowControlLeavesTheEmbeddedStatementTheScopeChainIsAlwaysRestoredToItsFormerState23()
        {
			RunTest(@"TestCases/ch12/12.10/S12.10_A3.2_T5.js", false);
        }

        [Fact]
        [Trait("Category", "12.10")]
        public void NoMatterHowControlLeavesTheEmbeddedStatementTheScopeChainIsAlwaysRestoredToItsFormerState24()
        {
			RunTest(@"TestCases/ch12/12.10/S12.10_A3.3_T1.js", false);
        }

        [Fact]
        [Trait("Category", "12.10")]
        public void NoMatterHowControlLeavesTheEmbeddedStatementTheScopeChainIsAlwaysRestoredToItsFormerState25()
        {
			RunTest(@"TestCases/ch12/12.10/S12.10_A3.3_T2.js", false);
        }

        [Fact]
        [Trait("Category", "12.10")]
        public void NoMatterHowControlLeavesTheEmbeddedStatementTheScopeChainIsAlwaysRestoredToItsFormerState26()
        {
			RunTest(@"TestCases/ch12/12.10/S12.10_A3.3_T3.js", false);
        }

        [Fact]
        [Trait("Category", "12.10")]
        public void NoMatterHowControlLeavesTheEmbeddedStatementTheScopeChainIsAlwaysRestoredToItsFormerState27()
        {
			RunTest(@"TestCases/ch12/12.10/S12.10_A3.3_T4.js", false);
        }

        [Fact]
        [Trait("Category", "12.10")]
        public void NoMatterHowControlLeavesTheEmbeddedStatementTheScopeChainIsAlwaysRestoredToItsFormerState28()
        {
			RunTest(@"TestCases/ch12/12.10/S12.10_A3.4_T1.js", false);
        }

        [Fact]
        [Trait("Category", "12.10")]
        public void NoMatterHowControlLeavesTheEmbeddedStatementTheScopeChainIsAlwaysRestoredToItsFormerState29()
        {
			RunTest(@"TestCases/ch12/12.10/S12.10_A3.4_T2.js", false);
        }

        [Fact]
        [Trait("Category", "12.10")]
        public void NoMatterHowControlLeavesTheEmbeddedStatementTheScopeChainIsAlwaysRestoredToItsFormerState30()
        {
			RunTest(@"TestCases/ch12/12.10/S12.10_A3.4_T3.js", false);
        }

        [Fact]
        [Trait("Category", "12.10")]
        public void NoMatterHowControlLeavesTheEmbeddedStatementTheScopeChainIsAlwaysRestoredToItsFormerState31()
        {
			RunTest(@"TestCases/ch12/12.10/S12.10_A3.4_T4.js", false);
        }

        [Fact]
        [Trait("Category", "12.10")]
        public void NoMatterHowControlLeavesTheEmbeddedStatementTheScopeChainIsAlwaysRestoredToItsFormerState32()
        {
			RunTest(@"TestCases/ch12/12.10/S12.10_A3.4_T5.js", false);
        }

        [Fact]
        [Trait("Category", "12.10")]
        public void NoMatterHowControlLeavesTheEmbeddedStatementTheScopeChainIsAlwaysRestoredToItsFormerState33()
        {
			RunTest(@"TestCases/ch12/12.10/S12.10_A3.5_T1.js", false);
        }

        [Fact]
        [Trait("Category", "12.10")]
        public void NoMatterHowControlLeavesTheEmbeddedStatementTheScopeChainIsAlwaysRestoredToItsFormerState34()
        {
			RunTest(@"TestCases/ch12/12.10/S12.10_A3.5_T2.js", false);
        }

        [Fact]
        [Trait("Category", "12.10")]
        public void NoMatterHowControlLeavesTheEmbeddedStatementTheScopeChainIsAlwaysRestoredToItsFormerState35()
        {
			RunTest(@"TestCases/ch12/12.10/S12.10_A3.5_T3.js", false);
        }

        [Fact]
        [Trait("Category", "12.10")]
        public void NoMatterHowControlLeavesTheEmbeddedStatementTheScopeChainIsAlwaysRestoredToItsFormerState36()
        {
			RunTest(@"TestCases/ch12/12.10/S12.10_A3.5_T4.js", false);
        }

        [Fact]
        [Trait("Category", "12.10")]
        public void NoMatterHowControlLeavesTheEmbeddedStatementTheScopeChainIsAlwaysRestoredToItsFormerState37()
        {
			RunTest(@"TestCases/ch12/12.10/S12.10_A3.5_T5.js", false);
        }

        [Fact]
        [Trait("Category", "12.10")]
        public void NoMatterHowControlLeavesTheEmbeddedStatementTheScopeChainIsAlwaysRestoredToItsFormerState38()
        {
			RunTest(@"TestCases/ch12/12.10/S12.10_A3.6_T1.js", false);
        }

        [Fact]
        [Trait("Category", "12.10")]
        public void NoMatterHowControlLeavesTheEmbeddedStatementTheScopeChainIsAlwaysRestoredToItsFormerState39()
        {
			RunTest(@"TestCases/ch12/12.10/S12.10_A3.6_T2.js", false);
        }

        [Fact]
        [Trait("Category", "12.10")]
        public void NoMatterHowControlLeavesTheEmbeddedStatementTheScopeChainIsAlwaysRestoredToItsFormerState40()
        {
			RunTest(@"TestCases/ch12/12.10/S12.10_A3.6_T3.js", false);
        }

        [Fact]
        [Trait("Category", "12.10")]
        public void NoMatterHowControlLeavesTheEmbeddedStatementTheScopeChainIsAlwaysRestoredToItsFormerState41()
        {
			RunTest(@"TestCases/ch12/12.10/S12.10_A3.7_T1.js", false);
        }

        [Fact]
        [Trait("Category", "12.10")]
        public void NoMatterHowControlLeavesTheEmbeddedStatementTheScopeChainIsAlwaysRestoredToItsFormerState42()
        {
			RunTest(@"TestCases/ch12/12.10/S12.10_A3.7_T2.js", false);
        }

        [Fact]
        [Trait("Category", "12.10")]
        public void NoMatterHowControlLeavesTheEmbeddedStatementTheScopeChainIsAlwaysRestoredToItsFormerState43()
        {
			RunTest(@"TestCases/ch12/12.10/S12.10_A3.7_T3.js", false);
        }

        [Fact]
        [Trait("Category", "12.10")]
        public void NoMatterHowControlLeavesTheEmbeddedStatementTheScopeChainIsAlwaysRestoredToItsFormerState44()
        {
			RunTest(@"TestCases/ch12/12.10/S12.10_A3.7_T4.js", false);
        }

        [Fact]
        [Trait("Category", "12.10")]
        public void NoMatterHowControlLeavesTheEmbeddedStatementTheScopeChainIsAlwaysRestoredToItsFormerState45()
        {
			RunTest(@"TestCases/ch12/12.10/S12.10_A3.7_T5.js", false);
        }

        [Fact]
        [Trait("Category", "12.10")]
        public void NoMatterHowControlLeavesTheEmbeddedStatementTheScopeChainIsAlwaysRestoredToItsFormerState46()
        {
			RunTest(@"TestCases/ch12/12.10/S12.10_A3.8_T1.js", false);
        }

        [Fact]
        [Trait("Category", "12.10")]
        public void NoMatterHowControlLeavesTheEmbeddedStatementTheScopeChainIsAlwaysRestoredToItsFormerState47()
        {
			RunTest(@"TestCases/ch12/12.10/S12.10_A3.8_T2.js", false);
        }

        [Fact]
        [Trait("Category", "12.10")]
        public void NoMatterHowControlLeavesTheEmbeddedStatementTheScopeChainIsAlwaysRestoredToItsFormerState48()
        {
			RunTest(@"TestCases/ch12/12.10/S12.10_A3.8_T3.js", false);
        }

        [Fact]
        [Trait("Category", "12.10")]
        public void NoMatterHowControlLeavesTheEmbeddedStatementTheScopeChainIsAlwaysRestoredToItsFormerState49()
        {
			RunTest(@"TestCases/ch12/12.10/S12.10_A3.8_T4.js", false);
        }

        [Fact]
        [Trait("Category", "12.10")]
        public void NoMatterHowControlLeavesTheEmbeddedStatementTheScopeChainIsAlwaysRestoredToItsFormerState50()
        {
			RunTest(@"TestCases/ch12/12.10/S12.10_A3.8_T5.js", false);
        }

        [Fact]
        [Trait("Category", "12.10")]
        public void NoMatterHowControlLeavesTheEmbeddedStatementTheScopeChainIsAlwaysRestoredToItsFormerState51()
        {
			RunTest(@"TestCases/ch12/12.10/S12.10_A3.9_T1.js", false);
        }

        [Fact]
        [Trait("Category", "12.10")]
        public void NoMatterHowControlLeavesTheEmbeddedStatementTheScopeChainIsAlwaysRestoredToItsFormerState52()
        {
			RunTest(@"TestCases/ch12/12.10/S12.10_A3.9_T2.js", false);
        }

        [Fact]
        [Trait("Category", "12.10")]
        public void NoMatterHowControlLeavesTheEmbeddedStatementTheScopeChainIsAlwaysRestoredToItsFormerState53()
        {
			RunTest(@"TestCases/ch12/12.10/S12.10_A3.9_T3.js", false);
        }

        [Fact]
        [Trait("Category", "12.10")]
        public void ChangingPropertyUsingEvalStatementContainingWithStatement()
        {
			RunTest(@"TestCases/ch12/12.10/S12.10_A4_T1.js", false);
        }

        [Fact]
        [Trait("Category", "12.10")]
        public void ChangingPropertyUsingEvalStatementContainingWithStatement2()
        {
			RunTest(@"TestCases/ch12/12.10/S12.10_A4_T2.js", false);
        }

        [Fact]
        [Trait("Category", "12.10")]
        public void ChangingPropertyUsingEvalStatementContainingWithStatement3()
        {
			RunTest(@"TestCases/ch12/12.10/S12.10_A4_T3.js", false);
        }

        [Fact]
        [Trait("Category", "12.10")]
        public void ChangingPropertyUsingEvalStatementContainingWithStatement4()
        {
			RunTest(@"TestCases/ch12/12.10/S12.10_A4_T4.js", false);
        }

        [Fact]
        [Trait("Category", "12.10")]
        public void ChangingPropertyUsingEvalStatementContainingWithStatement5()
        {
			RunTest(@"TestCases/ch12/12.10/S12.10_A4_T5.js", false);
        }

        [Fact]
        [Trait("Category", "12.10")]
        public void ChangingPropertyUsingEvalStatementContainingWithStatement6()
        {
			RunTest(@"TestCases/ch12/12.10/S12.10_A4_T6.js", false);
        }

        [Fact]
        [Trait("Category", "12.10")]
        public void DeletingPropertyUsingEvalStatementContainingWithStatement()
        {
			RunTest(@"TestCases/ch12/12.10/S12.10_A5_T1.js", false);
        }

        [Fact]
        [Trait("Category", "12.10")]
        public void DeletingPropertyUsingEvalStatementContainingWithStatement2()
        {
			RunTest(@"TestCases/ch12/12.10/S12.10_A5_T2.js", false);
        }

        [Fact]
        [Trait("Category", "12.10")]
        public void DeletingPropertyUsingEvalStatementContainingWithStatement3()
        {
			RunTest(@"TestCases/ch12/12.10/S12.10_A5_T3.js", false);
        }

        [Fact]
        [Trait("Category", "12.10")]
        public void DeletingPropertyUsingEvalStatementContainingWithStatement4()
        {
			RunTest(@"TestCases/ch12/12.10/S12.10_A5_T4.js", false);
        }

        [Fact]
        [Trait("Category", "12.10")]
        public void DeletingPropertyUsingEvalStatementContainingWithStatement5()
        {
			RunTest(@"TestCases/ch12/12.10/S12.10_A5_T5.js", false);
        }

        [Fact]
        [Trait("Category", "12.10")]
        public void DeletingPropertyUsingEvalStatementContainingWithStatement6()
        {
			RunTest(@"TestCases/ch12/12.10/S12.10_A5_T6.js", false);
        }


    }
}
