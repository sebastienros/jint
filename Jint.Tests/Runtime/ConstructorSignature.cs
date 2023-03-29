using Jint.Runtime.Interop;

namespace Jint.Tests.Runtime
{
    public class ConstructorSignature
    {
        [Fact]
        public void OptionalConstructorParameters()
        {
            var engine = new Engine();

            engine.SetValue("A", TypeReference.CreateTypeReference(engine, typeof(A)));

            // ParamArray tests
            Assert.Equal("3", engine.Evaluate("new A(1, 2).Result").AsString());
            Assert.Equal("3", engine.Evaluate("new A(1, 2, null).Result").AsString());
            Assert.Equal("3", engine.Evaluate("new A(1, 2, undefined).Result").AsString());
            Assert.Equal("5", engine.Evaluate("new A(1, 2, null, undefined).Result").AsString());
            Assert.Equal("9", engine.Evaluate("new A(1, 2, ...'packed').Result").AsString());
            Assert.Equal("3", engine.Evaluate("new A(1, 2, []).Result").AsString());
            Assert.Equal("7", engine.Evaluate("new A(1, 2, [...'abcd']).Result").AsString());

            // Optional parameter tests
            Assert.Equal("3", engine.Evaluate("new A(1, 2).Result").AsString());
            Assert.Equal("6", engine.Evaluate("new A(1).Result").AsString());
            Assert.Equal("7", engine.Evaluate("new A(2, undefined).Result").AsString());
            Assert.Equal("8", engine.Evaluate("new A(3, undefined).Result").AsString());
            Assert.Equal("ab", engine.Evaluate("new A('a').Result").AsString());
            Assert.Equal("ab", engine.Evaluate("new A('a', undefined).Result").AsString());
            Assert.Equal("ac", engine.Evaluate("new A('a', 'c').Result").AsString());
            Assert.Equal("adc", engine.Evaluate("new A('a', 'd', undefined).Result").AsString());
            Assert.Equal("ade", engine.Evaluate("new A('a', 'd', 'e').Result").AsString());
        }

        public class A
        {
            public A(int param1, int param2 = 5) => Result = (param1 + param2).ToString();
            public A(string param1, string param2 = "b") => Result = string.Concat(param1, param2);
            public A(string param1, string param2 = "b", string param3 = "c") => Result = string.Concat(param1, param2, param3);
            public A(int param1, int param2, params object[] param3) => Result = (param1 + param2 + param3?.Length).ToString();

            public string Result { get; }
        }
    }
}
