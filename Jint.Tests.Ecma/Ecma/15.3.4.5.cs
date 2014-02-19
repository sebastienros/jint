using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_3_4_5 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.3.4.5")]
        public void FunctionPrototypeBindMustExistAsAFunction()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.4/15.3.4.5/15.3.4.5-0-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.4.5")]
        public void FunctionPrototypeBindMustExistAsAFunctionTaking1Parameter()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.4/15.3.4.5/15.3.4.5-0-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.4.5")]
        public void FunctionPrototypeBindInternalPropertyClassOfFIsSetAsFunction()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.4/15.3.4.5/15.3.4.5-10-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.4.5")]
        public void FunctionPrototypeBindInternalPropertyPrototypeOfFIsSetAsFunctionPrototype()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.4/15.3.4.5/15.3.4.5-11-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.4.5")]
        public void FunctionPrototypeBindBoundFnHasALengthOwnProperty()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.4/15.3.4.5/15.3.4.5-13.b-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.4.5")]
        public void FunctionPrototypeBindLengthSetToRemainingNumberOfExpectedArgs()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.4/15.3.4.5/15.3.4.5-13.b-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.4.5")]
        public void FunctionPrototypeBindLengthSetToRemainingNumberOfExpectedArgsAllArgsPrefilled()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.4/15.3.4.5/15.3.4.5-13.b-3.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.4.5")]
        public void FunctionPrototypeBindLengthSetToRemainingNumberOfExpectedArgsTargetTakes0Args()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.4/15.3.4.5/15.3.4.5-13.b-4.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.4.5")]
        public void FunctionPrototypeBindLengthSetToRemainingNumberOfExpectedArgsTargetProvidedExtraArgs()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.4/15.3.4.5/15.3.4.5-13.b-5.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.4.5")]
        public void FunctionPrototypeBindLengthSetToRemainingNumberOfExpectedArgs2()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.4/15.3.4.5/15.3.4.5-13.b-6.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.4.5")]
        public void FunctionPrototypeBindLengthIsADataValuedOwnProperty()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.4/15.3.4.5/15.3.4.5-15-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.4.5")]
        public void FunctionPrototypeBindLengthIsADataValuedOwnPropertyWithDefaultAttributesFalse()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.4/15.3.4.5/15.3.4.5-15-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.4.5")]
        public void FunctionPrototypeBindTheWritableAttributeOfLengthPropertyInFSetAsFalse()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.4/15.3.4.5/15.3.4.5-15-3.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.4.5")]
        public void FunctionPrototypeBindTheEnumerableAttributeOfLengthPropertyInFSetAsFalse()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.4/15.3.4.5/15.3.4.5-15-4.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.4.5")]
        public void FunctionPrototypeBindTheConfigurableAttributeOfLengthPropertyInFSetAsFalse()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.4/15.3.4.5/15.3.4.5-15-5.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.4.5")]
        public void FunctionPrototypeBindExtensibleOfTheBoundFnIsTrue()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.4/15.3.4.5/15.3.4.5-16-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.4.5")]
        public void FunctionPrototypeBindTheExtensibleAttributeOfInternalPropertyInFSetAsTrue()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.4/15.3.4.5/15.3.4.5-16-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.4.5")]
        public void Step2SpecifiesThatATypeerrorMustBeThrownIfTheTargetIsNotCallable()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.4/15.3.4.5/15.3.4.5-2-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.4.5")]
        public void FunctionPrototypeBindThrowsTypeerrorIfTargetIsUndefined()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.4/15.3.4.5/15.3.4.5-2-10.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.4.5")]
        public void FunctionPrototypeBindThrowsTypeerrorIfTargetIsNull()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.4/15.3.4.5/15.3.4.5-2-11.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.4.5")]
        public void FunctionPrototypeBindThrowsTypeerrorIfTargetIsABoolean()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.4/15.3.4.5/15.3.4.5-2-12.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.4.5")]
        public void FunctionPrototypeBindThrowsTypeerrorIfTargetIsANumber()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.4/15.3.4.5/15.3.4.5-2-13.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.4.5")]
        public void FunctionPrototypeBindThrowsTypeerrorIfTargetIsAString()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.4/15.3.4.5/15.3.4.5-2-14.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.4.5")]
        public void FunctionPrototypeBindThrowsTypeerrorIfTargetIsObjectWithoutCallInternalMethod()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.4/15.3.4.5/15.3.4.5-2-15.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.4.5")]
        public void FunctionPrototypeBindTargetIsAFunction()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.4/15.3.4.5/15.3.4.5-2-16.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.4.5")]
        public void Step2SpecifiesThatATypeerrorMustBeThrownIfTheTargetIsNotCallable2()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.4/15.3.4.5/15.3.4.5-2-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.4.5")]
        public void Step2SpecifiesThatATypeerrorMustBeThrownIfTheTargetIsNotCallable3()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.4/15.3.4.5/15.3.4.5-2-3.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.4.5")]
        public void Step2SpecifiesThatATypeerrorMustBeThrownIfTheTargetIsNotCallable4()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.4/15.3.4.5/15.3.4.5-2-4.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.4.5")]
        public void Step2SpecifiesThatATypeerrorMustBeThrownIfTheTargetIsNotCallable5()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.4/15.3.4.5/15.3.4.5-2-5.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.4.5")]
        public void Step2SpecifiesThatATypeerrorMustBeThrownIfTheTargetIsNotCallable6()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.4/15.3.4.5/15.3.4.5-2-6.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.4.5")]
        public void Step2SpecifiesThatATypeerrorMustBeThrownIfTheTargetIsNotCallable7()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.4/15.3.4.5/15.3.4.5-2-7.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.4.5")]
        public void Step2SpecifiesThatATypeerrorMustBeThrownIfTheTargetIsNotCallable8()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.4/15.3.4.5/15.3.4.5-2-8.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.4.5")]
        public void FunctionPrototypeBindAllowsTargetToBeAConstructorDate()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.4/15.3.4.5/15.3.4.5-2-9.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.4.5")]
        public void FunctionPrototypeBindCallerIsDefinedAsOnePropertyOfF()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.4/15.3.4.5/15.3.4.5-20-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.4.5")]
        public void FunctionPrototypeBindGetAttributeOfCallerPropertyInFIsThrower()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.4/15.3.4.5/15.3.4.5-20-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.4.5")]
        public void FunctionPrototypeBindSetAttributeOfCallerPropertyInFIsThrower()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.4/15.3.4.5/15.3.4.5-20-3.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.4.5")]
        public void FunctionPrototypeBindTheEnumerableAttributeOfCallerPropertyInFIsFalse()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.4/15.3.4.5/15.3.4.5-20-4.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.4.5")]
        public void FunctionPrototypeBindTheConfigurableAttributeOfCallerPropertyInFIsFalse()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.4/15.3.4.5/15.3.4.5-20-5.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.4.5")]
        public void FunctionPrototypeBindArgumentsIsDefinedAsOnePropertyOfF()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.4/15.3.4.5/15.3.4.5-21-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.4.5")]
        public void FunctionPrototypeBindGetAttributeOfArgumentsPropertyInFIsThrower()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.4/15.3.4.5/15.3.4.5-21-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.4.5")]
        public void FunctionPrototypeBindSetAttributeOfArgumentsPropertyInFIsThrower()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.4/15.3.4.5/15.3.4.5-21-3.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.4.5")]
        public void FunctionPrototypeBindTheEnumerableAttributeOfArgumentsPropertyInFIsFalse()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.4/15.3.4.5/15.3.4.5-21-4.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.4.5")]
        public void FunctionPrototypeBindTheConfigurableAttributeOfArgumentsPropertyInFIsFalse()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.4/15.3.4.5/15.3.4.5-21-5.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.4.5")]
        public void FunctionPrototypeBindEachArgIsDefinedInAInListOrder()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.4/15.3.4.5/15.3.4.5-3-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.4.5")]
        public void FunctionPrototypeBindFCanGetOwnDataProperty()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.4/15.3.4.5/15.3.4.5-6-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.4.5")]
        public void FunctionPrototypeBindFCanGetOwnAccessorPropertyWithoutAGetFunctionThatOverridesAnInheritedAccessorProperty()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.4/15.3.4.5/15.3.4.5-6-10.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.4.5")]
        public void FunctionPrototypeBindFCanGetInheritedAccessorPropertyWithoutAGetFunction()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.4/15.3.4.5/15.3.4.5-6-11.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.4.5")]
        public void FunctionPrototypeBindFCannotGetPropertyWhichDoesnTExist()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.4/15.3.4.5/15.3.4.5-6-12.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.4.5")]
        public void FunctionPrototypeBindFCanGetInheritedDataProperty()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.4/15.3.4.5/15.3.4.5-6-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.4.5")]
        public void FunctionPrototypeBindFCanGetOwnDataPropertyThatOverridesAnInheritedDataProperty()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.4/15.3.4.5/15.3.4.5-6-3.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.4.5")]
        public void FunctionPrototypeBindFCanGetOwnDataPropertyThatOverridesAnInheritedAccessorProperty()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.4/15.3.4.5/15.3.4.5-6-4.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.4.5")]
        public void FunctionPrototypeBindFCanGetOwnAccessorProperty()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.4/15.3.4.5/15.3.4.5-6-5.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.4.5")]
        public void FunctionPrototypeBindFCanGetInheritedAccessorProperty()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.4/15.3.4.5/15.3.4.5-6-6.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.4.5")]
        public void FunctionPrototypeBindFCanGetOwnAccessorPropertyThatOverridesAnInheritedDataProperty()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.4/15.3.4.5/15.3.4.5-6-7.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.4.5")]
        public void FunctionPrototypeBindFCanGetOwnAccessorPropertyThatOverridesAnInheritedAccessorProperty()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.4/15.3.4.5/15.3.4.5-6-8.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.4.5")]
        public void FunctionPrototypeBindFCanGetOwnAccessorPropertyWithoutAGetFunction()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.4/15.3.4.5/15.3.4.5-6-9.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.4.5")]
        public void FunctionPrototypeBindTypeOfBoundFunctionMustBeFunction()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.4/15.3.4.5/15.3.4.5-8-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.4.5")]
        public void FunctionPrototypeBindClassOfBoundFunctionMustBeFunction()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.4/15.3.4.5/15.3.4.5-8-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.4.5")]
        public void FunctionPrototypeBindPrototypeIsFunctionPrototype()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.4/15.3.4.5/15.3.4.5-9-1.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.4.5")]
        public void FunctionPrototypeBindPrototypeIsFunctionPrototypeUsingGetprototypeof()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.4/15.3.4.5/15.3.4.5-9-2.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.4.5")]
        public void CallerOfBoundFunctionIsPoisonedStep20()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.4/15.3.4.5/S15.3.4.5_A1.js", true);
        }

        [Fact]
        [Trait("Category", "15.3.4.5")]
        public void IfIscallableFuncIsFalseThenThrowATypeerrorException()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.4/15.3.4.5/S15.3.4.5_A13.js", true);
        }

        [Fact]
        [Trait("Category", "15.3.4.5")]
        public void IfIscallableFuncIsFalseThenThrowATypeerrorException2()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.4/15.3.4.5/S15.3.4.5_A14.js", true);
        }

        [Fact]
        [Trait("Category", "15.3.4.5")]
        public void IfIscallableFuncIsFalseThenThrowATypeerrorException3()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.4/15.3.4.5/S15.3.4.5_A15.js", true);
        }

        [Fact]
        [Trait("Category", "15.3.4.5")]
        public void IfIscallableFuncIsFalseThenThrowATypeerrorException4()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.4/15.3.4.5/S15.3.4.5_A16.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.4.5")]
        public void ArgumentsOfBoundFunctionIsPoisonedStep21()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.4/15.3.4.5/S15.3.4.5_A2.js", true);
        }

        [Fact]
        [Trait("Category", "15.3.4.5")]
        public void FunctionPrototypeBindMustExist()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.4/15.3.4.5/S15.3.4.5_A3.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.4.5")]
        public void FunctionPrototypeBindCallTheOriginalSInternalCallMethodRatherThanItsApplyMethod()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.4/15.3.4.5/S15.3.4.5_A4.js", false);
        }

        [Fact]
        [Trait("Category", "15.3.4.5")]
        public void FunctionPrototypeBindMustCurryConstructAsWellAsCall()
        {
			RunTest(@"TestCases/ch15/15.3/15.3.4/15.3.4.5/S15.3.4.5_A5.js", false);
        }


    }
}
