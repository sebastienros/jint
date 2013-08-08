using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_11_2_4 : EcmaTest
    {
        [Fact]
        [Trait("Category", "11.2.4")]
        public void Arguments()
        {
			RunTest(@"TestCases/ch11/11.2/11.2.4/S11.2.4_A1.1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "11.2.4")]
        public void Arguments2()
        {
			RunTest(@"TestCases/ch11/11.2/11.2.4/S11.2.4_A1.1_T2.js", false);
        }

        [Fact]
        [Trait("Category", "11.2.4")]
        public void ArgumentsArgumentlist()
        {
			RunTest(@"TestCases/ch11/11.2/11.2.4/S11.2.4_A1.2_T1.js", false);
        }

        [Fact]
        [Trait("Category", "11.2.4")]
        public void ArgumentsArgumentlist2()
        {
			RunTest(@"TestCases/ch11/11.2/11.2.4/S11.2.4_A1.2_T2.js", false);
        }

        [Fact]
        [Trait("Category", "11.2.4")]
        public void ArgumentsArgumentlistArgumentlistAssignmentexpressionIsABadSyntax()
        {
			RunTest(@"TestCases/ch11/11.2/11.2.4/S11.2.4_A1.3_T1.js", true);
        }

        [Fact]
        [Trait("Category", "11.2.4")]
        public void ArgumentsArgumentlistArgumentlistAssignmentexpression()
        {
			RunTest(@"TestCases/ch11/11.2/11.2.4/S11.2.4_A1.4_T1.js", false);
        }

        [Fact]
        [Trait("Category", "11.2.4")]
        public void ArgumentsArgumentlistArgumentlistAssignmentexpression2()
        {
			RunTest(@"TestCases/ch11/11.2/11.2.4/S11.2.4_A1.4_T2.js", false);
        }

        [Fact]
        [Trait("Category", "11.2.4")]
        public void ArgumentsArgumentlistArgumentlistAssignmentexpression3()
        {
			RunTest(@"TestCases/ch11/11.2/11.2.4/S11.2.4_A1.4_T3.js", false);
        }

        [Fact]
        [Trait("Category", "11.2.4")]
        public void ArgumentsArgumentlistArgumentlistAssignmentexpression4()
        {
			RunTest(@"TestCases/ch11/11.2/11.2.4/S11.2.4_A1.4_T4.js", false);
        }


    }
}
