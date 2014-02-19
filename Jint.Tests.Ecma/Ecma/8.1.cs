using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_8_1 : EcmaTest
    {
        [Fact]
        [Trait("Category", "8.1")]
        public void TheUndefinedTypeHasOneValueCalledUndefined()
        {
			RunTest(@"TestCases/ch08/8.1/S8.1_A1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "8.1")]
        public void TheUndefinedTypeHasOneValueCalledUndefined2()
        {
			RunTest(@"TestCases/ch08/8.1/S8.1_A1_T2.js", false);
        }

        [Fact]
        [Trait("Category", "8.1")]
        public void AnyVariableThatHasNotBeenAssignedAValueHasTheValueUndefined()
        {
			RunTest(@"TestCases/ch08/8.1/S8.1_A2_T1.js", false);
        }

        [Fact]
        [Trait("Category", "8.1")]
        public void AnyVariableThatHasNotBeenAssignedAValueHasTheValueUndefined2()
        {
			RunTest(@"TestCases/ch08/8.1/S8.1_A2_T2.js", false);
        }

        [Fact]
        [Trait("Category", "8.1")]
        public void UndefinedIsNotAKeyword()
        {
			RunTest(@"TestCases/ch08/8.1/S8.1_A3.js", false);
        }

        [Fact]
        [Trait("Category", "8.1")]
        public void IfPropertyOfObjectNotExistReturnUndefined()
        {
			RunTest(@"TestCases/ch08/8.1/S8.1_A4.js", false);
        }

        [Fact]
        [Trait("Category", "8.1")]
        public void FunctionArgumentThatIsnTProvidedHasAValueOfUndefined()
        {
			RunTest(@"TestCases/ch08/8.1/S8.1_A5.js", false);
        }


    }
}
