using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_10_4_2_1 : EcmaTest
    {
        [Fact]
        [Trait("Category", "10.4.2.1")]
        public void StrictModeEvalCodeCannotInstantiateVariableInTheVariableEnvironmentOfTheCallingContextThatInvokedTheEvalIfTheCodeOfTheCallingContextIsStrictCode()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.2/10.4.2.1-1gs.js", true);
        }

        [Fact]
        [Trait("Category", "10.4.2.1")]
        public void StrictModeStrictModeEvalCodeCannotInstantiateFunctionsInTheVariableEnvironmentOfTheCallerToEval()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.2/10.4.2.1-2-s.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.2.1")]
        public void StrictModeStrictModeEvalCodeCannotInstantiateFunctionsInTheVariableEnvironmentOfTheCallerToEvalWhichIsContainedInStrictModeCode()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.2/10.4.2.1-4-s.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.2.1")]
        public void StrictIndirectEvalShouldNotLeakTopLevelDeclarationsIntoTheGlobalScope()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.2/S10.4.2.1_A1.js", false);
        }


    }
}
