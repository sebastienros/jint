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

        ok.Should().BeTrue();
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

        ok.Should().BeTrue();
    }

    [Fact]
    public void CanParseNestedArrays()
    {
        var engine = new Engine();
        var ok = engine.Evaluate("""
            var a = JSON.parse('[[1,2,3],[4,5,[6,7,[8]]],9]');
            JSON.stringify(a) === '[[1,2,3],[4,5,[6,7,[8]]],9]';
            """).AsBoolean();

        ok.Should().BeTrue();
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
        obj.HasOwnProperty(expectedPropertyName).Should().BeTrue();
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

        parsedCharacter.Should().Be(expectedCharacter);
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
        var ex = Invoking(() =>
        {
            parser.Parse(json);
        }).Should().Throw<JavaScriptException>().Which;

        ex.Message.Should().Be(expectedMessage);

        var error = ex.Error as Native.Error.ErrorInstance;
        error.Should().NotBeNull();
        error.Get("name").Should().Be("SyntaxError");
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

        result.Should().Be(expectedJson);
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
        JsArray array = result.Should().BeOfType<JsArray>().Which;
        array.Length.Should().Be((uint)numberOfElements);
        for (int i = 0; i < numberOfElements; i++)
        {
            ((int)array[i].AsNumber()).Should().Be(i);
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
        value.Should().Be(generatedString);
    }

    [Fact]
    public void CanParsePrimitivesCorrectly()
    {
        var engine = new Engine();
        var parser = new JsonParser(engine);

        parser.Parse("true").Should().BeSameAs(JsBoolean.True);
        parser.Parse("false").Should().BeSameAs(JsBoolean.False);
        parser.Parse("null").Should().BeSameAs(JsValue.Null);
    }

    [Fact]
    public void CanParseNumbersWithAndWithoutSign()
    {
        var engine = new Engine();
        var parser = new JsonParser(engine);

        ((int)parser.Parse("-1").AsNumber()).Should().Be(-1);
        ((int)parser.Parse("-0").AsNumber()).Should().Be(0);
        ((int)parser.Parse("0").AsNumber()).Should().Be(0);
        ((int)parser.Parse("1").AsNumber()).Should().Be(1);
    }

    [Fact]
    public void DoesPreservesNumberToMaxSafeInteger()
    {
        // see https://tc39.es/ecma262/multipage/numbers-and-dates.html#sec-number.max_safe_integer
        long maxSafeInteger = 9007199254740991;
        var engine = new Engine();
        var parser = new JsonParser(engine);

        ((long)parser.Parse("9007199254740991").AsNumber()).Should().Be(maxSafeInteger);
    }

    [Fact]
    public void DoesSupportFractionalNumbers()
    {
        var engine = new Engine();
        var parser = new JsonParser(engine);

        parser.Parse("0.1").AsNumber().Should().Be(0.1d);
        parser.Parse("1.1").AsNumber().Should().Be(1.1d);
        parser.Parse("-1.1").AsNumber().Should().Be(-1.1d);
    }

    [Fact]
    public void DoesSupportScientificNotation()
    {
        var engine = new Engine();
        var parser = new JsonParser(engine);

        parser.Parse("1E2").AsNumber().Should().Be(100d);
        parser.Parse("1E-2").AsNumber().Should().Be(0.01d);
    }

    [Fact]
    public void ThrowsExceptionWhenDepthLimitReachedArrays()
    {
        string json = GenerateDeepNestedArray(65);

        var engine = new Engine();
        var parser = new JsonParser(engine);

        JavaScriptException ex = Invoking(() => parser.Parse(json)).Should().ThrowExactly<JavaScriptException>().Which;
        ex.Message.Should().Be("Max. depth level of JSON reached at position 64");
    }

    [Fact]
    public void ThrowsExceptionWhenDepthLimitReachedObjects()
    {
        string json = GenerateDeepNestedObject(65);

        var engine = new Engine();
        var parser = new JsonParser(engine);

        JavaScriptException ex = Invoking(() => parser.Parse(json)).Should().ThrowExactly<JavaScriptException>().Which;
        ex.Message.Should().Be("Max. depth level of JSON reached at position 320");
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
        parsed["a"].IsObject().Should().BeTrue();
        parsed["b"].IsObject().Should().BeTrue();
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
        parsed["a"].IsArray().Should().BeTrue();
        parsed["b"].IsArray().Should().BeTrue();
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

        JavaScriptException ex = Invoking(() => parser.Parse(json)).Should().ThrowExactly<JavaScriptException>().Which;
        ex.Message.Should().Be("Max. depth level of JSON reached at position 224");
    }

    [Fact]
    public void CustomMaxDepthOfZeroDisallowsObjects()
    {
        var engine = new Engine();
        var parser = new JsonParser(engine, maxDepth: 0);

        JavaScriptException ex = Invoking(() => parser.Parse("{}")).Should().ThrowExactly<JavaScriptException>().Which;
        ex.Message.Should().Be("Max. depth level of JSON reached at position 0");
    }

    [Fact]
    public void CustomMaxDepthOfZeroDisallowsArrays()
    {
        var engine = new Engine();
        var parser = new JsonParser(engine, maxDepth: 0);

        JavaScriptException ex = Invoking(() => parser.Parse("[]")).Should().ThrowExactly<JavaScriptException>().Which;
        ex.Message.Should().Be("Max. depth level of JSON reached at position 0");
    }

    [Fact]
    public void MaxDepthDoesNotInfluencePrimitiveValues()
    {
        var engine = new Engine();
        var parser = new JsonParser(engine, maxDepth: 1);

        ObjectInstance parsed = parser.Parse("{\"a\": 2, \"b\": true, \"c\": null, \"d\": \"test\"}").AsObject();
        parsed["a"].IsNumber().Should().BeTrue();
        parsed["b"].IsBoolean().Should().BeTrue();
        parsed["c"].IsNull().Should().BeTrue();
        parsed["d"].IsString().Should().BeTrue();
    }

    [Fact]
    public void MaxDepthGetsUsedFromEngineOptionsConstraints()
    {
        var engine = new Engine(options => options.MaxJsonParseDepth(0));
        var parser = new JsonParser(engine);

        Invoking(() => parser.Parse("[]")).Should().ThrowExactly<JavaScriptException>();
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

        ok.Should().BeTrue();
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

        ok.Should().BeTrue();
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

        ok.Should().BeTrue();
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

        ok.Should().BeTrue();
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

        keys.Should().Be(expectedKeys);
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

        ok.Should().BeTrue();
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

        ok.Should().BeTrue();
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

        ok.Should().BeTrue();
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

        ok.Should().BeTrue();
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

        ok.Should().BeTrue();
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

        ok.Should().BeTrue();
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

        ok.Should().BeTrue();
    }

    [Fact]
    public void ParsedRecordsFromSameDocumentShareShape()
    {
        var engine = new Engine();
        var array = engine.Evaluate("""JSON.parse('[{"a":1,"b":2},{"a":3,"b":4}]')""").AsArray();
        var first = array[0].Should().BeOfType<JsObject>().Which;
        var second = array[1].Should().BeOfType<JsObject>().Which;

        first.ShapeOf.Should().NotBeNull();
        second.ShapeOf.Should().BeSameAs(first.ShapeOf);
    }

    [Fact]
    public void ParsedRecordsRoundTripThroughStringify()
    {
        var engine = new Engine();
        var ok = engine.Evaluate("""
            JSON.stringify(JSON.parse('[{"a":1,"b":"x"},{"a":2,"b":"y"}]')) === '[{"a":1,"b":"x"},{"a":2,"b":"y"}]';
            """).AsBoolean();

        ok.Should().BeTrue();
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

        ok.Should().BeTrue();
    }

    [Fact]
    public void CanBulkScanStringLiteralsWithEscapesAtEdges()
    {
        var parser = new JsonParser(new Engine());

        // escape at the very start, in the middle and at the end of the bulk run
        parser.Parse("\"\\nabc\"").AsString().Should().Be("\nabc");
        parser.Parse("\"abc\\ndef\"").AsString().Should().Be("abc\ndef");
        parser.Parse("\"abc\\n\"").AsString().Should().Be("abc\n");

        // every simple escape back-to-back: JSON "\"\\\/\n\r\t\b\f"
        parser.Parse("\"\\\"\\\\\\/\\n\\r\\t\\b\\f\"").AsString().Should().Be("\"\\/\n\r\t\b\f");

        // \uXXXX escapes (BMP)
        parser.Parse("\"\\u0041\\u00e9\\u4e2d\"").AsString().Should().Be("Aé中");

        // a run with no escapes at all takes the pure bulk path
        parser.Parse("\"plain text with spaces\"").AsString().Should().Be("plain text with spaces");
    }

    [Fact]
    public void CanBulkScanStringsCrossingInternalBufferBoundary()
    {
        // ValueStringBuilder starts with a 64-char stack buffer; these inputs force it to grow
        // while the bulk Append copies whole spans.
        var parser = new JsonParser(new Engine());

        // a single content run far longer than the 64-char buffer
        var long1 = new string('a', 200);
        parser.Parse("\"" + long1 + "\"").AsString().Should().Be(long1);

        // escape just before the boundary, then a long run after it
        var before = new string('a', 60);
        var after = new string('b', 60);
        parser.Parse("\"" + before + "\\n" + after + "\"").AsString().Should().Be(before + "\n" + after);

        // escape just after the boundary
        var before3 = new string('c', 70);
        var after3 = new string('d', 5);
        parser.Parse("\"" + before3 + "\\t" + after3 + "\"").AsString().Should().Be(before3 + "\t" + after3);

        // many escapes interspersed straddling the boundary
        var json = new System.Text.StringBuilder("\"");
        var expected = new System.Text.StringBuilder();
        for (var i = 0; i < 100; i++)
        {
            json.Append('x').Append("\\n");
            expected.Append('x').Append('\n');
        }
        json.Append('"');
        parser.Parse(json.ToString()).AsString().Should().Be(expected.ToString());
    }

    [Fact]
    public void UnicodeLineSeparatorsAreValidInsideStrings()
    {
        // the JSON grammar permits any code point except '"', '\' and control characters (< 0x20)
        // raw inside a string, including the Unicode line separators U+2028 / U+2029 (which V8
        // accepts too); they historically terminated the scan with an UnexpectedEOS error
        var parser = new JsonParser(new Engine());

        parser.Parse("\"ab\u2028cd\"").AsString().Should().Be("ab\u2028cd");
        parser.Parse("\"ab\u2029cd\"").AsString().Should().Be("ab\u2029cd");
    }

    [Fact]
    public void EscapedControlCharactersAreValidInPropertyKeys()
    {
        // any string is a valid member key; escaped control characters were rejected in keys
        // while being accepted in values
        var parser = new JsonParser(new Engine());

        parser.Parse("{\"\\u0001\":1}").AsObject().Get("\u0001").AsNumber().Should().Be(1);
        parser.Parse("{\"a\\u0000b\":2}").AsObject().Get("a\u0000b").AsNumber().Should().Be(2);

        // raw (unescaped) control characters keep being rejected, in keys and values alike
        Invoking(() => parser.Parse("{\"\u0001\":1}")).Should().Throw<JavaScriptException>();
    }

    [Theory]
    [InlineData("-09")]
    [InlineData("-00")]
    [InlineData("-01.5")]
    [InlineData("1.")]
    [InlineData("1.e3")]
    [InlineData("-2.")]
    public void InvalidNumberFormsAreRejected(string json)
    {
        // int = zero / (digit1-9 *DIGIT) - also after a sign; frac = decimal-point 1*DIGIT
        var parser = new JsonParser(new Engine());
        Invoking(() => parser.Parse(json)).Should().Throw<JavaScriptException>();
    }

    [Theory]
    [InlineData("-0", -0.0)]
    [InlineData("0", 0.0)]
    [InlineData("-0.5", -0.5)]
    [InlineData("10.25", 10.25)]
    [InlineData("-0e2", -0.0)]
    public void ValidNumberFormsKeepParsing(string json, double expected)
    {
        var parser = new JsonParser(new Engine());
        var value = parser.Parse(json).AsNumber();
        value.Should().Be(expected);
#if !NETFRAMEWORK
        // .NET Framework's double.Parse loses the sign of negative zero (fixed in .NET Core 3.0)
        double.IsNegativeInfinity(1 / value).Should().Be(double.IsNegativeInfinity(1 / expected));
#endif
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
            expected.Should().Be(actual, $"Bit mismatch for '{json}': expected 0x{expected:X16}, actual 0x{actual:X16}");
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
        actual.Should().Be(expected);
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
        BitConverter.DoubleToInt64Bits(parser.Parse(json).AsNumber()).Should().Be(expected);
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
            bits.Should().NotBe(positiveZeroBits);
        }
        else
        {
            bits.Should().Be(positiveZeroBits);
        }
    }

    [Fact]
    public void RepeatedStringValuesShareOneInstanceAcrossArraysAndObjects()
    {
        var parser = new JsonParser(new Engine());

        var arr = parser.Parse("""["alpha","alpha","beta","alpha"]""").AsArray();
        arr.Get("0").AsString().Should().Be("alpha");
        arr.Get("1").Should().BeSameAs(arr.Get("0"));
        arr.Get("3").Should().BeSameAs(arr.Get("0"));
        arr.Get("2").AsString().Should().Be("beta");
        arr.Get("2").Should().NotBeSameAs(arr.Get("0"));

        var obj = parser.Parse("""{"a":"repeat","b":"repeat","c":"other"}""").AsObject();
        obj.Get("b").Should().BeSameAs(obj.Get("a"));
        obj.Get("a").AsString().Should().Be("repeat");
        obj.Get("c").AsString().Should().Be("other");

        // Values interned once per parse are shared across nested arrays and objects within that parse.
        var root = parser.Parse("""{"list":["x-marker","x-marker"],"val":"x-marker"}""").AsObject();
        var list = root.Get("list").AsArray();
        list.Get("1").Should().BeSameAs(list.Get("0"));
        root.Get("val").Should().BeSameAs(list.Get("0"));
    }

    [Fact]
    public void EscapedAndUnescapedValuesWithSameContentAreEqualAndInterned()
    {
        var parser = new JsonParser(new Engine());

        // "AB" written three ways: plain, fully-escaped, and partially-escaped. All decode to the same
        // content, so interning (which keys off the DECODED span) must return one shared instance.
        var arr = parser.Parse("""["AB","AB","AB"]""").AsArray();
        arr.Get("0").AsString().Should().Be("AB");
        arr.Get("1").AsString().Should().Be("AB");
        arr.Get("2").AsString().Should().Be("AB");
        arr.Get("1").Should().BeSameAs(arr.Get("0"));
        arr.Get("2").Should().BeSameAs(arr.Get("0"));

        // An escape sequence that decodes to content differing only by the escape must not collide.
        var arr2 = parser.Parse("""["line\nbreak","line\nbreak","linebreak"]""").AsArray();
        arr2.Get("0").AsString().Should().Be("line\nbreak");
        arr2.Get("1").Should().BeSameAs(arr2.Get("0"));
        arr2.Get("2").Should().NotBeSameAs(arr2.Get("0"));
        arr2.Get("2").AsString().Should().Be("linebreak");
    }

    [Fact]
    public void EmptyAndSingleCharacterValuesParseAndReuseCaches()
    {
        var parser = new JsonParser(new Engine());

        var arr = parser.Parse("""["","","x","x","y"]""").AsArray();
        arr.Get("0").AsString().Should().Be("");
        // Empty routes through JsString.Create -> the shared JsString.Empty singleton.
        arr.Get("0").Should().BeSameAs(JsString.Empty);
        arr.Get("1").Should().BeSameAs(arr.Get("0"));
        arr.Get("2").AsString().Should().Be("x");
        arr.Get("3").Should().BeSameAs(arr.Get("2"));
        arr.Get("4").AsString().Should().Be("y");
        arr.Get("4").Should().NotBeSameAs(arr.Get("2"));
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
            arr.Get(i.ToString(CultureInfo.InvariantCulture)).AsString().Should().Be("value_" + i);
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
        arr.Get("0").AsString().Should().Be(longVal);
        arr.Get("1").AsString().Should().Be(longVal);
        arr.Get("1").Should().NotBeSameAs(arr.Get("0"));
    }

    [Fact]
    public void InternTableIsResetBetweenParses()
    {
        // A value interned in one parse must not leak into the next parse's identity checks; each parse
        // starts from a cleared table, so the same content produces a fresh instance on the second call.
        var parser = new JsonParser(new Engine());
        var first = parser.Parse("""["shared-token"]""").AsArray().Get("0");
        var second = parser.Parse("""["shared-token"]""").AsArray().Get("0");
        first.AsString().Should().Be("shared-token");
        second.AsString().Should().Be(first.AsString());
        second.Should().NotBeSameAs(first);
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
        actual.Should().Be(expected);
    }

    [Fact]
    public void UnexpectedTrailingNumberTokenReportsItsRawText()
    {
        // Number tokens no longer carry an eager Text string; the diagnostic reconstructs the raw text
        // from the token range. Lock in that the reported token is byte-identical to the source.
        var parser = new JsonParser(new Engine());
        var ex = Invoking(() => parser.Parse("1 23")).Should().Throw<JavaScriptException>().Which;
        ex.Message.Should().Be("Unexpected token '23' in JSON at position 2");
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
