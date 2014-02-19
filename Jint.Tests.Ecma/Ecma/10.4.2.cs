using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_10_4_2 : EcmaTest
    {
        [Fact]
        [Trait("Category", "10.4.2")]
        public void IndirectCallToEvalHasContextSetToGlobalContext()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.2/10.4.2-1-1.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.2")]
        public void IndirectCallToEvalHasContextSetToGlobalContextNestedFunction()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.2/10.4.2-1-2.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.2")]
        public void IndirectCallToEvalHasContextSetToGlobalContextCatchBlock()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.2/10.4.2-1-3.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.2")]
        public void IndirectCallToEvalHasContextSetToGlobalContextWithBlock()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.2/10.4.2-1-4.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.2")]
        public void IndirectCallToEvalHasContextSetToGlobalContextInsideAnotherEval()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.2/10.4.2-1-5.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.2")]
        public void DirectValCodeInNonStrictModeCanInstantiateVariableInCallingContext()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.2/10.4.2-2-c-1.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.2")]
        public void StrictModeStrictModeEvalCodeCannotInstantiateFunctionsInTheVariableEnvironmentOfTheCallerToEval()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.2/10.4.2-2-s.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.2")]
        public void DirectEvalCodeInStrictModeCannotInstantiateVariableInTheVariableEnvironmentOfTheCallingContext()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.2/10.4.2-3-c-1-s.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.2")]
        public void CallingCodeInStrictModeEvalCannotInstantiateVariableInTheVariableEnvironmentOfTheCallingContext()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.2/10.4.2-3-c-2-s.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.2")]
        public void TheScopeChainIsInitialisedToContainTheSameObjectsInTheSameOrderAsTheCallingContextSScopeChain()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.2/S10.4.2_A1.1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.2")]
        public void TheScopeChainIsInitialisedToContainTheSameObjectsInTheSameOrderAsTheCallingContextSScopeChain2()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.2/S10.4.2_A1.1_T10.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.2")]
        public void TheScopeChainIsInitialisedToContainTheSameObjectsInTheSameOrderAsTheCallingContextSScopeChain3()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.2/S10.4.2_A1.1_T11.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.2")]
        public void TheScopeChainIsInitialisedToContainTheSameObjectsInTheSameOrderAsTheCallingContextSScopeChain4()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.2/S10.4.2_A1.1_T2.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.2")]
        public void TheScopeChainIsInitialisedToContainTheSameObjectsInTheSameOrderAsTheCallingContextSScopeChain5()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.2/S10.4.2_A1.1_T3.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.2")]
        public void TheScopeChainIsInitialisedToContainTheSameObjectsInTheSameOrderAsTheCallingContextSScopeChain6()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.2/S10.4.2_A1.1_T4.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.2")]
        public void TheScopeChainIsInitialisedToContainTheSameObjectsInTheSameOrderAsTheCallingContextSScopeChain7()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.2/S10.4.2_A1.1_T5.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.2")]
        public void TheScopeChainIsInitialisedToContainTheSameObjectsInTheSameOrderAsTheCallingContextSScopeChain8()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.2/S10.4.2_A1.1_T6.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.2")]
        public void TheScopeChainIsInitialisedToContainTheSameObjectsInTheSameOrderAsTheCallingContextSScopeChain9()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.2/S10.4.2_A1.1_T7.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.2")]
        public void TheScopeChainIsInitialisedToContainTheSameObjectsInTheSameOrderAsTheCallingContextSScopeChain10()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.2/S10.4.2_A1.1_T8.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.2")]
        public void TheScopeChainIsInitialisedToContainTheSameObjectsInTheSameOrderAsTheCallingContextSScopeChain11()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.2/S10.4.2_A1.1_T9.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.2")]
        public void TheScopeChainIsInitialisedToContainTheSameObjectsInTheSameOrderAsTheCallingContextSScopeChain12()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.2/S10.4.2_A1.2_T1.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.2")]
        public void TheScopeChainIsInitialisedToContainTheSameObjectsInTheSameOrderAsTheCallingContextSScopeChain13()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.2/S10.4.2_A1.2_T10.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.2")]
        public void TheScopeChainIsInitialisedToContainTheSameObjectsInTheSameOrderAsTheCallingContextSScopeChain14()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.2/S10.4.2_A1.2_T11.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.2")]
        public void TheScopeChainIsInitialisedToContainTheSameObjectsInTheSameOrderAsTheCallingContextSScopeChain15()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.2/S10.4.2_A1.2_T2.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.2")]
        public void TheScopeChainIsInitialisedToContainTheSameObjectsInTheSameOrderAsTheCallingContextSScopeChain16()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.2/S10.4.2_A1.2_T3.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.2")]
        public void TheScopeChainIsInitialisedToContainTheSameObjectsInTheSameOrderAsTheCallingContextSScopeChain17()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.2/S10.4.2_A1.2_T4.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.2")]
        public void TheScopeChainIsInitialisedToContainTheSameObjectsInTheSameOrderAsTheCallingContextSScopeChain18()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.2/S10.4.2_A1.2_T5.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.2")]
        public void TheScopeChainIsInitialisedToContainTheSameObjectsInTheSameOrderAsTheCallingContextSScopeChain19()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.2/S10.4.2_A1.2_T6.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.2")]
        public void TheScopeChainIsInitialisedToContainTheSameObjectsInTheSameOrderAsTheCallingContextSScopeChain20()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.2/S10.4.2_A1.2_T7.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.2")]
        public void TheScopeChainIsInitialisedToContainTheSameObjectsInTheSameOrderAsTheCallingContextSScopeChain21()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.2/S10.4.2_A1.2_T8.js", false);
        }

        [Fact]
        [Trait("Category", "10.4.2")]
        public void TheScopeChainIsInitialisedToContainTheSameObjectsInTheSameOrderAsTheCallingContextSScopeChain22()
        {
			RunTest(@"TestCases/ch10/10.4/10.4.2/S10.4.2_A1.2_T9.js", false);
        }


    }
}
