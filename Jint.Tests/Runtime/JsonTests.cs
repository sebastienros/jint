using Jint.Native.Json;
using Jint.Runtime;

namespace Jint.Tests.Runtime
{
    public class JsonTests
    {
        [Fact]
        public void CanParseTabsInProperties()
        {
             var engine = new Engine();
             const string script = @"JSON.parse(""{\""abc\\tdef\"": \""42\""}"");";
             var obj = engine.Evaluate(script).AsObject();
             Assert.True(obj.HasOwnProperty("abc\tdef"));
        }

        [Theory]
        [InlineData("{\"a\":\"\\\"\"}", "\"")] // " quotation mark
        [InlineData("{\"a\":\"\\\\\"}", "\\")] // \ reverse solidus
        [InlineData("{\"a\":\"\\/\"}", "/")] // / solidus
        [InlineData("{\"a\":\"\\b\"}", "\b")] // backspace
        [InlineData("{\"a\":\"\\f\"}", "\f")] // formfeed
        [InlineData("{\"a\":\"\\n\"}", "\n")] // linefeed
        [InlineData("{\"a\":\"\\r\"}", "\r")] // carriage return
        [InlineData("{\"a\":\"\\t\"}", "\t")] // horizontal tab
        [InlineData("{\"a\":\"\\u0000\"}", "\0")]
        [InlineData("{\"a\":\"\\u0001\"}", "\x01")]
        [InlineData("{\"a\":\"\\u0061\"}", "a")]
        public void ShouldParseEscapedCharactersCorrectly(string json, string expectedCharacter)
        {
            var engine = new Engine();
            var parser = new JsonParser(engine);

            var parsedCharacter = parser.Parse(json).AsObject().Get("a").AsString();

            Assert.Equal(expectedCharacter, parsedCharacter);
        }

        [Theory]
        [InlineData("{\"a\":1", "Unexpected end of JSON input at position 6")]
        [InlineData("{\"a\":1},", "Unexpected token ',' in JSON at position 7")]
        [InlineData("{1}", "Unexpected number in JSON at position 1")]
        [InlineData("{\"a\" \"a\"}", "Unexpected string in JSON at position 5")]
        [InlineData("{true}", "Unexpected token 'true' in JSON at position 1")]
        [InlineData("{null}", "Unexpected token 'null' in JSON at position 1")]
        [InlineData("{:}", "Unexpected token ':' in JSON at position 1")]
        [InlineData("\"\\uah\"", "Expected hexadecimal digit in JSON at position 4")]
        [InlineData("0123", "Unexpected token '1' in JSON at position 1")]  // leading 0 (octal number) not allowed
        [InlineData("1e+A", "Unexpected token 'A' in JSON at position 3")]
        [InlineData("truE", "Unexpected token ILLEGAL in JSON at position 0")]
        [InlineData("nul", "Unexpected token ILLEGAL in JSON at position 0")]
        [InlineData("\"ab\t\"", "Invalid character in JSON at position 3")] // invalid char in string literal
        [InlineData("\"ab", "Unexpected end of JSON input at position 3")] // unterminated string literal
        [InlineData("alpha", "Unexpected token 'a' in JSON at position 0")]
        [InlineData("[1,\na]", "Unexpected token 'a' in JSON at position 4")] // multiline
        [InlineData("\x06", "Unexpected token '\x06' in JSON at position 0")] // control char
        [InlineData("{\"\\v\":1}", "Unexpected token 'v' in JSON at position 3")] // invalid escape sequence
        public void ShouldReportHelpfulSyntaxErrorForInvalidJson(string json, string expectedMessage)
        {
            var engine = new Engine();
            var parser = new JsonParser(engine);
            var ex = Assert.ThrowsAny<JavaScriptException>(() =>
            {
                parser.Parse(json);
            });

            Assert.Equal(expectedMessage, ex.Message);

            var error = ex.Error as Native.Error.ErrorInstance;
            Assert.NotNull(error);
            Assert.Equal("SyntaxError", error.Get("name"));
        }

        [Theory]
        [InlineData("[[]]", "[\n  []\n]")]
        [InlineData("[ { a: [{ x: 0 }], b:[]} ]",
            "[\n  {\n    \"a\": [\n      {\n        \"x\": 0\n      }\n    ],\n    \"b\": []\n  }\n]")]
        public void ShouldSerializeWithCorrectIndentation(string script, string expectedJson)
        {
            var engine = new Engine();
            engine.SetValue("x", engine.Evaluate(script));

            var result = engine.Evaluate("JSON.stringify(x, null, 2);").AsString();

            Assert.Equal(expectedJson, result);
        }
    }
}
