using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_11_1_1 : EcmaTest
    {
        [Fact]
        [Trait("Category", "11.1.1")]
        public void StrictModeThisObjectAtTheGlobalScopeIsNotUndefined()
        {
			RunTest(@"TestCases/ch11/11.1/11.1.1/11.1.1-1gs.js", false);
        }

        [Fact]
        [Trait("Category", "11.1.1")]
        public void TheThisIsReservedWord()
        {
			RunTest(@"TestCases/ch11/11.1/11.1.1/S11.1.1_A1.js", true);
        }

        [Fact]
        [Trait("Category", "11.1.1")]
        public void BeingInFunctionCodeThisAndEvalThisCalledAsAFunctionsReturnTheGlobalObject()
        {
			RunTest(@"TestCases/ch11/11.1/11.1.1/S11.1.1_A3.1.js", false);
        }

        [Fact]
        [Trait("Category", "11.1.1")]
        public void BeingInFunctionCodeThisAndEvalThisCalledAsAConstructorsReturnTheObject()
        {
			RunTest(@"TestCases/ch11/11.1/11.1.1/S11.1.1_A3.2.js", false);
        }

        [Fact]
        [Trait("Category", "11.1.1")]
        public void BeingInAnonymousCodeThisAndEvalThisCalledAsAFunctionReturnTheGlobalObject()
        {
			RunTest(@"TestCases/ch11/11.1/11.1.1/S11.1.1_A4.1.js", false);
        }

        [Fact]
        [Trait("Category", "11.1.1")]
        public void BeingInAnonymousCodeThisAndEvalThisCalledAsAConstructorReturnTheObject()
        {
			RunTest(@"TestCases/ch11/11.1/11.1.1/S11.1.1_A4.2.js", false);
        }


    }
}
