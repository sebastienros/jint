using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_11_1_4 : EcmaTest
    {
        [Fact]
        [Trait("Category", "11.1.4")]
        public void ElementsElidedAtTheEndOfAnArrayDoNotContributeToItsLength()
        {
			RunTest(@"TestCases/ch11/11.1/11.1.4/11.1.4-0.js", false);
        }

        [Fact]
        [Trait("Category", "11.1.4")]
        public void Refer1114TheProductionElementlistElisionoptAssignmentexpression5CallTheDefineownpropertyInternalMethodOfArrayWithArgumentsTostringFirstindexThePropertyDescriptorValueInitvalueWritableTrueEnumerableTrueConfigurableTrueAndFalse()
        {
			RunTest(@"TestCases/ch11/11.1/11.1.4/11.1.4_4-5-1.js", false);
        }

        [Fact]
        [Trait("Category", "11.1.4")]
        public void Refer1114TheProductionElementlistElementlistElisionoptAssignmentexpression6CallTheDefineownpropertyInternalMethodOfArrayWithArgumentsTostringTouint32PadLenAndThePropertyDescriptorValueInitvalueWritableTrueEnumerableTrueConfigurableTrueAndFalse()
        {
			RunTest(@"TestCases/ch11/11.1/11.1.4/11.1.4_5-6-1.js", false);
        }

        [Fact]
        [Trait("Category", "11.1.4")]
        public void EvaluateTheProductionArrayliteral()
        {
			RunTest(@"TestCases/ch11/11.1/11.1.4/S11.1.4_A1.1.js", false);
        }

        [Fact]
        [Trait("Category", "11.1.4")]
        public void EvaluateTheProductionArrayliteralElision()
        {
			RunTest(@"TestCases/ch11/11.1/11.1.4/S11.1.4_A1.2.js", false);
        }

        [Fact]
        [Trait("Category", "11.1.4")]
        public void EvaluateTheProductionArrayliteralAssignmentexpression()
        {
			RunTest(@"TestCases/ch11/11.1/11.1.4/S11.1.4_A1.3.js", false);
        }

        [Fact]
        [Trait("Category", "11.1.4")]
        public void EvaluateTheProductionArrayliteralElisionAssignmentexpression()
        {
			RunTest(@"TestCases/ch11/11.1/11.1.4/S11.1.4_A1.4.js", false);
        }

        [Fact]
        [Trait("Category", "11.1.4")]
        public void EvaluateTheProductionArrayliteralAssignmentexpressionElision()
        {
			RunTest(@"TestCases/ch11/11.1/11.1.4/S11.1.4_A1.5.js", false);
        }

        [Fact]
        [Trait("Category", "11.1.4")]
        public void EvaluateTheProductionArrayliteralElisionAssignmentexpressionElision()
        {
			RunTest(@"TestCases/ch11/11.1/11.1.4/S11.1.4_A1.6.js", false);
        }

        [Fact]
        [Trait("Category", "11.1.4")]
        public void EvaluateTheProductionArrayliteralAssignmentexpressionElisionAssignmentexpression()
        {
			RunTest(@"TestCases/ch11/11.1/11.1.4/S11.1.4_A1.7.js", false);
        }

        [Fact]
        [Trait("Category", "11.1.4")]
        public void CreateMultiDimensionalArray()
        {
			RunTest(@"TestCases/ch11/11.1/11.1.4/S11.1.4_A2.js", false);
        }


    }
}
