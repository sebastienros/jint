using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_2_1_1 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.2.1.1")]
        public void WhenTheObjectValueIsCalledAndTheValueIsNullUndefinedOrNotSuppliedCreateAndReturnANewObjectObjectIfTheObjectConstructorHadBeenCalledWithTheSameArguments15221()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.1/S15.2.1.1_A1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.1.1")]
        public void WhenTheObjectValueIsCalledAndTheValueIsNullUndefinedOrNotSuppliedCreateAndReturnANewObjectObjectIfTheObjectConstructorHadBeenCalledWithTheSameArguments152212()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.1/S15.2.1.1_A1_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.1.1")]
        public void WhenTheObjectValueIsCalledAndTheValueIsNullUndefinedOrNotSuppliedCreateAndReturnANewObjectObjectIfTheObjectConstructorHadBeenCalledWithTheSameArguments152213()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.1/S15.2.1.1_A1_T3.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.1.1")]
        public void WhenTheObjectValueIsCalledAndTheValueIsNullUndefinedOrNotSuppliedCreateAndReturnANewObjectObjectIfTheObjectConstructorHadBeenCalledWithTheSameArguments152214()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.1/S15.2.1.1_A1_T4.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.1.1")]
        public void WhenTheObjectValueIsCalledAndTheValueIsNullUndefinedOrNotSuppliedCreateAndReturnANewObjectObjectIfTheObjectConstructorHadBeenCalledWithTheSameArguments152215()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.1/S15.2.1.1_A1_T5.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.1.1")]
        public void WhenTheObjectFunctionIsCalledWithOneArgumentValueAndTheValueNeitherIsNullNorUndefinedAndIsSuppliedReturnToobjectValue()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.1/S15.2.1.1_A2_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.1.1")]
        public void WhenTheObjectFunctionIsCalledWithOneArgumentValueAndTheValueNeitherIsNullNorUndefinedAndIsSuppliedReturnToobjectValue2()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.1/S15.2.1.1_A2_T10.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.1.1")]
        public void WhenTheObjectFunctionIsCalledWithOneArgumentValueAndTheValueNeitherIsNullNorUndefinedAndIsSuppliedReturnToobjectValue3()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.1/S15.2.1.1_A2_T11.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.1.1")]
        public void WhenTheObjectFunctionIsCalledWithOneArgumentValueAndTheValueNeitherIsNullNorUndefinedAndIsSuppliedReturnToobjectValue4()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.1/S15.2.1.1_A2_T12.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.1.1")]
        public void WhenTheObjectFunctionIsCalledWithOneArgumentValueAndTheValueNeitherIsNullNorUndefinedAndIsSuppliedReturnToobjectValue5()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.1/S15.2.1.1_A2_T13.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.1.1")]
        public void WhenTheObjectFunctionIsCalledWithOneArgumentValueAndTheValueNeitherIsNullNorUndefinedAndIsSuppliedReturnToobjectValue6()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.1/S15.2.1.1_A2_T14.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.1.1")]
        public void WhenTheObjectFunctionIsCalledWithOneArgumentValueAndTheValueNeitherIsNullNorUndefinedAndIsSuppliedReturnToobjectValue7()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.1/S15.2.1.1_A2_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.1.1")]
        public void WhenTheObjectFunctionIsCalledWithOneArgumentValueAndTheValueNeitherIsNullNorUndefinedAndIsSuppliedReturnToobjectValue8()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.1/S15.2.1.1_A2_T3.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.1.1")]
        public void WhenTheObjectFunctionIsCalledWithOneArgumentValueAndTheValueNeitherIsNullNorUndefinedAndIsSuppliedReturnToobjectValue9()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.1/S15.2.1.1_A2_T4.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.1.1")]
        public void WhenTheObjectFunctionIsCalledWithOneArgumentValueAndTheValueNeitherIsNullNorUndefinedAndIsSuppliedReturnToobjectValue10()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.1/S15.2.1.1_A2_T5.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.1.1")]
        public void WhenTheObjectFunctionIsCalledWithOneArgumentValueAndTheValueNeitherIsNullNorUndefinedAndIsSuppliedReturnToobjectValue11()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.1/S15.2.1.1_A2_T6.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.1.1")]
        public void WhenTheObjectFunctionIsCalledWithOneArgumentValueAndTheValueNeitherIsNullNorUndefinedAndIsSuppliedReturnToobjectValue12()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.1/S15.2.1.1_A2_T7.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.1.1")]
        public void WhenTheObjectFunctionIsCalledWithOneArgumentValueAndTheValueNeitherIsNullNorUndefinedAndIsSuppliedReturnToobjectValue13()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.1/S15.2.1.1_A2_T8.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.1.1")]
        public void WhenTheObjectFunctionIsCalledWithOneArgumentValueAndTheValueNeitherIsNullNorUndefinedAndIsSuppliedReturnToobjectValue14()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.1/S15.2.1.1_A2_T9.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.1.1")]
        public void SinceCallingObjectAsAFunctionIsIdenticalToCallingAFunctionListOfArgumentsBracketingIsAllowed()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.1/S15.2.1.1_A3_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.1.1")]
        public void SinceCallingObjectAsAFunctionIsIdenticalToCallingAFunctionListOfArgumentsBracketingIsAllowed2()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.1/S15.2.1.1_A3_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.2.1.1")]
        public void SinceCallingObjectAsAFunctionIsIdenticalToCallingAFunctionListOfArgumentsBracketingIsAllowed3()
        {
			RunTest(@"TestCases/ch15/15.2/15.2.1/S15.2.1.1_A3_T3.js", false);
        }


    }
}
