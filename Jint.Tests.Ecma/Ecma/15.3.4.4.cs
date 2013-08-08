using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_3_4_4 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.3.4.4")]
        public void StrictModeThisValueIsAStringWhichCannotBeConvertedToWrapperObjectsWhenTheFunctionIsCalledWithoutAnArrayOfArguments()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.4/15.3.4.4/15.3.4.4-1-s.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.4.4")]
        public void StrictModeThisValueIsANumberWhichCannotBeConvertedToWrapperObjectsWhenTheFunctionIsCalledWithoutAnArrayArgument()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.4/15.3.4.4/15.3.4.4-2-s.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.4.4")]
        public void StrictModeThisValueIsABooleanWhichCannotBeConvertedToWrapperObjectsWhenTheFunctionIsCalledWithoutAnArrayOfArguments()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.4/15.3.4.4/15.3.4.4-3-s.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.4.4")]
        public void TheFunctionPrototypeCallLengthPropertyHasTheAttributeReadonly()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.4/15.3.4.4/S15.3.4.4_A10.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.4.4")]
        public void TheFunctionPrototypeCallLengthPropertyHasTheAttributeDontenum()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.4/15.3.4.4/S15.3.4.4_A11.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.4.4")]
        public void FunctionPrototypeCallHasNotPrototypeProperty()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.4/15.3.4.4/S15.3.4.4_A12.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.4.4")]
        public void IfIscallableFuncIsFalseThenThrowATypeerrorException()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.4/15.3.4.4/S15.3.4.4_A13.js", true);
        }

        [Fact]
        [Trait("Category", "15.3.4.4")]
        public void IfIscallableFuncIsFalseThenThrowATypeerrorException2()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.4/15.3.4.4/S15.3.4.4_A14.js", true);
        }

        [Fact]
        [Trait("Category", "15.3.4.4")]
        public void IfIscallableFuncIsFalseThenThrowATypeerrorException3()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.4/15.3.4.4/S15.3.4.4_A15.js", true);
        }

        [Fact]
        [Trait("Category", "15.3.4.4")]
        public void IfIscallableFuncIsFalseThenThrowATypeerrorException4()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.4/15.3.4.4/S15.3.4.4_A16.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.4.4")]
        public void TheCallMethodPerformsAFunctionCallUsingTheCallPropertyOfTheObjectIfTheObjectDoesNotHaveACallPropertyATypeerrorExceptionIsThrown()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.4/15.3.4.4/S15.3.4.4_A1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.4.4")]
        public void TheCallMethodPerformsAFunctionCallUsingTheCallPropertyOfTheObjectIfTheObjectDoesNotHaveACallPropertyATypeerrorExceptionIsThrown2()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.4/15.3.4.4/S15.3.4.4_A1_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.4.4")]
        public void TheLengthPropertyOfTheCallMethodIs1()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.4/15.3.4.4/S15.3.4.4_A2_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.4.4")]
        public void TheLengthPropertyOfTheCallMethodIs12()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.4/15.3.4.4/S15.3.4.4_A2_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.4.4")]
        public void IfThisargIsNullOrUndefinedTheCalledFunctionIsPassedTheGlobalObjectAsTheThisValue()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.4/15.3.4.4/S15.3.4.4_A3_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.4.4")]
        public void IfThisargIsNullOrUndefinedTheCalledFunctionIsPassedTheGlobalObjectAsTheThisValue2()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.4/15.3.4.4/S15.3.4.4_A3_T10.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.4.4")]
        public void IfThisargIsNullOrUndefinedTheCalledFunctionIsPassedTheGlobalObjectAsTheThisValue3()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.4/15.3.4.4/S15.3.4.4_A3_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.4.4")]
        public void IfThisargIsNullOrUndefinedTheCalledFunctionIsPassedTheGlobalObjectAsTheThisValue4()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.4/15.3.4.4/S15.3.4.4_A3_T3.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.4.4")]
        public void IfThisargIsNullOrUndefinedTheCalledFunctionIsPassedTheGlobalObjectAsTheThisValue5()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.4/15.3.4.4/S15.3.4.4_A3_T4.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.4.4")]
        public void IfThisargIsNullOrUndefinedTheCalledFunctionIsPassedTheGlobalObjectAsTheThisValue6()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.4/15.3.4.4/S15.3.4.4_A3_T5.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.4.4")]
        public void IfThisargIsNullOrUndefinedTheCalledFunctionIsPassedTheGlobalObjectAsTheThisValue7()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.4/15.3.4.4/S15.3.4.4_A3_T6.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.4.4")]
        public void IfThisargIsNullOrUndefinedTheCalledFunctionIsPassedTheGlobalObjectAsTheThisValue8()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.4/15.3.4.4/S15.3.4.4_A3_T7.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.4.4")]
        public void IfThisargIsNullOrUndefinedTheCalledFunctionIsPassedTheGlobalObjectAsTheThisValue9()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.4/15.3.4.4/S15.3.4.4_A3_T8.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.4.4")]
        public void IfThisargIsNullOrUndefinedTheCalledFunctionIsPassedTheGlobalObjectAsTheThisValue10()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.4/15.3.4.4/S15.3.4.4_A3_T9.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.4.4")]
        public void IfThisargIsNotNullDefinedTheCalledFunctionIsPassedToobjectThisargAsTheThisValue()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.4/15.3.4.4/S15.3.4.4_A5_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.4.4")]
        public void IfThisargIsNotNullDefinedTheCalledFunctionIsPassedToobjectThisargAsTheThisValue2()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.4/15.3.4.4/S15.3.4.4_A5_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.4.4")]
        public void IfThisargIsNotNullDefinedTheCalledFunctionIsPassedToobjectThisargAsTheThisValue3()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.4/15.3.4.4/S15.3.4.4_A5_T3.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.4.4")]
        public void IfThisargIsNotNullDefinedTheCalledFunctionIsPassedToobjectThisargAsTheThisValue4()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.4/15.3.4.4/S15.3.4.4_A5_T4.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.4.4")]
        public void IfThisargIsNotNullDefinedTheCalledFunctionIsPassedToobjectThisargAsTheThisValue5()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.4/15.3.4.4/S15.3.4.4_A5_T5.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.4.4")]
        public void IfThisargIsNotNullDefinedTheCalledFunctionIsPassedToobjectThisargAsTheThisValue6()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.4/15.3.4.4/S15.3.4.4_A5_T6.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.4.4")]
        public void IfThisargIsNotNullDefinedTheCalledFunctionIsPassedToobjectThisargAsTheThisValue7()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.4/15.3.4.4/S15.3.4.4_A5_T7.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.4.4")]
        public void IfThisargIsNotNullDefinedTheCalledFunctionIsPassedToobjectThisargAsTheThisValue8()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.4/15.3.4.4/S15.3.4.4_A5_T8.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.4.4")]
        public void TheCallMethodTakesOneOrMoreArgumentsThisargAndOptionallyArg1Arg2EtcAndPerformsAFunctionCallUsingTheCallPropertyOfTheObject()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.4/15.3.4.4/S15.3.4.4_A6_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.4.4")]
        public void TheCallMethodTakesOneOrMoreArgumentsThisargAndOptionallyArg1Arg2EtcAndPerformsAFunctionCallUsingTheCallPropertyOfTheObject2()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.4/15.3.4.4/S15.3.4.4_A6_T10.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.4.4")]
        public void TheCallMethodTakesOneOrMoreArgumentsThisargAndOptionallyArg1Arg2EtcAndPerformsAFunctionCallUsingTheCallPropertyOfTheObject3()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.4/15.3.4.4/S15.3.4.4_A6_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.4.4")]
        public void TheCallMethodTakesOneOrMoreArgumentsThisargAndOptionallyArg1Arg2EtcAndPerformsAFunctionCallUsingTheCallPropertyOfTheObject4()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.4/15.3.4.4/S15.3.4.4_A6_T3.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.4.4")]
        public void TheCallMethodTakesOneOrMoreArgumentsThisargAndOptionallyArg1Arg2EtcAndPerformsAFunctionCallUsingTheCallPropertyOfTheObject5()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.4/15.3.4.4/S15.3.4.4_A6_T4.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.4.4")]
        public void TheCallMethodTakesOneOrMoreArgumentsThisargAndOptionallyArg1Arg2EtcAndPerformsAFunctionCallUsingTheCallPropertyOfTheObject6()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.4/15.3.4.4/S15.3.4.4_A6_T5.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.4.4")]
        public void TheCallMethodTakesOneOrMoreArgumentsThisargAndOptionallyArg1Arg2EtcAndPerformsAFunctionCallUsingTheCallPropertyOfTheObject7()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.4/15.3.4.4/S15.3.4.4_A6_T6.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.4.4")]
        public void TheCallMethodTakesOneOrMoreArgumentsThisargAndOptionallyArg1Arg2EtcAndPerformsAFunctionCallUsingTheCallPropertyOfTheObject8()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.4/15.3.4.4/S15.3.4.4_A6_T7.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.4.4")]
        public void TheCallMethodTakesOneOrMoreArgumentsThisargAndOptionallyArg1Arg2EtcAndPerformsAFunctionCallUsingTheCallPropertyOfTheObject9()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.4/15.3.4.4/S15.3.4.4_A6_T8.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.4.4")]
        public void TheCallMethodTakesOneOrMoreArgumentsThisargAndOptionallyArg1Arg2EtcAndPerformsAFunctionCallUsingTheCallPropertyOfTheObject10()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.4/15.3.4.4/S15.3.4.4_A6_T9.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.4.4")]
        public void FunctionPrototypeCallCanTBeUsedAsCreateCaller()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.4/15.3.4.4/S15.3.4.4_A7_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.4.4")]
        public void FunctionPrototypeCallCanTBeUsedAsCreateCaller2()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.4/15.3.4.4/S15.3.4.4_A7_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.4.4")]
        public void FunctionPrototypeCallCanTBeUsedAsCreateCaller3()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.4/15.3.4.4/S15.3.4.4_A7_T3.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.4.4")]
        public void FunctionPrototypeCallCanTBeUsedAsCreateCaller4()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.4/15.3.4.4/S15.3.4.4_A7_T4.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.4.4")]
        public void FunctionPrototypeCallCanTBeUsedAsCreateCaller5()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.4/15.3.4.4/S15.3.4.4_A7_T5.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.4.4")]
        public void FunctionPrototypeCallCanTBeUsedAsCreateCaller6()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.4/15.3.4.4/S15.3.4.4_A7_T6.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.4.4")]
        public void TheFunctionPrototypeCallLengthPropertyHasTheAttributeDontdelete()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.4/15.3.4.4/S15.3.4.4_A9.js", false);
        }


    }
}
