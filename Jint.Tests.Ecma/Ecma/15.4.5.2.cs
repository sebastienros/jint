using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_4_5_2 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.4.5.2")]
        public void EveryArrayObjectHasALengthPropertyWhoseValueIsAlwaysANonnegativeIntegerLessThan232TheValueOfTheLengthPropertyIsNumericallyGreaterThanTheNameOfEveryPropertyWhoseNameIsAnArrayIndex()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.5/15.4.5.2/S15.4.5.2_A1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.5.2")]
        public void EveryArrayObjectHasALengthPropertyWhoseValueIsAlwaysANonnegativeIntegerLessThan232TheValueOfTheLengthPropertyIsNumericallyGreaterThanTheNameOfEveryPropertyWhoseNameIsAnArrayIndex2()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.5/15.4.5.2/S15.4.5.2_A1_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.5.2")]
        public void IfAPropertyIsAddedWhoseNameIsAnArrayIndexTheLengthPropertyIsChanged()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.5/15.4.5.2/S15.4.5.2_A2_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.5.2")]
        public void IfTheLengthPropertyIsChangedEveryPropertyWhoseNameIsAnArrayIndexWhoseValueIsNotSmallerThanTheNewLengthIsAutomaticallyDeleted()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.5/15.4.5.2/S15.4.5.2_A3_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.5.2")]
        public void IfTheLengthPropertyIsChangedEveryPropertyWhoseNameIsAnArrayIndexWhoseValueIsNotSmallerThanTheNewLengthIsAutomaticallyDeleted2()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.5/15.4.5.2/S15.4.5.2_A3_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.5.2")]
        public void IfTheLengthPropertyIsChangedEveryPropertyWhoseNameIsAnArrayIndexWhoseValueIsNotSmallerThanTheNewLengthIsAutomaticallyDeleted3()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.5/15.4.5.2/S15.4.5.2_A3_T3.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.5.2")]
        public void IfTheLengthPropertyIsChangedEveryPropertyWhoseNameIsAnArrayIndexWhoseValueIsNotSmallerThanTheNewLengthIsAutomaticallyDeleted4()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.5/15.4.5.2/S15.4.5.2_A3_T4.js", false);
        }


    }
}
