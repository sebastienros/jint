using Jint.Native;
using Jint.Native.Json;
using Jint.Native.Object;
using Jint.Runtime;

namespace Jint.Tests.Runtime;

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
    [InlineData("{\"a\":\"\\u003C\"}", "<")]
    [InlineData("{\"a\":\"\\u003E\"}", ">")]
    [InlineData("{\"a\":\"\\u003c\"}", "<")]
    [InlineData("{\"a\":\"\\u003e\"}", ">")]
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
    [InlineData("[,]", "Unexpected token ',' in JSON at position 1")]
    [InlineData("{\"key\": ()}", "Unexpected token '(' in JSON at position 8")]
    [InlineData(".1", "Unexpected token '.' in JSON at position 0")]
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

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(10)]
    [InlineData(100)]
    public void ShouldParseArrayIndependentOfLengthInSameOrder(int numberOfElements)
    {
        string json = $"[{string.Join(",", Enumerable.Range(0, numberOfElements))}]";
        var engine = new Engine();
        var parser = new JsonParser(engine);

        JsValue result = parser.Parse(json);
        JsArray array = Assert.IsType<JsArray>(result);
        Assert.Equal((uint)numberOfElements, array.Length);
        for (int i = 0; i < numberOfElements; i++)
        {
            Assert.Equal(i, (int)array[i].AsNumber());
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(10)]
    [InlineData(20)]
    public void ShouldParseStringIndependentOfLength(int stringLength)
    {
        string generatedString = string.Join("", Enumerable.Range(0, stringLength).Select(index => 'A' + index));
        string json = $"\"{generatedString}\"";
        var engine = new Engine();
        var parser = new JsonParser(engine);

        string value = parser.Parse(json).AsString();
        Assert.Equal(generatedString, value, StringComparer.Ordinal);
    }

    [Fact]
    public void CanParsePrimitivesCorrectly()
    {
        var engine = new Engine();
        var parser = new JsonParser(engine);

        Assert.Same(JsBoolean.True, parser.Parse("true"));
        Assert.Same(JsBoolean.False, parser.Parse("false"));
        Assert.Same(JsValue.Null, parser.Parse("null"));
    }

    [Fact]
    public void CanParseNumbersWithAndWithoutSign()
    {
        var engine = new Engine();
        var parser = new JsonParser(engine);

        Assert.Equal(-1, (int)parser.Parse("-1").AsNumber());
        Assert.Equal(0, (int)parser.Parse("-0").AsNumber());
        Assert.Equal(0, (int)parser.Parse("0").AsNumber());
        Assert.Equal(1, (int)parser.Parse("1").AsNumber());
    }

    [Fact]
    public void DoesPreservesNumberToMaxSafeInteger()
    {
        // see https://tc39.es/ecma262/multipage/numbers-and-dates.html#sec-number.max_safe_integer
        long maxSafeInteger = 9007199254740991;
        var engine = new Engine();
        var parser = new JsonParser(engine);

        Assert.Equal(maxSafeInteger, (long)parser.Parse("9007199254740991").AsNumber());
    }

    [Fact]
    public void DoesSupportFractionalNumbers()
    {
        var engine = new Engine();
        var parser = new JsonParser(engine);

        Assert.Equal(0.1d, parser.Parse("0.1").AsNumber());
        Assert.Equal(1.1d, parser.Parse("1.1").AsNumber());
        Assert.Equal(-1.1d, parser.Parse("-1.1").AsNumber());
    }

    [Fact]
    public void DoesSupportScientificNotation()
    {
        var engine = new Engine();
        var parser = new JsonParser(engine);

        Assert.Equal(100d, parser.Parse("1E2").AsNumber());
        Assert.Equal(0.01d, parser.Parse("1E-2").AsNumber());
    }

    [Fact]
    public void ThrowsExceptionWhenDepthLimitReachedArrays()
    {
        string json = GenerateDeepNestedArray(65);

        var engine = new Engine();
        var parser = new JsonParser(engine);

        JavaScriptException ex = Assert.Throws<JavaScriptException>(() => parser.Parse(json));
        Assert.Equal("Max. depth level of JSON reached at position 64", ex.Message);
    }

    [Fact]
    public void ThrowsExceptionWhenDepthLimitReachedObjects()
    {
        string json = GenerateDeepNestedObject(65);

        var engine = new Engine();
        var parser = new JsonParser(engine);

        JavaScriptException ex = Assert.Throws<JavaScriptException>(() => parser.Parse(json));
        Assert.Equal("Max. depth level of JSON reached at position 320", ex.Message);
    }

    [Fact]
    public void CanParseMultipleNestedObjects()
    {
        string objectA = GenerateDeepNestedObject(63);
        string objectB = GenerateDeepNestedObject(63);
        string json = $"{{\"a\":{objectA},\"b\":{objectB} }}";

        var engine = new Engine();
        var parser = new JsonParser(engine);

        ObjectInstance parsed = parser.Parse(json).AsObject();
        Assert.True(parsed["a"].IsObject());
        Assert.True(parsed["b"].IsObject());
    }

    [Fact]
    public void CanParseMultipleNestedArrays()
    {
        string arrayA = GenerateDeepNestedArray(63);
        string arrayB = GenerateDeepNestedArray(63);
        string json = $"{{\"a\":{arrayA},\"b\":{arrayB} }}";

        var engine = new Engine();
        var parser = new JsonParser(engine);

        ObjectInstance parsed = parser.Parse(json).AsObject();
        Assert.True(parsed["a"].IsArray());
        Assert.True(parsed["b"].IsArray());
    }

    [Fact]
    public void ArrayAndObjectDepthNotCountedSeparately()
    {
        // individual depth is below the default limit, but combined
        // a max. depth level is reached.
        string innerArray = GenerateDeepNestedArray(40);
        string json = GenerateDeepNestedObject(40, innerArray);

        var engine = new Engine();
        var parser = new JsonParser(engine);

        JavaScriptException ex = Assert.Throws<JavaScriptException>(() => parser.Parse(json));
        Assert.Equal("Max. depth level of JSON reached at position 224", ex.Message);
    }

    [Fact]
    public void CustomMaxDepthOfZeroDisallowsObjects()
    {
        var engine = new Engine();
        var parser = new JsonParser(engine, maxDepth: 0);

        JavaScriptException ex = Assert.Throws<JavaScriptException>(() => parser.Parse("{}"));
        Assert.Equal("Max. depth level of JSON reached at position 0", ex.Message);
    }

    [Fact]
    public void CustomMaxDepthOfZeroDisallowsArrays()
    {
        var engine = new Engine();
        var parser = new JsonParser(engine, maxDepth: 0);

        JavaScriptException ex = Assert.Throws<JavaScriptException>(() => parser.Parse("[]"));
        Assert.Equal("Max. depth level of JSON reached at position 0", ex.Message);
    }

    [Fact]
    public void MaxDepthDoesNotInfluencePrimitiveValues()
    {
        var engine = new Engine();
        var parser = new JsonParser(engine, maxDepth: 1);

        ObjectInstance parsed = parser.Parse("{\"a\": 2, \"b\": true, \"c\": null, \"d\": \"test\"}").AsObject();
        Assert.True(parsed["a"].IsNumber());
        Assert.True(parsed["b"].IsBoolean());
        Assert.True(parsed["c"].IsNull());
        Assert.True(parsed["d"].IsString());
    }

    [Fact]
    public void MaxDepthGetsUsedFromEngineOptionsConstraints()
    {
        var engine = new Engine(options => options.MaxJsonParseDepth(0));
        var parser = new JsonParser(engine);

        Assert.Throws<JavaScriptException>(() => parser.Parse("[]"));
    }

    private static string GenerateDeepNestedArray(int depth)
    {
        string arrayOpen = new string('[', depth);
        string arrayClose = new string(']', depth);
        return $"{arrayOpen}{arrayClose}";
    }

    private static string GenerateDeepNestedObject(int depth)
    {
        return GenerateDeepNestedObject(depth, mostInnerValue: "1");
    }

    private static string GenerateDeepNestedObject(int depth, string mostInnerValue)
    {
        string objectOpen = string.Concat(Enumerable.Repeat("{\"A\":", depth));
        string objectClose = new string('}', depth);
        return $"{objectOpen}{mostInnerValue}{objectClose}";
    }
}