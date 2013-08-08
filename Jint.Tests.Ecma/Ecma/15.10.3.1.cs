using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_10_3_1 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.10.3.1")]
        public void IfPatternIsAnObjectRWhoseClassPropertyIsRegexpAndFlagsIsUndefinedThenReturnRUnchanged()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.3/S15.10.3.1_A1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.3.1")]
        public void IfPatternIsAnObjectRWhoseClassPropertyIsRegexpAndFlagsIsUndefinedThenReturnRUnchanged2()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.3/S15.10.3.1_A1_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.3.1")]
        public void IfPatternIsAnObjectRWhoseClassPropertyIsRegexpAndFlagsIsUndefinedThenReturnRUnchanged3()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.3/S15.10.3.1_A1_T3.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.3.1")]
        public void IfPatternIsAnObjectRWhoseClassPropertyIsRegexpAndFlagsIsUndefinedThenReturnRUnchanged4()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.3/S15.10.3.1_A1_T4.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.3.1")]
        public void IfPatternIsAnObjectRWhoseClassPropertyIsRegexpAndFlagsIsUndefinedThenReturnRUnchanged5()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.3/S15.10.3.1_A1_T5.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.3.1")]
        public void IfPatternIsAnObjectRWhoseClassPropertyIsRegexpAndFlagsIsDefinedThenCallTheRegexpConstructor151041PassingItThePatternAndFlagsArgumentsAndReturnTheObjectConstructedByThatConstructor()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.3/S15.10.3.1_A2_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.3.1")]
        public void IfPatternIsAnObjectRWhoseClassPropertyIsRegexpAndFlagsIsDefinedThenCallTheRegexpConstructor151041PassingItThePatternAndFlagsArgumentsAndReturnTheObjectConstructedByThatConstructor2()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.3/S15.10.3.1_A2_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.3.1")]
        public void IfPatternAndFlagsAreDefinedThenCallTheRegexpConstructor151041PassingItThePatternAndFlagsArgumentsAndReturnTheObjectConstructedByThatConstructor()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.3/S15.10.3.1_A3_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.10.3.1")]
        public void IfPatternAndFlagsAreDefinedThenCallTheRegexpConstructor151041PassingItThePatternAndFlagsArgumentsAndReturnTheObjectConstructedByThatConstructor2()
        {
			RunTest(@"TestCases/ch15/15.10/15.10.3/S15.10.3.1_A3_T2.js", false);
        }


    }
}
