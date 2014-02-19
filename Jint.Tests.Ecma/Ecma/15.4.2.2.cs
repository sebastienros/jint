using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_15_4_2_2 : EcmaTest
    {
        [Fact]
        [Trait("Category", "15.4.2.2")]
        public void ThePrototypePropertyOfTheNewlyConstructedObjectIsSetToTheOriginalArrayPrototypeObjectTheOneThatIsTheInitialValueOfArrayPrototype()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.2/15.4.2.2/S15.4.2.2_A1.1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.2.2")]
        public void ThePrototypePropertyOfTheNewlyConstructedObjectIsSetToTheOriginalArrayPrototypeObjectTheOneThatIsTheInitialValueOfArrayPrototype2()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.2/15.4.2.2/S15.4.2.2_A1.1_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.2.2")]
        public void ThePrototypePropertyOfTheNewlyConstructedObjectIsSetToTheOriginalArrayPrototypeObjectTheOneThatIsTheInitialValueOfArrayPrototype3()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.2/15.4.2.2/S15.4.2.2_A1.1_T3.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.2.2")]
        public void TheClassPropertyOfTheNewlyConstructedObjectIsSetToArray()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.2/15.4.2.2/S15.4.2.2_A1.2_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.2.2")]
        public void IfTheArgumentLenIsANumberAndTouint32LenIsEqualToLenThenTheLengthPropertyOfTheNewlyConstructedObjectIsSetToTouint32Len()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.2/15.4.2.2/S15.4.2.2_A2.1_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.2.2")]
        public void IfTheArgumentLenIsANumberAndTouint32LenIsNotEqualToLenARangeerrorExceptionIsThrown()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.2/15.4.2.2/S15.4.2.2_A2.2_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.2.2")]
        public void IfTheArgumentLenIsANumberAndTouint32LenIsNotEqualToLenARangeerrorExceptionIsThrown2()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.2/15.4.2.2/S15.4.2.2_A2.2_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.2.2")]
        public void IfTheArgumentLenIsANumberAndTouint32LenIsNotEqualToLenARangeerrorExceptionIsThrown3()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.2/15.4.2.2/S15.4.2.2_A2.2_T3.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.2.2")]
        public void IfTheArgumentLenIsNotANumberThenTheLengthPropertyOfTheNewlyConstructedObjectIsSetTo1AndThe0PropertyOfTheNewlyConstructedObjectIsSetToLen()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.2/15.4.2.2/S15.4.2.2_A2.3_T1.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.2.2")]
        public void IfTheArgumentLenIsNotANumberThenTheLengthPropertyOfTheNewlyConstructedObjectIsSetTo1AndThe0PropertyOfTheNewlyConstructedObjectIsSetToLen2()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.2/15.4.2.2/S15.4.2.2_A2.3_T2.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.2.2")]
        public void IfTheArgumentLenIsNotANumberThenTheLengthPropertyOfTheNewlyConstructedObjectIsSetTo1AndThe0PropertyOfTheNewlyConstructedObjectIsSetToLen3()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.2/15.4.2.2/S15.4.2.2_A2.3_T3.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.2.2")]
        public void IfTheArgumentLenIsNotANumberThenTheLengthPropertyOfTheNewlyConstructedObjectIsSetTo1AndThe0PropertyOfTheNewlyConstructedObjectIsSetToLen4()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.2/15.4.2.2/S15.4.2.2_A2.3_T4.js", false);
        }

        [Fact]
        [Trait("Category", "15.4.2.2")]
        public void IfTheArgumentLenIsNotANumberThenTheLengthPropertyOfTheNewlyConstructedObjectIsSetTo1AndThe0PropertyOfTheNewlyConstructedObjectIsSetToLen5()
        {
			RunTest(@"TestCases/ch15/15.4/15.4.2/15.4.2.2/S15.4.2.2_A2.3_T5.js", false);
        }


    }
}
