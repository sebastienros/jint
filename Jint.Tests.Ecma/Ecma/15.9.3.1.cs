using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_9_3_1 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.9.3.1")]
        public void WhenDateIsCalledAsPartOfANewExpressionItIsAConstructorItInitializesTheNewlyCreatedObject()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.3/S15.9.3.1_A1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.3.1")]
        public void WhenDateIsCalledAsPartOfANewExpressionItIsAConstructorItInitializesTheNewlyCreatedObject2()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.3/S15.9.3.1_A1_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.3.1")]
        public void WhenDateIsCalledAsPartOfANewExpressionItIsAConstructorItInitializesTheNewlyCreatedObject3()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.3/S15.9.3.1_A1_T3.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.3.1")]
        public void WhenDateIsCalledAsPartOfANewExpressionItIsAConstructorItInitializesTheNewlyCreatedObject4()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.3/S15.9.3.1_A1_T4.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.3.1")]
        public void WhenDateIsCalledAsPartOfANewExpressionItIsAConstructorItInitializesTheNewlyCreatedObject5()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.3/S15.9.3.1_A1_T5.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.3.1")]
        public void WhenDateIsCalledAsPartOfANewExpressionItIsAConstructorItInitializesTheNewlyCreatedObject6()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.3/S15.9.3.1_A1_T6.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.3.1")]
        public void ThePrototypePropertyOfTheNewlyConstructedObjectIsSetToTheOriginalDatePrototypeObjectTheOneThatIsTheInitialValueOfDatePrototype()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.3/S15.9.3.1_A2_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.3.1")]
        public void ThePrototypePropertyOfTheNewlyConstructedObjectIsSetToTheOriginalDatePrototypeObjectTheOneThatIsTheInitialValueOfDatePrototype2()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.3/S15.9.3.1_A2_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.3.1")]
        public void ThePrototypePropertyOfTheNewlyConstructedObjectIsSetToTheOriginalDatePrototypeObjectTheOneThatIsTheInitialValueOfDatePrototype3()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.3/S15.9.3.1_A2_T3.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.3.1")]
        public void ThePrototypePropertyOfTheNewlyConstructedObjectIsSetToTheOriginalDatePrototypeObjectTheOneThatIsTheInitialValueOfDatePrototype4()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.3/S15.9.3.1_A2_T4.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.3.1")]
        public void ThePrototypePropertyOfTheNewlyConstructedObjectIsSetToTheOriginalDatePrototypeObjectTheOneThatIsTheInitialValueOfDatePrototype5()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.3/S15.9.3.1_A2_T5.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.3.1")]
        public void ThePrototypePropertyOfTheNewlyConstructedObjectIsSetToTheOriginalDatePrototypeObjectTheOneThatIsTheInitialValueOfDatePrototype6()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.3/S15.9.3.1_A2_T6.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.3.1")]
        public void TheClassPropertyOfTheNewlyConstructedObjectIsSetToDate()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.3/S15.9.3.1_A3_T1.1.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.3.1")]
        public void TheClassPropertyOfTheNewlyConstructedObjectIsSetToDate2()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.3/S15.9.3.1_A3_T1.2.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.3.1")]
        public void TheClassPropertyOfTheNewlyConstructedObjectIsSetToDate3()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.3/S15.9.3.1_A3_T2.1.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.3.1")]
        public void TheClassPropertyOfTheNewlyConstructedObjectIsSetToDate4()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.3/S15.9.3.1_A3_T2.2.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.3.1")]
        public void TheClassPropertyOfTheNewlyConstructedObjectIsSetToDate5()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.3/S15.9.3.1_A3_T3.1.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.3.1")]
        public void TheClassPropertyOfTheNewlyConstructedObjectIsSetToDate6()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.3/S15.9.3.1_A3_T3.2.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.3.1")]
        public void TheClassPropertyOfTheNewlyConstructedObjectIsSetToDate7()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.3/S15.9.3.1_A3_T4.1.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.3.1")]
        public void TheClassPropertyOfTheNewlyConstructedObjectIsSetToDate8()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.3/S15.9.3.1_A3_T4.2.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.3.1")]
        public void TheClassPropertyOfTheNewlyConstructedObjectIsSetToDate9()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.3/S15.9.3.1_A3_T5.1.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.3.1")]
        public void TheClassPropertyOfTheNewlyConstructedObjectIsSetToDate10()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.3/S15.9.3.1_A3_T5.2.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.3.1")]
        public void TheClassPropertyOfTheNewlyConstructedObjectIsSetToDate11()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.3/S15.9.3.1_A3_T6.1.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.3.1")]
        public void TheClassPropertyOfTheNewlyConstructedObjectIsSetToDate12()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.3/S15.9.3.1_A3_T6.2.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.3.1")]
        public void TheValuePropertyOfTheNewlyConstructedObjectIsSetByFollowingSteps1CallTonumberYear2CallTonumberMonth3IfDateIsSuppliedUseTonumberDate4IfHoursIsSuppliedUseTonumberHours5IfMinutesIsSuppliedUseTonumberMinutes6IfSecondsIsSuppliedUseTonumberSeconds7IfMsIsSuppliedUseTonumberMs()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.3/S15.9.3.1_A4_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.3.1")]
        public void TheValuePropertyOfTheNewlyConstructedObjectIsSetByFollowingSteps1CallTonumberYear2CallTonumberMonth3IfDateIsSuppliedUseTonumberDate4IfHoursIsSuppliedUseTonumberHours5IfMinutesIsSuppliedUseTonumberMinutes6IfSecondsIsSuppliedUseTonumberSeconds7IfMsIsSuppliedUseTonumberMs2()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.3/S15.9.3.1_A4_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.3.1")]
        public void TheValuePropertyOfTheNewlyConstructedObjectIsSetByFollowingSteps1CallTonumberYear2CallTonumberMonth3IfDateIsSuppliedUseTonumberDate4IfHoursIsSuppliedUseTonumberHours5IfMinutesIsSuppliedUseTonumberMinutes6IfSecondsIsSuppliedUseTonumberSeconds7IfMsIsSuppliedUseTonumberMs3()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.3/S15.9.3.1_A4_T3.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.3.1")]
        public void TheValuePropertyOfTheNewlyConstructedObjectIsSetByFollowingSteps1CallTonumberYear2CallTonumberMonth3IfDateIsSuppliedUseTonumberDate4IfHoursIsSuppliedUseTonumberHours5IfMinutesIsSuppliedUseTonumberMinutes6IfSecondsIsSuppliedUseTonumberSeconds7IfMsIsSuppliedUseTonumberMs4()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.3/S15.9.3.1_A4_T4.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.3.1")]
        public void TheValuePropertyOfTheNewlyConstructedObjectIsSetByFollowingSteps1CallTonumberYear2CallTonumberMonth3IfDateIsSuppliedUseTonumberDate4IfHoursIsSuppliedUseTonumberHours5IfMinutesIsSuppliedUseTonumberMinutes6IfSecondsIsSuppliedUseTonumberSeconds7IfMsIsSuppliedUseTonumberMs5()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.3/S15.9.3.1_A4_T5.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.3.1")]
        public void TheValuePropertyOfTheNewlyConstructedObjectIsSetByFollowingSteps1CallTonumberYear2CallTonumberMonth3IfDateIsSuppliedUseTonumberDate4IfHoursIsSuppliedUseTonumberHours5IfMinutesIsSuppliedUseTonumberMinutes6IfSecondsIsSuppliedUseTonumberSeconds7IfMsIsSuppliedUseTonumberMs6()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.3/S15.9.3.1_A4_T6.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.3.1")]
        public void TheValuePropertyOfTheNewlyConstructedObjectIsSetByFollowingSteps8IfResult1IsNotNanAnd0TointegerResult199Result8Is1900TointegerResult1OtherwiseResult8IsResult19ComputeMakedayResult8Result2Result310ComputeMaketimeResult4Result5Result6Result711ComputeMakedateResult9Result1012SetTheValuePropertyOfTheNewlyConstructedObjectToTimeclipUtcResult11()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.3/S15.9.3.1_A5_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.3.1")]
        public void TheValuePropertyOfTheNewlyConstructedObjectIsSetByFollowingSteps8IfResult1IsNotNanAnd0TointegerResult199Result8Is1900TointegerResult1OtherwiseResult8IsResult19ComputeMakedayResult8Result2Result310ComputeMaketimeResult4Result5Result6Result711ComputeMakedateResult9Result1012SetTheValuePropertyOfTheNewlyConstructedObjectToTimeclipUtcResult112()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.3/S15.9.3.1_A5_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.3.1")]
        public void TheValuePropertyOfTheNewlyConstructedObjectIsSetByFollowingSteps8IfResult1IsNotNanAnd0TointegerResult199Result8Is1900TointegerResult1OtherwiseResult8IsResult19ComputeMakedayResult8Result2Result310ComputeMaketimeResult4Result5Result6Result711ComputeMakedateResult9Result1012SetTheValuePropertyOfTheNewlyConstructedObjectToTimeclipUtcResult113()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.3/S15.9.3.1_A5_T3.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.3.1")]
        public void TheValuePropertyOfTheNewlyConstructedObjectIsSetByFollowingSteps8IfResult1IsNotNanAnd0TointegerResult199Result8Is1900TointegerResult1OtherwiseResult8IsResult19ComputeMakedayResult8Result2Result310ComputeMaketimeResult4Result5Result6Result711ComputeMakedateResult9Result1012SetTheValuePropertyOfTheNewlyConstructedObjectToTimeclipUtcResult114()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.3/S15.9.3.1_A5_T4.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.3.1")]
        public void TheValuePropertyOfTheNewlyConstructedObjectIsSetByFollowingSteps8IfResult1IsNotNanAnd0TointegerResult199Result8Is1900TointegerResult1OtherwiseResult8IsResult19ComputeMakedayResult8Result2Result310ComputeMaketimeResult4Result5Result6Result711ComputeMakedateResult9Result1012SetTheValuePropertyOfTheNewlyConstructedObjectToTimeclipUtcResult115()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.3/S15.9.3.1_A5_T5.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.3.1")]
        public void TheValuePropertyOfTheNewlyConstructedObjectIsSetByFollowingSteps8IfResult1IsNotNanAnd0TointegerResult199Result8Is1900TointegerResult1OtherwiseResult8IsResult19ComputeMakedayResult8Result2Result310ComputeMaketimeResult4Result5Result6Result711ComputeMakedateResult9Result1012SetTheValuePropertyOfTheNewlyConstructedObjectToTimeclipUtcResult116()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.3/S15.9.3.1_A5_T6.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.3.1")]
        public void TheValuePropertyOfTheNewlyConstructedObjectWithSuppliedUndefinedArgumentShouldBeNan()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.3/S15.9.3.1_A6_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.3.1")]
        public void TheValuePropertyOfTheNewlyConstructedObjectWithSuppliedUndefinedArgumentShouldBeNan2()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.3/S15.9.3.1_A6_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.3.1")]
        public void TheValuePropertyOfTheNewlyConstructedObjectWithSuppliedUndefinedArgumentShouldBeNan3()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.3/S15.9.3.1_A6_T3.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.3.1")]
        public void TheValuePropertyOfTheNewlyConstructedObjectWithSuppliedUndefinedArgumentShouldBeNan4()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.3/S15.9.3.1_A6_T4.js", false);
        }

        [Fact]
        [Trait("Category", "15.9.3.1")]
        public void TheValuePropertyOfTheNewlyConstructedObjectWithSuppliedUndefinedArgumentShouldBeNan5()
        {
			RunTest(@"TestCases/ch15/15.9/15.9.3/S15.9.3.1_A6_T5.js", false);
        }


    }
}
