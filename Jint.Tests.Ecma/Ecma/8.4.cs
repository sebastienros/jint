using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_8_4 : EcmaTest
    {
        [Fact]
        [Trait("Category", "8.4")]
        public void AnyVariableThatHasBeenAssignedWithStringLiteralHasTheTypeString()
        {
			RunTest(@"TestCases/ch08/8.4/S8.4_A1.js", false);
        }

        [Fact]
        [Trait("Category", "8.4")]
        public void BothUnicodeAndAsciiCharsAreAllowed()
        {
			RunTest(@"TestCases/ch08/8.4/S8.4_A10.js", false);
        }

        [Fact]
        [Trait("Category", "8.4")]
        public void PresenceOfReservedWordsInStringLiteralAreAllowed()
        {
			RunTest(@"TestCases/ch08/8.4/S8.4_A11.js", false);
        }

        [Fact]
        [Trait("Category", "8.4")]
        public void AssignmentToStringLiteralCallsStringConstructor()
        {
			RunTest(@"TestCases/ch08/8.4/S8.4_A12.js", false);
        }

        [Fact]
        [Trait("Category", "8.4")]
        public void WhenAppearsNotClosedSingleQuoteProgramFailes()
        {
			RunTest(@"TestCases/ch08/8.4/S8.4_A13_T1.js", true);
        }

        [Fact]
        [Trait("Category", "8.4")]
        public void WhenAppearsNotClosedSingleQuoteProgramFailes2()
        {
			RunTest(@"TestCases/ch08/8.4/S8.4_A13_T2.js", true);
        }

        [Fact]
        [Trait("Category", "8.4")]
        public void WhenAppearsNotClosedSingleQuoteProgramFailes3()
        {
			RunTest(@"TestCases/ch08/8.4/S8.4_A13_T3.js", true);
        }

        [Fact]
        [Trait("Category", "8.4")]
        public void WhenAppearsNotClosedDoubleQuoteProgramFailes()
        {
			RunTest(@"TestCases/ch08/8.4/S8.4_A14_T1.js", true);
        }

        [Fact]
        [Trait("Category", "8.4")]
        public void WhenAppearsNotClosedDoubleQuoteProgramFailes2()
        {
			RunTest(@"TestCases/ch08/8.4/S8.4_A14_T2.js", true);
        }

        [Fact]
        [Trait("Category", "8.4")]
        public void WhenAppearsNotClosedDoubleQuoteProgramFailes3()
        {
			RunTest(@"TestCases/ch08/8.4/S8.4_A14_T3.js", true);
        }

        [Fact]
        [Trait("Category", "8.4")]
        public void EmptyStringHasTypeString()
        {
			RunTest(@"TestCases/ch08/8.4/S8.4_A2.js", false);
        }

        [Fact]
        [Trait("Category", "8.4")]
        public void StringTypeHasALengthProperty()
        {
			RunTest(@"TestCases/ch08/8.4/S8.4_A3.js", false);
        }

        [Fact]
        [Trait("Category", "8.4")]
        public void EmptyStringVariableHasALengthProperty()
        {
			RunTest(@"TestCases/ch08/8.4/S8.4_A4.js", false);
        }

        [Fact]
        [Trait("Category", "8.4")]
        public void Zero0NotTerminatesTheStringCString()
        {
			RunTest(@"TestCases/ch08/8.4/S8.4_A5.js", false);
        }

        [Fact]
        [Trait("Category", "8.4")]
        public void LargeString4096Bytes()
        {
			RunTest(@"TestCases/ch08/8.4/S8.4_A6.1.js", false);
        }

        [Fact]
        [Trait("Category", "8.4")]
        public void LargeString8192Bytes()
        {
			RunTest(@"TestCases/ch08/8.4/S8.4_A6.2.js", false);
        }

        [Fact]
        [Trait("Category", "8.4")]
        public void LfBetweenChunksOfOneStringNotAllowed()
        {
			RunTest(@"TestCases/ch08/8.4/S8.4_A7.1.js", true);
        }

        [Fact]
        [Trait("Category", "8.4")]
        public void CrBetweenChunksOfOneStringNotAllowed()
        {
			RunTest(@"TestCases/ch08/8.4/S8.4_A7.2.js", true);
        }

        [Fact]
        [Trait("Category", "8.4")]
        public void PsBetweenChunksOfOneStringNotAllowed()
        {
			RunTest(@"TestCases/ch08/8.4/S8.4_A7.3.js", true);
        }

        [Fact]
        [Trait("Category", "8.4")]
        public void LsBetweenChunksOfOneStringNotAllowed()
        {
			RunTest(@"TestCases/ch08/8.4/S8.4_A7.4.js", true);
        }

        [Fact]
        [Trait("Category", "8.4")]
        public void EmptyString0FalseAreAllEqualToEachOtherSinceTheyAllEvaluateTo0()
        {
			RunTest(@"TestCases/ch08/8.4/S8.4_A8.js", false);
        }

        [Fact]
        [Trait("Category", "8.4")]
        public void AssignmentToStringLiteralsCallsStringConstructor()
        {
			RunTest(@"TestCases/ch08/8.4/S8.4_A9_T1.js", false);
        }

        [Fact]
        [Trait("Category", "8.4")]
        public void AssignmentToStringLiteralsCallsStringConstructor2()
        {
			RunTest(@"TestCases/ch08/8.4/S8.4_A9_T2.js", false);
        }

        [Fact]
        [Trait("Category", "8.4")]
        public void AssignmentToStringLiteralsCallsStringConstructor3()
        {
			RunTest(@"TestCases/ch08/8.4/S8.4_A9_T3.js", false);
        }


    }
}
