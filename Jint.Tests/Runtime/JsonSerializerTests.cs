using Jint.Native;
using Jint.Native.Json;
using Jint.Runtime;
using System.Buffers;

namespace Jint.Tests.Runtime;

public class JsonSerializerTests
{
    [Fact]
    public void ResultLimitsAreInclusiveAndDefaultsRemainCompatible()
    {
        using var engine = new Engine();
        var value = engine.Evaluate("({ a: [1, 2], b: 'text' })");
        var expected = "{\"a\":[1,2],\"b\":\"text\"}";

        new JsonSerializer(engine).Serialize(value).AsString().Should().Be(expected);
        new JsonSerializer(engine).SerializeWithLimits(
            value,
            new ResultLimits(
                maxDepth: 2,
                maxPropertyCount: 4,
                maxStringLength: 4,
                maxOutputCharacters: expected.Length)).AsString().Should().Be(expected);
    }

    [Fact]
    public void ReentrantUseOfTheSameInstanceCannotReplaceOuterLimits()
    {
        var engine = new Engine();
        var serializer = new JsonSerializer(engine);
        var nestedWasRejected = false;
        engine.SetValue("reenter", new Func<string>(() =>
        {
            try
            {
                serializer.SerializeWithLimits(new JsString("unbounded"), ResultLimits.Unlimited);
            }
            catch (InvalidOperationException)
            {
                nestedWasRejected = true;
            }

            return "1234";
        }));
        var value = engine.Evaluate("({ toJSON() { return reenter(); } })");

        Invoking(() => serializer.SerializeWithLimits(value, new ResultLimits(maxStringLength: 3)))
            .Should().ThrowExactly<ResultLimitExceededException>();
        nestedWasRejected.Should().BeTrue();
        serializer.Serialize(new JsString("ok")).AsString().Should().Be("\"ok\"");
    }

    [Fact]
    public void JsonLimitsDepthPropertiesStringsAndCharacters()
    {
        using var engine = new Engine();
        var serializer = new JsonSerializer(engine);

        AssertLimit(
            () => serializer.SerializeWithLimits(engine.Evaluate("({ a: { b: 1 } })"), new ResultLimits(maxDepth: 1)),
            ResultLimit.Depth);
        AssertLimit(
            () => serializer.SerializeWithLimits(engine.Evaluate("[1, 2, 3]"), new ResultLimits(maxPropertyCount: 2)),
            ResultLimit.PropertyCount);
        AssertLimit(
            () => serializer.SerializeWithLimits(new JsString("1234"), new ResultLimits(maxStringLength: 3)),
            ResultLimit.StringLength);
        AssertLimit(
            () => serializer.SerializeWithLimits(new JsString("1234"), new ResultLimits(maxOutputCharacters: 5)),
            ResultLimit.OutputCharacters);
    }

    [Fact]
    public void EscapingIsCountedBeforeTheQuotedStringIsAppended()
    {
        using var engine = new Engine();
        var value = new JsString(new string('\0', 20));

        AssertLimit(
            () => new JsonSerializer(engine).SerializeWithLimits(
                value,
                new ResultLimits(maxOutputCharacters: 100)),
            ResultLimit.OutputCharacters);
    }

    [Fact]
    public void StringLengthIsCheckedBeforeAConcatenatedStringIsFlattened()
    {
        using var engine = new Engine();
        var value = (JsString.ConcatenatedString) engine.Evaluate("""
            var values = ["a"];
            values[0] += "b";
            values[0] += "c";
            values[0];
            """);
        value._value.Should().Be("ab");

        AssertLimit(
            () => new JsonSerializer(engine).SerializeWithLimits(
                value,
                new ResultLimits(maxStringLength: 2)),
            ResultLimit.StringLength);
        value._value.Should().Be("ab");
    }

    [Fact]
    public void BoxedStringsUseObservableStringCoercion()
    {
        using var engine = new Engine();

        engine.Evaluate("""
            var value = new String("original");
            value.toString = function () { return "overridden"; };
            JSON.stringify(value);
            """).AsString().Should().Be("\"overridden\"");

        engine.Evaluate("""
            var key = new String("a");
            key.toString = function () { return "selected"; };
            JSON.stringify({ a: 1, selected: 2 }, [key]);
            """).AsString().Should().Be("{\"selected\":2}");
    }

    [Fact]
    public void PropertyLimitFiresBeforeJsonGetter()
    {
        using var engine = new Engine();
        var value = engine.Evaluate("""
            globalThis.reads = 0;
            ({ get a() { reads++; return 1; }, get b() { reads++; return 2; } })
            """);

        AssertLimit(
            () => new JsonSerializer(engine).SerializeWithLimits(value, new ResultLimits(maxPropertyCount: 1)),
            ResultLimit.PropertyCount);
        engine.GetValue("reads").AsNumber().Should().Be(0);
    }

    [Fact]
    public void Utf8LimitIsExactAndWriterIsUntouchedOnFailure()
    {
        using var engine = new Engine();
        var serializer = new JsonSerializer(engine);
        var value = new JsString("é");
        var writer = new CountingBufferWriter();

        serializer.Serialize(value, writer, new ResultLimits(maxOutputBytes: 4)).Should().BeTrue();
        writer.WrittenCount.Should().Be(4);

        writer = new CountingBufferWriter();
        AssertLimit(
            () => serializer.Serialize(value, writer, new ResultLimits(maxOutputBytes: 3)),
            ResultLimit.OutputBytes);
        writer.WrittenCount.Should().Be(0);
    }

    [Fact]
    public void ConfiguredLimitsAlsoBoundScriptStringify()
    {
        using var engine = new Engine(options =>
            options.ResultLimits = new ResultLimits(maxOutputCharacters: 5));

        Invoking(() => engine.Evaluate("try { JSON.stringify([1, 2, 3]); } catch { 'caught'; }"))
            .Should().ThrowExactly<ResultLimitExceededException>();
    }

    [Fact]
    public void ToJsonExecutionUsesEngineConstraints()
    {
        using var engine = new Engine(options => options.MaxStatements(100));
        var value = engine.Evaluate("({ toJSON() { while (true) {} } })");

        Invoking(() => new JsonSerializer(engine).SerializeWithLimits(value, ResultLimits.Conservative))
            .Should().ThrowExactly<StatementsCountOverflowException>();
    }

    private static void AssertLimit(Action action, ResultLimit limit)
    {
        Invoking(action).Should().ThrowExactly<ResultLimitExceededException>()
            .Which.Limit.Should().Be(limit);
    }

    [Fact]
    public void CanStringifyBasicTypes()
    {
        using var engine = new Engine();
        var serializer = new JsonSerializer(engine);

        serializer.Serialize(JsValue.Null).ToString().Should().Be("null");
        serializer.Serialize(JsBoolean.True).ToString().Should().Be("true");
        serializer.Serialize(JsBoolean.False).ToString().Should().Be("false");
        serializer.Serialize(new JsString("")).ToString().Should().Be("\"\"");
        serializer.Serialize(new JsString("abc")).ToString().Should().Be("\"abc\"");
        serializer.Serialize(new JsNumber(1)).ToString().Should().Be("1");
        serializer.Serialize(new JsNumber(0.5)).ToString().Should().Be("0.5");
        serializer.Serialize(new JsObject(engine)).ToString().Should().Be("{}");
        serializer.Serialize(new JsArray(engine)).ToString().Should().Be("[]");

        serializer.Serialize(JsValue.Undefined).Should().BeSameAs(JsValue.Undefined);
    }

    [Fact]
    public void EmptyObjectHasNoLineBreakWithSpaceDefined()
    {
        using var engine = new Engine();
        var serializer = new JsonSerializer(engine);
        serializer.Serialize(new JsObject(engine), JsValue.Undefined, new JsString("  ")).ToString().Should().Be("{}");
    }

    [Fact]
    public void EmptyArrayHasNoLineBreakWithSpaceDefined()
    {
        using var engine = new Engine();
        var serializer = new JsonSerializer(engine);
        serializer.Serialize(new JsArray(engine), JsValue.Undefined, new JsString("  ")).ToString().Should().Be("[]");
    }

    [Fact]
    public void StringCharactersGetEscaped()
    {
        using var engine = new Engine();
        var serializer = new JsonSerializer(engine);

        string actual = serializer.Serialize(new JsString("\"\\\t\r\n\f\r\b\ud834")).ToString();
        actual.Should().Be("\"\\\"\\\\\\t\\r\\n\\f\\r\\b\\ud834\"");
    }

    [Fact]
    public void JsonStringOutputIsIndentedWhenSpacerDefined()
    {
        using var engine = new Engine();
        var serializer = new JsonSerializer(engine);

        JsObject instance = new JsObject(engine);
        instance["a"] = "b";
        instance["b"] = 2;
        instance["c"] = new JsArray(engine, [new JsNumber(4), new JsNumber(5), new JsNumber(6)]);
        instance["d"] = true;

        string actual = serializer.Serialize(instance, JsValue.Undefined, new JsNumber(2)).ToString();
        actual.Should().Be("{\n  \"a\": \"b\",\n  \"b\": 2,\n  \"c\": [\n    4,\n    5,\n    6\n  ],\n  \"d\": true\n}");
    }

    [Fact]
    public void JsonStringOutputIsCompactWithoutSpacer()
    {
        using var engine = new Engine();
        var serializer = new JsonSerializer(engine);

        JsObject instance = new JsObject(engine);
        instance["a"] = "b";
        instance["b"] = 2;
        instance["c"] = new JsArray(engine, [new JsNumber(4), new JsNumber(5), new JsNumber(6)]);
        instance["d"] = true;

        string actual = serializer.Serialize(instance, JsValue.Undefined, JsValue.Undefined).ToString();
        actual.Should().Be("{\"a\":\"b\",\"b\":2,\"c\":[4,5,6],\"d\":true}");
    }

    [Fact]
    public void ArrayWithUndefinedWillBeNull()
    {
        using var engine = new Engine();
        var serializer = new JsonSerializer(engine);

        JsArray array = new JsArray(engine, [JsValue.Undefined, new JsNumber(42)]);
        string actual = serializer.Serialize(array, JsValue.Undefined, JsValue.Undefined).ToString();
        actual.Should().Be("[null,42]");
    }

    [Fact]
    public void ObjectPropertyWithUndefinedWillBeSkipped()
    {
        using var engine = new Engine();
        var serializer = new JsonSerializer(engine);

        JsObject instance = new JsObject(engine);
        instance["a"] = JsValue.Undefined;
        instance["b"] = 42;
        string actual = serializer.Serialize(instance, JsValue.Undefined, JsValue.Undefined).ToString();
        actual.Should().Be("{\"b\":42}");
    }

    [Fact]
    public void NonStringObjectKeyWillSerializedAsString()
    {
        using var engine = new Engine();
        var serializer = new JsonSerializer(engine);

        JsObject instance = new JsObject(engine);
        instance[JsValue.Undefined] = 10;
        instance[JsValue.Null] = 21;
        instance[new JsNumber(10)] = 42;
        string actual = serializer.Serialize(instance, JsValue.Undefined, JsValue.Undefined).ToString();
        actual.Should().Be("{\"10\":42,\"undefined\":10,\"null\":21}");
    }

    [Fact]
    public void InfinityAndNaNGetsSerializedAsNull()
    {
        using var engine = new Engine();
        var serializer = new JsonSerializer(engine);
        JsArray array = new JsArray(engine, [JsNumber.DoubleNegativeInfinity, JsNumber.DoublePositiveInfinity, JsNumber.DoubleNaN]);
        string actual = serializer.Serialize(array, JsValue.Undefined, JsValue.Undefined).ToString();
        actual.Should().Be("[null,null,null]");
    }

    [Fact]
    public void ArrayAsReplacedDictatesPropertiesToSerializer()
    {
        using var engine = new Engine();
        var serializer = new JsonSerializer(engine);

        JsObject instance = new JsObject(engine);
        instance["a"] = 21;
        instance["b"] = 42;
        JsValue replacer = new JsArray(engine, [new JsString("b"), new JsString("z")]);
        string actual = serializer.Serialize(instance, replacer, JsValue.Undefined).ToString();
        actual.Should().Be("{\"b\":42}");
    }

    [Theory]
    [InlineData("test123\n456", "\"test123\\n456\"")]
    [InlineData("test123456\n", "\"test123456\\n\"")]
    [InlineData("\u0002test\u0002", "\"\\u0002test\\u0002\"")]
    [InlineData("\u0002tes\tt\u0002", "\"\\u0002tes\\tt\\u0002\"")]
    [InlineData("t\u0002est\u0002", "\"t\\u0002est\\u0002\"")]
    [InlineData("test😀123456\n", "\"test😀123456\\n\"")]
    public void JsonStringEncodingFormatsContentCorrectly(string inputString, string expectedOutput)
    {
        using var engine = new Engine();
        var serializer = new JsonSerializer(engine);

        string actual = serializer.Serialize(new JsString(inputString)).ToString();
        actual.Should().Be(expectedOutput);
    }

    [Fact]
    public void ReplacerArrayOverParsedShapedRecordsUsesPropertyListOrder()
    {
        // A replacer array installs a PropertyList that dictates both the key set and their order,
        // so shape-mode objects must take the generic path.
        using var engine = new Engine();
        var ok = engine.Evaluate("""
            var a = JSON.parse('[{"a":1,"b":2,"c":3},{"a":4,"b":5,"c":6}]');
            JSON.stringify(a, ["b", "a"]) === '[{"b":2,"a":1},{"b":5,"a":4}]';
            """).AsBoolean();

        ok.Should().BeTrue();
    }

    [Fact]
    public void ToJsonOnShapedObjectIsInvoked()
    {
        using var engine = new Engine();
        var ok = engine.Evaluate("""
            var a = JSON.parse('[{"x":1,"y":2},{"x":3,"y":4}]');
            a[0].toJSON = function (k) { return 'record-' + k; };
            JSON.stringify(a) === '["record-0",{"x":3,"y":4}]';
            """).AsBoolean();

        ok.Should().BeTrue();
    }

    [Fact]
    public void ToJsonMutatingShapedHolderMidSerializationFallsBackSafely()
    {
        // A member value's toJSON deopts the holder mid-serialization (delete converts it to
        // dictionary mode); the remaining snapshotted keys must be read live like the generic path
        // reads its snapshotted key list — the removed key serializes as absent.
        using var engine = new Engine();
        var ok = engine.Evaluate("""
            var o = JSON.parse('{"a":1,"b":2,"c":3}');
            o.b = { toJSON: function () { delete o.c; o.a = 100; return 'B'; } };
            JSON.stringify(o) === '{"a":1,"b":"B"}';
            """).AsBoolean();

        ok.Should().BeTrue();
    }

    [Fact]
    public void ShapedAndDictionaryObjectsSerializeIdentically()
    {
        using var engine = new Engine();
        var ok = engine.Evaluate("""
            var shaped = JSON.parse('{"a":1,"b":"x","c":[1,2],"d":{"e":null},"f":1.5,"g":true}');
            var dict = JSON.parse('{"a":1,"b":"x","c":[1,2],"d":{"e":null},"f":1.5,"g":true}');
            delete dict.g;              // deopts to dictionary mode
            dict.g = true;              // re-added at the same (last) position
            JSON.stringify(shaped) === JSON.stringify(dict)
                && JSON.stringify(shaped, null, 2) === JSON.stringify(dict, null, 2)
                && JSON.stringify(shaped) === '{"a":1,"b":"x","c":[1,2],"d":{"e":null},"f":1.5,"g":true}';
            """).AsBoolean();

        ok.Should().BeTrue();
    }

    [Fact]
    public void UnserializableShapedMembersAreSkipped()
    {
        using var engine = new Engine();
        var ok = engine.Evaluate("""
            var o = JSON.parse('{"a":1,"b":2}');
            o.fn = function () {};      // stays shaped: post-parse adds transition the shape
            o.u = undefined;
            JSON.stringify(o) === '{"a":1,"b":2}';
            """).AsBoolean();

        ok.Should().BeTrue();
    }

    [Fact]
    public void ShapedObjectWithDigitLeadingKeyFallsBackToSpecOrder()
    {
        // A digit-leading key added post-parse lives in the shape, but own-key order places integer
        // indices first, which slot order cannot express — stringify must fall back and sort.
        using var engine = new Engine();
        var ok = engine.Evaluate("""
            var o = JSON.parse('{"b":1,"c":2}');
            o['3'] = 3;
            JSON.stringify(o) === '{"3":3,"b":1,"c":2}' && Object.keys(o).join() === '3,b,c';
            """).AsBoolean();

        ok.Should().BeTrue();
    }

    [Fact]
    public void AReusedSerializerDoesNotCarryReplacerOrSpaceIntoTheNextCall()
    {
        // JSON.stringify allocates a serializer per call, but the type is public and a host may hold one.
        using var engine = new Engine();
        var serializer = new JsonSerializer(engine);

        var instance = new JsObject(engine);
        instance["a"] = 21;
        instance["b"] = 42;

        var replacerArray = new JsArray(engine, [new JsString("b")]);
        serializer.Serialize(instance, replacerArray, new JsNumber(2)).ToString().Should().Be("{\n  \"b\": 42\n}");
        serializer.Serialize(instance).ToString().Should().Be("{\"a\":21,\"b\":42}");

        var replacerFunction = engine.Evaluate("(function (k, v) { return k === 'a' ? undefined : v; })");
        serializer.Serialize(instance, replacerFunction, JsValue.Undefined).ToString().Should().Be("{\"b\":42}");
        serializer.Serialize(instance).ToString().Should().Be("{\"a\":21,\"b\":42}");
    }

    [Fact]
    public void ReplacerFunctionAppliesToShapedObjects()
    {
        using var engine = new Engine();
        var ok = engine.Evaluate("""
            var a = JSON.parse('[{"a":1,"b":2},{"a":3,"b":4}]');
            JSON.stringify(a, function (k, v) { return k === 'b' ? undefined : v; }) === '[{"a":1},{"a":3}]';
            """).AsBoolean();

        ok.Should().BeTrue();
    }

    /// <summary>
    /// A minimal <see cref="IBufferWriter{T}"/> for the byte-writing overloads. Hand-written rather than
    /// <c>ArrayBufferWriter&lt;byte&gt;</c> because that type is internal in the <c>System.Memory</c> package
    /// this project compiles against on .NET Framework, and the API under test takes the interface anyway.
    /// </summary>
    private sealed class CountingBufferWriter : IBufferWriter<byte>
    {
        private byte[] _buffer = new byte[256];

        public int WrittenCount { get; private set; }

        public void Advance(int count) => WrittenCount += count;

        public Memory<byte> GetMemory(int sizeHint = 0)
        {
            Grow(sizeHint);
            return _buffer.AsMemory(WrittenCount);
        }

        public Span<byte> GetSpan(int sizeHint = 0)
        {
            Grow(sizeHint);
            return _buffer.AsSpan(WrittenCount);
        }

        private void Grow(int sizeHint)
        {
            var needed = WrittenCount + Math.Max(sizeHint, 1);
            if (needed <= _buffer.Length)
            {
                return;
            }

            Array.Resize(ref _buffer, Math.Max(needed, _buffer.Length * 2));
        }
    }
}