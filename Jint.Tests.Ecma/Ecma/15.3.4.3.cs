using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_3_4_3 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.3.4.3")]
        public void StrictModeThisValueIsAStringWhichCannotBeConvertedToWrapperObjectsWhenTheFunctionIsCalledWithAnArrayOfArguments()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.4/15.3.4.3/15.3.4.3-1-s.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.4.3")]
        public void StrictModeThisValueIsANumberWhichCannotBeConvertedToWrapperObjectsWhenTheFunctionIsCalledWithAnArrayOfArguments()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.4/15.3.4.3/15.3.4.3-2-s.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.4.3")]
        public void StrictModeThisValueIsABooleanWhichCannotBeConvertedToWrapperObjectsWhenTheFunctionIsCalledWithAnArrayOfArguments()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.4/15.3.4.3/15.3.4.3-3-s.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.4.3")]
        public void TheFunctionPrototypeApplyLengthPropertyHasTheAttributeReadonly()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.4/15.3.4.3/S15.3.4.3_A10.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.4.3")]
        public void TheFunctionPrototypeApplyLengthPropertyHasTheAttributeDontenum()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.4/15.3.4.3/S15.3.4.3_A11.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.4.3")]
        public void FunctionPrototypeApplyHasNotPrototypeProperty()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.4/15.3.4.3/S15.3.4.3_A12.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.4.3")]
        public void IfIscallableFuncIsFalseThenThrowATypeerrorException()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.4/15.3.4.3/S15.3.4.3_A13.js", true);
        }

        [Fact]
        [Trait("Category", "15.3.4.3")]
        public void IfIscallableFuncIsFalseThenThrowATypeerrorException2()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.4/15.3.4.3/S15.3.4.3_A14.js", true);
        }

        [Fact]
        [Trait("Category", "15.3.4.3")]
        public void IfIscallableFuncIsFalseThenThrowATypeerrorException3()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.4/15.3.4.3/S15.3.4.3_A15.js", true);
        }

        [Fact]
        [Trait("Category", "15.3.4.3")]
        public void IfIscallableFuncIsFalseThenThrowATypeerrorException4()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.4/15.3.4.3/S15.3.4.3_A16.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.4.3")]
        public void TheApplyMethodPerformsAFunctionCallUsingTheCallPropertyOfTheObjectIfTheObjectDoesNotHaveACallPropertyATypeerrorExceptionIsThrown()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.4/15.3.4.3/S15.3.4.3_A1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.4.3")]
        public void TheApplyMethodPerformsAFunctionCallUsingTheCallPropertyOfTheObjectIfTheObjectDoesNotHaveACallPropertyATypeerrorExceptionIsThrown2()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.4/15.3.4.3/S15.3.4.3_A1_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.4.3")]
        public void TheLengthPropertyOfTheApplyMethodIs2()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.4/15.3.4.3/S15.3.4.3_A2_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.4.3")]
        public void TheLengthPropertyOfTheApplyMethodIs22()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.4/15.3.4.3/S15.3.4.3_A2_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.4.3")]
        public void IfThisargIsNullOrUndefinedTheCalledFunctionIsPassedTheGlobalObjectAsTheThisValue()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.4/15.3.4.3/S15.3.4.3_A3_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.4.3")]
        public void IfThisargIsNullOrUndefinedTheCalledFunctionIsPassedTheGlobalObjectAsTheThisValue2()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.4/15.3.4.3/S15.3.4.3_A3_T10.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.4.3")]
        public void IfThisargIsNullOrUndefinedTheCalledFunctionIsPassedTheGlobalObjectAsTheThisValue3()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.4/15.3.4.3/S15.3.4.3_A3_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.4.3")]
        public void IfThisargIsNullOrUndefinedTheCalledFunctionIsPassedTheGlobalObjectAsTheThisValue4()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.4/15.3.4.3/S15.3.4.3_A3_T3.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.4.3")]
        public void IfThisargIsNullOrUndefinedTheCalledFunctionIsPassedTheGlobalObjectAsTheThisValue5()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.4/15.3.4.3/S15.3.4.3_A3_T4.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.4.3")]
        public void IfThisargIsNullOrUndefinedTheCalledFunctionIsPassedTheGlobalObjectAsTheThisValue6()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.4/15.3.4.3/S15.3.4.3_A3_T5.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.4.3")]
        public void IfThisargIsNullOrUndefinedTheCalledFunctionIsPassedTheGlobalObjectAsTheThisValue7()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.4/15.3.4.3/S15.3.4.3_A3_T6.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.4.3")]
        public void IfThisargIsNullOrUndefinedTheCalledFunctionIsPassedTheGlobalObjectAsTheThisValue8()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.4/15.3.4.3/S15.3.4.3_A3_T7.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.4.3")]
        public void IfThisargIsNullOrUndefinedTheCalledFunctionIsPassedTheGlobalObjectAsTheThisValue9()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.4/15.3.4.3/S15.3.4.3_A3_T8.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.4.3")]
        public void IfThisargIsNullOrUndefinedTheCalledFunctionIsPassedTheGlobalObjectAsTheThisValue10()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.4/15.3.4.3/S15.3.4.3_A3_T9.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.4.3")]
        public void IfThisargIsNotNullDefinedTheCalledFunctionIsPassedToobjectThisargAsTheThisValue()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.4/15.3.4.3/S15.3.4.3_A5_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.4.3")]
        public void IfThisargIsNotNullDefinedTheCalledFunctionIsPassedToobjectThisargAsTheThisValue2()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.4/15.3.4.3/S15.3.4.3_A5_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.4.3")]
        public void IfThisargIsNotNullDefinedTheCalledFunctionIsPassedToobjectThisargAsTheThisValue3()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.4/15.3.4.3/S15.3.4.3_A5_T3.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.4.3")]
        public void IfThisargIsNotNullDefinedTheCalledFunctionIsPassedToobjectThisargAsTheThisValue4()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.4/15.3.4.3/S15.3.4.3_A5_T4.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.4.3")]
        public void IfThisargIsNotNullDefinedTheCalledFunctionIsPassedToobjectThisargAsTheThisValue5()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.4/15.3.4.3/S15.3.4.3_A5_T5.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.4.3")]
        public void IfThisargIsNotNullDefinedTheCalledFunctionIsPassedToobjectThisargAsTheThisValue6()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.4/15.3.4.3/S15.3.4.3_A5_T6.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.4.3")]
        public void IfThisargIsNotNullDefinedTheCalledFunctionIsPassedToobjectThisargAsTheThisValue7()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.4/15.3.4.3/S15.3.4.3_A5_T7.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.4.3")]
        public void IfThisargIsNotNullDefinedTheCalledFunctionIsPassedToobjectThisargAsTheThisValue8()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.4/15.3.4.3/S15.3.4.3_A5_T8.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.4.3")]
        public void IfArgarrayIsNeitherAnArrayNorAnArgumentsObjectSee1018ATypeerrorExceptionIsThrown()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.4/15.3.4.3/S15.3.4.3_A6_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.4.3")]
        public void IfArgarrayIsNeitherAnArrayNorAnArgumentsObjectSee1018ATypeerrorExceptionIsThrown2()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.4/15.3.4.3/S15.3.4.3_A6_T3.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.4.3")]
        public void IfArgarrayIsEitherAnArrayOrAnArgumentsObjectTheFunctionIsPassedTheTouint32ArgarrayLengthArgumentsArgarray0Argarray1ArgarrayTouint32ArgarrayLength1()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.4/15.3.4.3/S15.3.4.3_A7_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.4.3")]
        public void IfArgarrayIsEitherAnArrayOrAnArgumentsObjectTheFunctionIsPassedTheTouint32ArgarrayLengthArgumentsArgarray0Argarray1ArgarrayTouint32ArgarrayLength12()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.4/15.3.4.3/S15.3.4.3_A7_T10.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.4.3")]
        public void IfArgarrayIsEitherAnArrayOrAnArgumentsObjectTheFunctionIsPassedTheTouint32ArgarrayLengthArgumentsArgarray0Argarray1ArgarrayTouint32ArgarrayLength13()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.4/15.3.4.3/S15.3.4.3_A7_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.4.3")]
        public void IfArgarrayIsEitherAnArrayOrAnArgumentsObjectTheFunctionIsPassedTheTouint32ArgarrayLengthArgumentsArgarray0Argarray1ArgarrayTouint32ArgarrayLength14()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.4/15.3.4.3/S15.3.4.3_A7_T3.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.4.3")]
        public void IfArgarrayIsEitherAnArrayOrAnArgumentsObjectTheFunctionIsPassedTheTouint32ArgarrayLengthArgumentsArgarray0Argarray1ArgarrayTouint32ArgarrayLength15()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.4/15.3.4.3/S15.3.4.3_A7_T4.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.4.3")]
        public void IfArgarrayIsEitherAnArrayOrAnArgumentsObjectTheFunctionIsPassedTheTouint32ArgarrayLengthArgumentsArgarray0Argarray1ArgarrayTouint32ArgarrayLength16()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.4/15.3.4.3/S15.3.4.3_A7_T5.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.4.3")]
        public void IfArgarrayIsEitherAnArrayOrAnArgumentsObjectTheFunctionIsPassedTheTouint32ArgarrayLengthArgumentsArgarray0Argarray1ArgarrayTouint32ArgarrayLength17()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.4/15.3.4.3/S15.3.4.3_A7_T6.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.4.3")]
        public void IfArgarrayIsEitherAnArrayOrAnArgumentsObjectTheFunctionIsPassedTheTouint32ArgarrayLengthArgumentsArgarray0Argarray1ArgarrayTouint32ArgarrayLength18()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.4/15.3.4.3/S15.3.4.3_A7_T7.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.4.3")]
        public void IfArgarrayIsEitherAnArrayOrAnArgumentsObjectTheFunctionIsPassedTheTouint32ArgarrayLengthArgumentsArgarray0Argarray1ArgarrayTouint32ArgarrayLength19()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.4/15.3.4.3/S15.3.4.3_A7_T8.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.4.3")]
        public void IfArgarrayIsEitherAnArrayOrAnArgumentsObjectTheFunctionIsPassedTheTouint32ArgarrayLengthArgumentsArgarray0Argarray1ArgarrayTouint32ArgarrayLength110()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.4/15.3.4.3/S15.3.4.3_A7_T9.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.4.3")]
        public void FunctionPrototypeApplyCanTBeUsedAsCreateCaller()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.4/15.3.4.3/S15.3.4.3_A8_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.4.3")]
        public void FunctionPrototypeApplyCanTBeUsedAsCreateCaller2()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.4/15.3.4.3/S15.3.4.3_A8_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.4.3")]
        public void FunctionPrototypeApplyCanTBeUsedAsCreateCaller3()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.4/15.3.4.3/S15.3.4.3_A8_T3.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.4.3")]
        public void FunctionPrototypeApplyCanTBeUsedAsCreateCaller4()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.4/15.3.4.3/S15.3.4.3_A8_T4.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.4.3")]
        public void FunctionPrototypeApplyCanTBeUsedAsCreateCaller5()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.4/15.3.4.3/S15.3.4.3_A8_T5.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.4.3")]
        public void FunctionPrototypeApplyCanTBeUsedAsCreateCaller6()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.4/15.3.4.3/S15.3.4.3_A8_T6.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.4.3")]
        public void TheFunctionPrototypeApplyLengthPropertyHasTheAttributeDontdelete()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.4/15.3.4.3/S15.3.4.3_A9.js", false);
        }


    }
}
