using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_10_2_2 : EcmaTest
    {
        [Fact]
        [Trait("Category", "10.2.2")]
        public void EveryExecutionContextHasAssociatedWithItAScopeChainAScopeChainIsAListOfObjectsThatAreSearchedWhenEvaluatingAnIdentifier()
        {
			RunTest(@"TestCases/ch10/10.2/10.2.2/S10.2.2_A1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "10.2.2")]
        public void EveryExecutionContextHasAssociatedWithItAScopeChainAScopeChainIsAListOfObjectsThatAreSearchedWhenEvaluatingAnIdentifier2()
        {
			RunTest(@"TestCases/ch10/10.2/10.2.2/S10.2.2_A1_T2.js", false);
        }

        [Fact]
        [Trait("Category", "10.2.2")]
        public void EveryExecutionContextHasAssociatedWithItAScopeChainAScopeChainIsAListOfObjectsThatAreSearchedWhenEvaluatingAnIdentifier3()
        {
			RunTest(@"TestCases/ch10/10.2/10.2.2/S10.2.2_A1_T3.js", false);
        }

        [Fact]
        [Trait("Category", "10.2.2")]
        public void EveryExecutionContextHasAssociatedWithItAScopeChainAScopeChainIsAListOfObjectsThatAreSearchedWhenEvaluatingAnIdentifier4()
        {
			RunTest(@"TestCases/ch10/10.2/10.2.2/S10.2.2_A1_T4.js", false);
        }

        [Fact]
        [Trait("Category", "10.2.2")]
        public void EveryExecutionContextHasAssociatedWithItAScopeChainAScopeChainIsAListOfObjectsThatAreSearchedWhenEvaluatingAnIdentifier5()
        {
			RunTest(@"TestCases/ch10/10.2/10.2.2/S10.2.2_A1_T5.js", false);
        }

        [Fact]
        [Trait("Category", "10.2.2")]
        public void EveryExecutionContextHasAssociatedWithItAScopeChainAScopeChainIsAListOfObjectsThatAreSearchedWhenEvaluatingAnIdentifier6()
        {
			RunTest(@"TestCases/ch10/10.2/10.2.2/S10.2.2_A1_T6.js", false);
        }

        [Fact]
        [Trait("Category", "10.2.2")]
        public void EveryExecutionContextHasAssociatedWithItAScopeChainAScopeChainIsAListOfObjectsThatAreSearchedWhenEvaluatingAnIdentifier7()
        {
			RunTest(@"TestCases/ch10/10.2/10.2.2/S10.2.2_A1_T7.js", false);
        }

        [Fact]
        [Trait("Category", "10.2.2")]
        public void EveryExecutionContextHasAssociatedWithItAScopeChainAScopeChainIsAListOfObjectsThatAreSearchedWhenEvaluatingAnIdentifier8()
        {
			RunTest(@"TestCases/ch10/10.2/10.2.2/S10.2.2_A1_T8.js", false);
        }

        [Fact]
        [Trait("Category", "10.2.2")]
        public void EveryExecutionContextHasAssociatedWithItAScopeChainAScopeChainIsAListOfObjectsThatAreSearchedWhenEvaluatingAnIdentifier9()
        {
			RunTest(@"TestCases/ch10/10.2/10.2.2/S10.2.2_A1_T9.js", false);
        }


    }
}
