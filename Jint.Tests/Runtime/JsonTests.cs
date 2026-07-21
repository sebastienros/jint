using System.Globalization;
using Jint.Native;
using Jint.Native.Json;
using Jint.Native.Object;
using Jint.Runtime;

namespace Jint.Tests.Runtime;

public class JsonTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(15)]
    [InlineData(16)]
    [InlineData(17)]
    [InlineData(1000)]
    public void CanParseArraysOfAnySize(int size)
    {
        var engine = new Engine();
        var json = "[" + string.Join(",", Enumerable.Range(0, size)) + "]";
        var ok = engine.Evaluate($"var a = JSON.parse('{json}'); a.length === {size} && ({size} === 0 || a[{size - 1}] === {size - 1})").AsBoolean();

        Assert.True(ok);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(15)]
    [InlineData(16)]
    [InlineData(17)]
    [InlineData(1000)]
    public void CanParseArraysOfAnySizeWithReviver(int size)
    {
        var engine = new Engine();
        var json = "[" + string.Join(",", Enumerable.Range(0, size)) + "]";
        var ok = engine.Evaluate($"var a = JSON.parse('{json}', function (k, v) {{ return v; }}); a.length === {size} && ({size} === 0 || a[{size - 1}] === {size - 1})").AsBoolean();

        Assert.True(ok);
    }

    [Fact]
    public void CanParseNestedArrays()
    {
        var engine = new Engine();
        var ok = engine.Evaluate("""
            var a = JSON.parse('[[1,2,3],[4,5,[6,7,[8]]],9]');
            JSON.stringify(a) === '[[1,2,3],[4,5,[6,7,[8]]],9]';
            """).AsBoolean();

        Assert.True(ok);
    }

    [Theory]
    [InlineData(@"JSON.parse(""{\""abc\\tdef\"": \""42\""}"");", "abc\tdef")]
    [InlineData(@"JSON.parse(""{\""abc\\ndef\"": \""42\""}"");", "abc\ndef")]
    [InlineData(@"JSON.parse(""{\""abc\\fdef\"": \""42\""}"");", "abc\fdef")]
    [InlineData(@"JSON.parse(""{\""abc\\bdef\"": \""42\""}"");", "abc\bdef")]
    [InlineData(@"JSON.parse(""{\""abc\\rdef\"": \""42\""}"");", "abc\rdef")]
    [InlineData(@"JSON.parse(""{\""abc\\r\\ndef\"": \""42\""}"");", "abc\r\ndef")]
    [InlineData(@"JSON.parse(""{\""abc\\\""def\"": \""42\""}"");", "abc\"def")]
    public void CanParseEscapeSequencesInProperties(string script, string expectedPropertyName)
    {
        var engine = new Engine();
        var obj = engine.Evaluate(script).AsObject();
        Assert.True(obj.HasOwnProperty(expectedPropertyName));
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
    [InlineData("\"\u0001abc\"", "Invalid character in JSON at position 1")] // control char right after the opening quote
    [InlineData("\"abc\u0005def\"", "Invalid character in JSON at position 4")] // control char in the middle of a bulk run
    [InlineData("\"ab", "Unexpected end of JSON input at position 3")] // unterminated string literal
    [InlineData("alpha", "Unexpected token 'a' in JSON at position 0")]
    [InlineData("[1,\na]", "Unexpected token 'a' in JSON at position 4")] // multiline
    [InlineData("\x06", "Unexpected token '\x06' in JSON at position 0")] // control char
    [InlineData("{\"\\v\":1}", "Unexpected token 'v' in JSON at position 3")] // invalid escape sequence
    [InlineData("[,]", "Unexpected token ',' in JSON at position 1")]
    [InlineData("{\"key\": ()}", "Unexpected token '(' in JSON at position 8")]
    [InlineData(".1", "Unexpected token '.' in JSON at position 0")]
    [InlineData("\"\\u", "Expected hexadecimal digit in JSON at position 3")] // truncated \u escape at end of input
    [InlineData("\"\\u1\"", "Expected hexadecimal digit in JSON at position 4")] // \u with only 1 hex digit
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

    [Fact]
    public void ParsedProtoKeyBecomesOwnDataPropertyAndDoesNotPollutePrototype()
    {
        var engine = new Engine();
        var ok = engine.Evaluate("""
            var o = JSON.parse('{"__proto__":{"a":1}}');
            o.hasOwnProperty('__proto__')
                && Object.getPrototypeOf(o) === Object.prototype
                && o.__proto__.a === 1
                && !('a' in {})
                && Object.prototype.a === undefined;
            """).AsBoolean();

        Assert.True(ok);
    }

    [Fact]
    public void ParsedProtoKeyBecomesOwnDataPropertyWithReviver()
    {
        var engine = new Engine();
        var ok = engine.Evaluate("""
            var o = JSON.parse('{"__proto__":{"a":1}}', function (k, v) { return v; });
            o.hasOwnProperty('__proto__')
                && Object.getPrototypeOf(o) === Object.prototype
                && o.__proto__.a === 1
                && !('a' in {})
                && Object.prototype.a === undefined;
            """).AsBoolean();

        Assert.True(ok);
    }

    [Fact]
    public void DuplicateProtoKeysLastValueWins()
    {
        var engine = new Engine();
        var ok = engine.Evaluate("""
            var o = JSON.parse('{"__proto__":1,"__proto__":2}');
            var d = Object.getOwnPropertyDescriptor(o, '__proto__');
            d.value === 2 && d.writable && d.enumerable && d.configurable
                && Object.getPrototypeOf(o) === Object.prototype;
            """).AsBoolean();

        Assert.True(ok);
    }

    [Fact]
    public void DuplicateKeysLastValueWinsAtFirstPosition()
    {
        var engine = new Engine();
        var ok = engine.Evaluate("""
            var o = JSON.parse('{"a":1,"b":9,"a":2}');
            o.a === 2
                && JSON.stringify(Object.keys(o)) === '["a","b"]'
                && JSON.stringify(o) === '{"a":2,"b":9}';
            """).AsBoolean();

        Assert.True(ok);
    }

    [Theory]
    [InlineData("""{"1":"y","b":"z"}""", """["1","b"]""")]
    [InlineData("""{"b":"z","1":"y"}""", """["1","b"]""")]
    [InlineData("""{"0":1}""", """["0"]""")]
    [InlineData("""{"b":1,"1abc":2,"a":3}""", """["b","1abc","a"]""")]
    public void IntegerLikeKeysEnumerateInSpecOrder(string json, string expectedKeys)
    {
        var engine = new Engine();
        engine.SetValue("json", json);
        var keys = engine.Evaluate("JSON.stringify(Object.keys(JSON.parse(json)))").AsString();

        Assert.Equal(expectedKeys, keys);
    }

    [Fact]
    public void CanParseObjectWithManyProperties()
    {
        // 100 properties trips the 64-own-property shape guard mid-build; the object finishes as a
        // dictionary with insertion order intact and stays fully mutable.
        var engine = new Engine();
        var json = "{" + string.Join(",", Enumerable.Range(0, 100).Select(i => $"\"p{i}\":{i}")) + "}";
        engine.SetValue("json", json);
        var ok = engine.Evaluate("""
            var o = JSON.parse(json);
            var keys = Object.keys(o);
            var ok = keys.length === 100;
            for (var i = 0; i < 100; i++) {
                ok = ok && keys[i] === ('p' + i) && o['p' + i] === i;
            }
            o.extra = 'x';
            delete o.p0;
            ok && o.extra === 'x' && !('p0' in o) && Object.keys(o).length === 100;
            """).AsBoolean();

        Assert.True(ok);
    }

    [Fact]
    public void ReviverCanMutateAndDeleteParsedObjectProperties()
    {
        var engine = new Engine();
        var ok = engine.Evaluate("""
            var o = JSON.parse('{"keep":1,"double":2,"drop":3}', function (k, v) {
                if (k === 'drop') return undefined;
                if (k === 'double') return v * 2;
                return v;
            });
            o.keep === 1 && o.double === 4 && !('drop' in o)
                && JSON.stringify(Object.keys(o)) === '["keep","double"]';
            """).AsBoolean();

        Assert.True(ok);
    }

    [Fact]
    public void ReviverWorksOverArrayOfRecords()
    {
        var engine = new Engine();
        var ok = engine.Evaluate("""
            var a = JSON.parse('[{"id":1,"v":10},{"id":2,"v":20},{"id":3,"v":30}]', function (k, v) {
                return typeof v === 'number' && k === 'v' ? v + 1 : v;
            });
            a.length === 3 && a[0].v === 11 && a[1].v === 21 && a[2].v === 31
                && a[0].id === 1 && a[2].id === 3;
            """).AsBoolean();

        Assert.True(ok);
    }

    [Fact]
    public void ReviverContextSourceIsProvidedForPrimitives()
    {
        var engine = new Engine();
        var ok = engine.Evaluate("""
            var sources = {};
            JSON.parse('{"a":1,"b":"x"}', function (k, v, context) {
                if (context && typeof context.source === 'string') sources[k] = context.source;
                return v;
            });
            sources.a === '1' && sources.b === '"x"';
            """).AsBoolean();

        Assert.True(ok);
    }

    [Fact]
    public void ParsedObjectSupportsPostParseMutation()
    {
        var engine = new Engine();
        var ok = engine.Evaluate("""
            var o = JSON.parse('{"a":1,"b":2}');
            var ok = !('x' in o);
            o.x = 3;
            ok = ok && o.x === 3 && ('x' in o);
            delete o.b;
            ok = ok && !('b' in o) && JSON.stringify(Object.keys(o)) === '["a","x"]';
            Object.defineProperty(o, 'acc', { get: function () { return o.a + 10; }, configurable: true });
            ok = ok && o.acc === 11;
            Object.freeze(o);
            o.a = 42;
            ok && o.a === 1 && Object.isFrozen(o);
            """).AsBoolean();

        Assert.True(ok);
    }

    [Fact]
    public void MutatingOneParsedRecordDoesNotAffectOthers()
    {
        var engine = new Engine();
        var json = "[" + string.Join(",", Enumerable.Range(0, 100).Select(i => $"{{\"id\":{i},\"name\":\"n{i}\",\"flag\":true,\"score\":1.5}}")) + "]";
        engine.SetValue("json", json);
        var ok = engine.Evaluate("""
            var a = JSON.parse(json);
            a[42].name = 'changed';
            a[42].id = -1;
            var ok = a[42].name === 'changed' && a[42].id === -1;
            for (var i = 0; i < 100; i++) {
                if (i === 42) continue;
                ok = ok && a[i].id === i && a[i].name === ('n' + i) && a[i].flag === true && a[i].score === 1.5;
            }
            ok;
            """).AsBoolean();

        Assert.True(ok);
    }

    [Fact]
    public void ParseRemainsCorrectWhenShapeTransitionBudgetIsExhausted()
    {
        // >1024 distinct object layouts whose per-node fan-out stays below the megamorphic guard, so
        // the parser's per-call transition budget runs out mid-document and later objects fall back to
        // dictionary building. Only correctness (values and key order) is asserted.
        var engine = new Engine();
        var json = "[" + string.Join(",", Enumerable.Range(0, 1100).Select(i => $"{{\"a\":{i},\"b{i % 50}\":{i},\"c{i / 50}\":{i}}}")) + "]";
        engine.SetValue("json", json);
        var ok = engine.Evaluate("""
            var a = JSON.parse(json);
            var ok = a.length === 1100;
            for (var i = 0; i < 1100; i++) {
                var o = a[i];
                var keys = Object.keys(o);
                ok = ok && o.a === i
                    && o['b' + (i % 50)] === i
                    && o['c' + Math.floor(i / 50)] === i
                    && keys.length === 3
                    && keys[0] === 'a'
                    && keys[1] === 'b' + (i % 50)
                    && keys[2] === 'c' + Math.floor(i / 50);
            }
            ok;
            """).AsBoolean();

        Assert.True(ok);
    }

    [Fact]
    public void ParsedRecordsFromSameDocumentShareShape()
    {
        var engine = new Engine();
        var array = engine.Evaluate("""JSON.parse('[{"a":1,"b":2},{"a":3,"b":4}]')""").AsArray();
        var first = Assert.IsType<JsObject>(array[0]);
        var second = Assert.IsType<JsObject>(array[1]);

        Assert.NotNull(first.ShapeOf);
        Assert.Same(first.ShapeOf, second.ShapeOf);
    }

    [Fact]
    public void ParsedRecordsRoundTripThroughStringify()
    {
        var engine = new Engine();
        var ok = engine.Evaluate("""
            JSON.stringify(JSON.parse('[{"a":1,"b":"x"},{"a":2,"b":"y"}]')) === '[{"a":1,"b":"x"},{"a":2,"b":"y"}]';
            """).AsBoolean();

        Assert.True(ok);
    }

    [Fact]
    public void CanParseNestedObjectsWithSharedAndDistinctLayouts()
    {
        var engine = new Engine();
        var ok = engine.Evaluate("""
            var o = JSON.parse('{"outer":{"inner":{"x":1,"y":2},"z":3},"w":{"x":4,"y":5}}');
            o.outer.inner.x === 1 && o.outer.inner.y === 2 && o.outer.z === 3
                && o.w.x === 4 && o.w.y === 5
                && JSON.stringify(Object.keys(o)) === '["outer","w"]'
                && JSON.stringify(Object.keys(o.outer)) === '["inner","z"]';
            """).AsBoolean();

        Assert.True(ok);
    }

    [Fact]
    public void CanBulkScanStringLiteralsWithEscapesAtEdges()
    {
        var parser = new JsonParser(new Engine());

        // escape at the very start, in the middle and at the end of the bulk run
        Assert.Equal("\nabc", parser.Parse("\"\\nabc\"").AsString());
        Assert.Equal("abc\ndef", parser.Parse("\"abc\\ndef\"").AsString());
        Assert.Equal("abc\n", parser.Parse("\"abc\\n\"").AsString());

        // every simple escape back-to-back: JSON "\"\\\/\n\r\t\b\f"
        Assert.Equal("\"\\/\n\r\t\b\f", parser.Parse("\"\\\"\\\\\\/\\n\\r\\t\\b\\f\"").AsString());

        // \uXXXX escapes (BMP)
        Assert.Equal("Aé中", parser.Parse("\"\\u0041\\u00e9\\u4e2d\"").AsString());

        // a run with no escapes at all takes the pure bulk path
        Assert.Equal("plain text with spaces", parser.Parse("\"plain text with spaces\"").AsString());
    }

    [Fact]
    public void CanBulkScanStringsCrossingInternalBufferBoundary()
    {
        // ValueStringBuilder starts with a 64-char stack buffer; these inputs force it to grow
        // while the bulk Append copies whole spans.
        var parser = new JsonParser(new Engine());

        // a single content run far longer than the 64-char buffer
        var long1 = new string('a', 200);
        Assert.Equal(long1, parser.Parse("\"" + long1 + "\"").AsString());

        // escape just before the boundary, then a long run after it
        var before = new string('a', 60);
        var after = new string('b', 60);
        Assert.Equal(before + "\n" + after, parser.Parse("\"" + before + "\\n" + after + "\"").AsString());

        // escape just after the boundary
        var before3 = new string('c', 70);
        var after3 = new string('d', 5);
        Assert.Equal(before3 + "\t" + after3, parser.Parse("\"" + before3 + "\\t" + after3 + "\"").AsString());

        // many escapes interspersed straddling the boundary
        var json = new System.Text.StringBuilder("\"");
        var expected = new System.Text.StringBuilder();
        for (var i = 0; i < 100; i++)
        {
            json.Append('x').Append("\\n");
            expected.Append('x').Append('\n');
        }
        json.Append('"');
        Assert.Equal(expected.ToString(), parser.Parse(json.ToString()).AsString());
    }

    [Fact]
    public void BulkScanPreservesUnicodeLineSeparatorTermination()
    {
        // U+2028 / U+2029 terminate the string scan with the quote still open, which the original
        // char-by-char scanner reported as UnexpectedEOS just past the separator. Lock that in.
        var parser = new JsonParser(new Engine());

        var ex = Assert.ThrowsAny<JavaScriptException>(() => parser.Parse("\"ab\u2028cd\""));
        Assert.Equal("Unexpected end of JSON input at position 4", ex.Message);

        var ex2 = Assert.ThrowsAny<JavaScriptException>(() => parser.Parse("\"ab\u2029cd\""));
        Assert.Equal("Unexpected end of JSON input at position 4", ex2.Message);
    }

    private const NumberStyles JsonNumberStyles =
        NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint | NumberStyles.AllowExponent;

    [Fact]
    public void NumberFastPathIsBitIdenticalToDoubleParse()
    {
        var parser = new JsonParser(new Engine());
        var random = new Random(20260721);

        for (var iteration = 0; iteration < 200_000; iteration++)
        {
            var json = GenerateRandomJsonNumber(random);
            var expected = BitConverter.DoubleToInt64Bits(double.Parse(json, JsonNumberStyles, CultureInfo.InvariantCulture));
            var actual = BitConverter.DoubleToInt64Bits(parser.Parse(json).AsNumber());
            Assert.True(expected == actual, $"Bit mismatch for '{json}': expected 0x{expected:X16}, actual 0x{actual:X16}");
        }
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-0")]
    [InlineData("0.0")]
    [InlineData("-0.0")]
    [InlineData("0.1")]
    [InlineData("0.2")]
    [InlineData("0.3")]
    [InlineData("1.005")]
    [InlineData("-1.5")]
    [InlineData("19.99")]
    [InlineData("37.774929")]
    [InlineData("3.141592653589")]     // 13 significant digits, fast path
    [InlineData("1.23456789012345")]   // 15 total digits, fast path
    [InlineData("123456789012.345")]   // 15 total digits, fast path
    [InlineData("0.00000000000001")]   // tiny fraction, small numerator
    [InlineData("0.123456789012345")]  // 16 total digit chars -> falls back to double.Parse
    [InlineData("1234567890123456.5")] // 16 integer digits -> falls back
    [InlineData("99999999999999.9")]   // 16 total digits -> falls back
    [InlineData("9007199254740992")]   // 2^53 exactly (integer path)
    [InlineData("1.7976931348623157e308")] // exponent -> falls back
    [InlineData("5e-324")]             // exponent -> falls back
    public void NumberFastPathEdgeCasesMatchDoubleParse(string json)
    {
        var parser = new JsonParser(new Engine());
        var expected = BitConverter.DoubleToInt64Bits(double.Parse(json, JsonNumberStyles, CultureInfo.InvariantCulture));
        var actual = BitConverter.DoubleToInt64Bits(parser.Parse(json).AsNumber());
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("-0")]
    [InlineData("-0.0")]
    [InlineData("-0.00")]
    public void NumberFastPathMatchesDoubleParseForNegativeZero(string json)
    {
        // Negative zero is deliberately deferred to double.Parse so the platform-specific sign of zero
        // (+0.0 on .NET Framework, -0.0 on .NET Core) is preserved exactly rather than normalized.
        var expected = BitConverter.DoubleToInt64Bits(double.Parse(json, JsonNumberStyles, CultureInfo.InvariantCulture));
        var parser = new JsonParser(new Engine());
        Assert.Equal(expected, BitConverter.DoubleToInt64Bits(parser.Parse(json).AsNumber()));
    }

    [Theory]
    [InlineData("0")]
    [InlineData("0.0")]
    [InlineData("0.5")]
    public void NumberFastPathProducesPositiveZeroWhereExpected(string json)
    {
        var positiveZeroBits = BitConverter.DoubleToInt64Bits(0.0);
        var parser = new JsonParser(new Engine());
        var bits = BitConverter.DoubleToInt64Bits(parser.Parse(json).AsNumber());
        if (json == "0.5")
        {
            Assert.NotEqual(positiveZeroBits, bits);
        }
        else
        {
            Assert.Equal(positiveZeroBits, bits);
        }
    }

    [Fact]
    public void RepeatedStringValuesShareOneInstanceAcrossArraysAndObjects()
    {
        var parser = new JsonParser(new Engine());

        var arr = parser.Parse("""["alpha","alpha","beta","alpha"]""").AsArray();
        Assert.Equal("alpha", arr.Get("0").AsString());
        Assert.Same(arr.Get("0"), arr.Get("1"));
        Assert.Same(arr.Get("0"), arr.Get("3"));
        Assert.Equal("beta", arr.Get("2").AsString());
        Assert.NotSame(arr.Get("0"), arr.Get("2"));

        var obj = parser.Parse("""{"a":"repeat","b":"repeat","c":"other"}""").AsObject();
        Assert.Same(obj.Get("a"), obj.Get("b"));
        Assert.Equal("repeat", obj.Get("a").AsString());
        Assert.Equal("other", obj.Get("c").AsString());

        // Values interned once per parse are shared across nested arrays and objects within that parse.
        var root = parser.Parse("""{"list":["x-marker","x-marker"],"val":"x-marker"}""").AsObject();
        var list = root.Get("list").AsArray();
        Assert.Same(list.Get("0"), list.Get("1"));
        Assert.Same(list.Get("0"), root.Get("val"));
    }

    [Fact]
    public void EscapedAndUnescapedValuesWithSameContentAreEqualAndInterned()
    {
        var parser = new JsonParser(new Engine());

        // "AB" written three ways: plain, fully-escaped, and partially-escaped. All decode to the same
        // content, so interning (which keys off the DECODED span) must return one shared instance.
        var arr = parser.Parse("""["AB","AB","AB"]""").AsArray();
        Assert.Equal("AB", arr.Get("0").AsString());
        Assert.Equal("AB", arr.Get("1").AsString());
        Assert.Equal("AB", arr.Get("2").AsString());
        Assert.Same(arr.Get("0"), arr.Get("1"));
        Assert.Same(arr.Get("0"), arr.Get("2"));

        // An escape sequence that decodes to content differing only by the escape must not collide.
        var arr2 = parser.Parse("""["line\nbreak","line\nbreak","linebreak"]""").AsArray();
        Assert.Equal("line\nbreak", arr2.Get("0").AsString());
        Assert.Same(arr2.Get("0"), arr2.Get("1"));
        Assert.NotSame(arr2.Get("0"), arr2.Get("2"));
        Assert.Equal("linebreak", arr2.Get("2").AsString());
    }

    [Fact]
    public void EmptyAndSingleCharacterValuesParseAndReuseCaches()
    {
        var parser = new JsonParser(new Engine());

        var arr = parser.Parse("""["","","x","x","y"]""").AsArray();
        Assert.Equal("", arr.Get("0").AsString());
        // Empty routes through JsString.Create -> the shared JsString.Empty singleton.
        Assert.Same(JsString.Empty, arr.Get("0"));
        Assert.Same(arr.Get("0"), arr.Get("1"));
        Assert.Equal("x", arr.Get("2").AsString());
        Assert.Same(arr.Get("2"), arr.Get("3"));
        Assert.Equal("y", arr.Get("4").AsString());
        Assert.NotSame(arr.Get("2"), arr.Get("4"));
    }

    [Fact]
    public void ManyDistinctStringValuesParseCorrectlyDespiteTableThrashing()
    {
        var parser = new JsonParser(new Engine());

        // Far more distinct values than the fixed intern table has slots (256): the table must thrash
        // gracefully (replace-on-collision, no growth) and every value must still be exactly correct.
        const int count = 2000;
        var sb = new System.Text.StringBuilder();
        sb.Append('[');
        for (var i = 0; i < count; i++)
        {
            if (i > 0)
            {
                sb.Append(',');
            }
            sb.Append('"').Append("value_").Append(i).Append('"');
        }
        sb.Append(']');

        var arr = parser.Parse(sb.ToString()).AsArray();
        for (var i = 0; i < count; i++)
        {
            Assert.Equal("value_" + i, arr.Get(i.ToString(CultureInfo.InvariantCulture)).AsString());
        }
    }

    [Fact]
    public void OverLongStringValuesAreNotInternedButParseCorrectly()
    {
        var parser = new JsonParser(new Engine());

        // Longer than MaxInternedValueLength (64): deliberately skips the table so a hostile payload of
        // huge unique strings can never thrash it. Still parsed exactly, just not deduplicated.
        var longVal = new string('z', 100);
        var arr = parser.Parse($"[\"{longVal}\",\"{longVal}\"]").AsArray();
        Assert.Equal(longVal, arr.Get("0").AsString());
        Assert.Equal(longVal, arr.Get("1").AsString());
        Assert.NotSame(arr.Get("0"), arr.Get("1"));
    }

    [Fact]
    public void InternTableIsResetBetweenParses()
    {
        // A value interned in one parse must not leak into the next parse's identity checks; each parse
        // starts from a cleared table, so the same content produces a fresh instance on the second call.
        var parser = new JsonParser(new Engine());
        var first = parser.Parse("""["shared-token"]""").AsArray().Get("0");
        var second = parser.Parse("""["shared-token"]""").AsArray().Get("0");
        Assert.Equal("shared-token", first.AsString());
        Assert.Equal(first.AsString(), second.AsString());
        Assert.NotSame(first, second);
    }

    [Theory]
    [InlineData("1e-323")]
    [InlineData("123456789012345678901234567890")]
    [InlineData("-0")]
    [InlineData("1.7976931348623157e308")]
    [InlineData("0")]
    [InlineData("-0.0")]
    [InlineData("42")]
    [InlineData("-42")]
    [InlineData("3.14159")]
    [InlineData("9007199254740991")]
    [InlineData("0.123456789012345")]
    public void SpanNumberPathIsBitIdenticalToDoubleParse(string json)
    {
        var expected = BitConverter.DoubleToInt64Bits(double.Parse(json, JsonNumberStyles, CultureInfo.InvariantCulture));
        var parser = new JsonParser(new Engine());
        var actual = BitConverter.DoubleToInt64Bits(parser.Parse(json).AsNumber());
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void UnexpectedTrailingNumberTokenReportsItsRawText()
    {
        // Number tokens no longer carry an eager Text string; the diagnostic reconstructs the raw text
        // from the token range. Lock in that the reported token is byte-identical to the source.
        var parser = new JsonParser(new Engine());
        var ex = Assert.ThrowsAny<JavaScriptException>(() => parser.Parse("1 23"));
        Assert.Equal("Unexpected token '23' in JSON at position 2", ex.Message);
    }

    private static string GenerateRandomJsonNumber(Random random)
    {
        var sb = new System.Text.StringBuilder();
        if (random.Next(2) == 0)
        {
            sb.Append('-');
        }

        // integer part: either a lone '0' or a non-zero-leading run (valid JSON grammar)
        if (random.Next(10) == 0)
        {
            sb.Append('0');
        }
        else
        {
            var intLength = 1 + random.Next(18); // 1..18 digits, straddling the 15-digit fast-path bound
            sb.Append((char) ('1' + random.Next(9)));
            for (var k = 1; k < intLength; k++)
            {
                sb.Append((char) ('0' + random.Next(10)));
            }
        }

        // optional fraction
        if (random.Next(2) == 0)
        {
            sb.Append('.');
            var fractionLength = 1 + random.Next(18);
            for (var k = 0; k < fractionLength; k++)
            {
                sb.Append((char) ('0' + random.Next(10)));
            }
        }

        // optional exponent (always exercises the fallback path)
        if (random.Next(5) == 0)
        {
            sb.Append(random.Next(2) == 0 ? 'e' : 'E');
            var sign = random.Next(3);
            if (sign == 1)
            {
                sb.Append('+');
            }
            else if (sign == 2)
            {
                sb.Append('-');
            }
            sb.Append((char) ('0' + random.Next(10)));
            if (random.Next(2) == 0)
            {
                sb.Append((char) ('0' + random.Next(10)));
            }
        }

        return sb.ToString();
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
