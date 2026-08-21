using Jint.Constraints;
using Jint.Native;
using Jint.Native.Json;
using Jint.Runtime;
using Jint.Runtime.Interop;

namespace Jint.Tests.PublicInterface;

public class HostResultLimitsTests
{
    private const string ConcurrentUseMessage = "*already in use by another thread or has an asynchronous operation in progress*";

    [Fact]
    public void DefaultConversionRemainsUnlimitedAndCompatible()
    {
        var options = new Options();
        using var engine = new Engine(options);
        var value = engine.Evaluate("({ name: 'jint', nested: [1, true, null] })");

        var result = engine.Advanced.ConvertResult(value)
            .Should().BeAssignableTo<IDictionary<string, object>>().Which;

        result["name"].Should().Be("jint");
        result["nested"].Should().BeEquivalentTo(new object[] { 1d, true, null });
        options.ResultLimits.Should().BeSameAs(ResultLimits.Unlimited);
    }

    [Fact]
    public void DepthBoundaryIsInclusive()
    {
        using var engine = new Engine();
        var value = engine.Evaluate("({ child: { value: 1 } })");

        engine.Advanced.ConvertResult(value, Limits(maxDepth: 2)).Should().NotBeNull();
        AssertLimit(
            () => engine.Advanced.ConvertResult(value, Limits(maxDepth: 1)),
            ResultLimit.Depth,
            maximum: 1,
            observed: 2);
    }

    [Fact]
    public void LeafObjectsDoNotConsumeContainerDepth()
    {
        using var engine = new Engine();
        var value = engine.Evaluate("({ date: new Date(0), boxed: new Number(1), regexp: /x/ })");

        engine.Advanced.ConvertResult(value, Limits(maxDepth: 1)).Should().NotBeNull();
    }

    [Fact]
    public void PropertyBoundaryIsCheckedBeforeGettersRun()
    {
        using var engine = new Engine();
        var value = engine.Evaluate("""
            globalThis.reads = 0;
            ({ get a() { reads++; return 1; }, get b() { reads++; return 2; }, get c() { reads++; return 3; } })
            """);

        engine.Advanced.ConvertResult(value, Limits(maxPropertyCount: 3)).Should().NotBeNull();
        engine.GetValue("reads").AsNumber().Should().Be(3);

        engine.SetValue("reads", 0);
        AssertLimit(
            () => engine.Advanced.ConvertResult(value, Limits(maxPropertyCount: 2)),
            ResultLimit.PropertyCount,
            maximum: 2,
            observed: 3);
        engine.GetValue("reads").AsNumber().Should().Be(0);
    }

    [Fact]
    public void StringAndAggregateCharacterLimitsAreIndependent()
    {
        using var engine = new Engine();
        var value = engine.Evaluate("({ a: '1234', b: '5678' })");

        AssertLimit(
            () => engine.Advanced.ConvertResult(value, Limits(maxStringLength: 3)),
            ResultLimit.StringLength,
            maximum: 3,
            observed: 4);
        AssertLimit(
            () => engine.Advanced.ConvertResult(value, Limits(maxOutputCharacters: 9)),
            ResultLimit.OutputCharacters,
            maximum: 9,
            observed: 10);
    }

    [Fact]
    public void ArraysAndTypedArraysAreRejectedBeforeOutputAllocation()
    {
        using var engine = new Engine();

        AssertLimit(
            () => engine.Advanced.ConvertResult(
                engine.Evaluate("[1, 2, 3]"),
                Limits(maxPropertyCount: 2)),
            ResultLimit.PropertyCount,
            maximum: 2,
            observed: 3);

        AssertLimit(
            () => engine.Advanced.ConvertResult(
                engine.Evaluate("new Uint32Array(3)"),
                Limits(maxOutputBytes: 11)),
            ResultLimit.OutputBytes,
            maximum: 11,
            observed: 12);
    }

    [Fact]
    public void BinaryByteLimitIsCumulativeAndArrayBuffersAreCopied()
    {
        using var engine = new Engine();
        var value = engine.Evaluate("""
            globalThis.buffer = new Uint8Array([1, 2, 3]).buffer;
            ({ first: buffer, second: new Uint8Array([4, 5, 6]).buffer })
            """);

        AssertLimit(
            () => engine.Advanced.ConvertResult(value, Limits(maxOutputBytes: 5)),
            ResultLimit.OutputBytes,
            maximum: 5,
            observed: 6);

        var bytes = (byte[]) engine.Advanced.ConvertResult(engine.GetValue("buffer"));
        bytes[0] = 99;
        engine.Evaluate("new Uint8Array(buffer)[0]").AsNumber().Should().Be(1);
    }

    [Fact]
    public void FunctionsAndSymbolsAreNotExportedAsEngineAffineValues()
    {
        using var engine = new Engine();

        Invoking(() => engine.Advanced.ConvertResult(engine.Evaluate("(function () {})")))
            .Should().ThrowExactly<NotSupportedException>();
        Invoking(() => engine.Advanced.ConvertResult(engine.Evaluate("Symbol('x')")))
            .Should().ThrowExactly<NotSupportedException>();
    }

    [Fact]
    public void MapsAndSetsConvertToDetachedClrCollections()
    {
        using var engine = new Engine();
        var value = engine.Evaluate("""
            ({
                map: new Map([['a', 1], ['b', { value: 2 }]]),
                set: new Set(['x', 'y'])
            })
            """);

        var result = (Dictionary<string, object>) engine.Advanced.ConvertResult(value, Limits(maxPropertyCount: 10));
        var map = result["map"].Should()
            .BeAssignableTo<List<KeyValuePair<object, object>>>().Which;
        var set = result["set"].Should().BeAssignableTo<object[]>().Which;

        map.Should().HaveCount(2);
        map[0].Key.Should().Be("a");
        map[1].Value.Should().BeAssignableTo<IDictionary<string, object>>();
        set.Should().Equal("x", "y");
    }

    [Fact]
    public void CyclesAreRejected()
    {
        using var engine = new Engine();
        var value = engine.Evaluate("var value = {}; value.self = value; value");

        Invoking(() => engine.Advanced.ConvertResult(value, ResultLimits.Conservative))
            .Should().ThrowExactly<JavaScriptException>()
            .WithMessage("*Cyclic reference detected*");
    }

    [Fact]
    public void ProxyKeysAreCountedBeforeProxyGetsRun()
    {
        using var engine = new Engine();
        var value = engine.Evaluate("""
            globalThis.gets = 0;
            new Proxy({ a: 1, b: 2, c: 3 }, {
                get(target, key, receiver) {
                    gets++;
                    return Reflect.get(target, key, receiver);
                }
            })
            """);

        AssertLimit(
            () => engine.Advanced.ConvertResult(value, Limits(maxPropertyCount: 2)),
            ResultLimit.PropertyCount,
            maximum: 2,
            observed: 3);
        engine.GetValue("gets").AsNumber().Should().Be(0);
    }

    [Fact]
    public void ProxiedArraysPreserveArrayOutputAndRunGetTraps()
    {
        using var engine = new Engine();
        var value = engine.Evaluate("""
            globalThis.gets = 0;
            new Proxy([1, 2], {
                get(target, key, receiver) {
                    gets++;
                    return Reflect.get(target, key, receiver);
                }
            })
            """);

        engine.Advanced.ConvertResult(value, Limits(maxDepth: 1, maxPropertyCount: 2))
            .Should().BeEquivalentTo(new object[] { 1d, 2d });
        engine.GetValue("gets").AsNumber().Should().Be(3, "length and both elements are read through the proxy");
    }

    [Fact]
    public void GetterExecutionUsesEngineConstraints()
    {
        using var engine = new Engine(options => options.MaxStatements(100));
        var value = engine.Evaluate("({ get value() { while (true) {} } })");

        Invoking(() => engine.Advanced.ConvertResult(value, ResultLimits.Conservative))
            .Should().ThrowExactly<StatementsCountOverflowException>();
    }

    [Fact]
    public void ConstraintCadenceSpansManySmallContainers()
    {
        var constraint = new ArmedConstraint();
        using var engine = new Engine(new Options().Constraint(constraint));
        var value = engine.Evaluate("""
            var node = { value: 0 };
            for (var i = 0; i < 14; i++) {
                node = { left: node, right: node };
            }
            node;
            """);
        constraint.Armed = true;

        Invoking(() => engine.Advanced.ConvertResult(
                value,
                Limits(maxDepth: 20, maxPropertyCount: 100_000)))
            .Should().ThrowExactly<InvalidOperationException>()
            .WithMessage("conversion constraint checked");
    }

    [Fact]
    public void PropertyCountBoundsRepeatedSharedReferences()
    {
        using var engine = new Engine();
        var value = engine.Evaluate("""
            var leaf = { value: 1 };
            ({ first: leaf, second: leaf });
            """);

        AssertLimit(
            () => engine.Advanced.ConvertResult(value, Limits(maxPropertyCount: 3)),
            ResultLimit.PropertyCount,
            maximum: 3,
            observed: 4);
    }

    [Fact]
    public void HostWrappersReturnTheirExistingTargetWithoutWalkingIt()
    {
        using var engine = new Engine();
        var target = new HostPayload { Value = "unbounded host-owned value" };
        var wrapper = ObjectWrapper.Create(engine, target);

        engine.Advanced.ConvertResult(wrapper, new ResultLimits(maxDepth: 0, maxPropertyCount: 0))
            .Should().BeSameAs(target);
    }

    [Fact]
    public void JavaScriptErrorRenderingCanBeBounded()
    {
        using var engine = new Engine();
        var exception = Invoking(() => engine.Evaluate("throw new Error('1234')"))
            .Should().ThrowExactly<JavaScriptException>().Which;

        AssertLimit(
            () => exception.GetJavaScriptErrorString(Limits(maxStringLength: 3)),
            ResultLimit.StringLength,
            maximum: 3,
            observed: 4);
    }

    [Fact]
    public void JavaScriptErrorStackAccessUsesEngineConstraints()
    {
        using var engine = new Engine(options => options.MaxStatements(100));
        var error = engine.Evaluate("""
            var error = new Error("x");
            Object.defineProperty(error, "stack", { get() { while (true) {} } });
            error;
            """);
        var exception = new JavaScriptException(error);

        Invoking(() => exception.GetJavaScriptErrorString(ResultLimits.Conservative))
            .Should().ThrowExactly<StatementsCountOverflowException>();
    }

    [Fact]
    public async Task PublicResultBoundariesRejectConcurrentUseAndRecover()
    {
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        using var engine = new Engine();
        var value = engine.Evaluate("({ value: 42 })");
        var serializer = new JsonSerializer(engine);
        var exception = new JavaScriptException(engine.Evaluate("new Error('failure')"));
        engine.SetValue("block", new Action(() =>
        {
            entered.Set();
            release.Wait();
        }));
        var running = Task.Run(() => engine.Execute("block()"));

        entered.Wait(TimeSpan.FromSeconds(10)).Should().BeTrue();
        try
        {
            Invoking(() => engine.Advanced.ConvertResult(value))
                .Should().Throw<InvalidOperationException>()
                .WithMessage(ConcurrentUseMessage);
            Invoking(() => serializer.Serialize(value))
                .Should().Throw<InvalidOperationException>()
                .WithMessage(ConcurrentUseMessage);
            Invoking(() => exception.GetJavaScriptErrorString(ResultLimits.Conservative))
                .Should().Throw<InvalidOperationException>()
                .WithMessage(ConcurrentUseMessage);
        }
        finally
        {
            release.Set();
        }

        await running;
        engine.Advanced.ConvertResult(value).Should().NotBeNull();
        serializer.Serialize(value).AsString().Should().Be("""{"value":42}""");
    }

    [Fact]
    public void ConversionAndSerializationAllowSameOwnerCallbackReentry()
    {
        using var engine = new Engine();
        engine.SetValue("reenter", new Func<int>(() => (int) engine.Evaluate("40 + 2").AsNumber()));
        var value = engine.Evaluate("""
            new Proxy({}, {
                ownKeys() { return ["value"]; },
                getOwnPropertyDescriptor() { return { enumerable: true, configurable: true }; },
                get() { return reenter(); }
            })
            """);

        var converted = (Dictionary<string, object>) engine.Advanced.ConvertResult(value);
        converted["value"].Should().Be(42d);
        new JsonSerializer(engine).Serialize(value).AsString().Should().Be("""{"value":42}""");
    }

    [Fact]
    public void OperationDeadlineSpansEvaluationAndConversion()
    {
        var deadline = new OperationDeadlineConstraint();
        using var engine = new Engine(options => options.Constraint(deadline));
        engine.SetValue("pause", new Action(() => Thread.Sleep(250)));
        engine.SetValue("noop", new Action(() => { }));

        // Warm parsing, the interop call path and ConvertResult before the clock starts. The budget
        // leaves only 150ms of headroom over the first pause, and on .NET Framework a cold first
        // evaluation costs more than that often enough to make the Evaluate below throw the
        // TimeoutException this test means to observe from the conversion instead.
        engine.Advanced.ConvertResult(engine.Evaluate("noop(); ({ get value() { noop(); return 42; } })"));

        deadline.Begin(TimeSpan.FromMilliseconds(400));
        try
        {
            var value = engine.Evaluate("pause(); ({ get value() { pause(); return 42; } })");

            Invoking(() => engine.Advanced.ConvertResult(value))
                .Should().ThrowExactly<TimeoutException>();
        }
        finally
        {
            deadline.End();
        }

        engine.Evaluate("1 + 1").AsNumber().Should().Be(2);
    }

    [Fact]
    public void ExplicitMemoryOperationSpansEvaluationAndConversion()
    {
        const int allocationSize = 2_000_000;
        var allocations = new List<byte[]>();
        using var engine = new Engine(options => options.LimitMemory(3_500_000));
        engine.SetValue("allocate", new Action(() => allocations.Add(new byte[allocationSize])));
        var memory = engine.Constraints.Find<MemoryLimitConstraint>()!;

        memory.Begin();
        try
        {
            var value = engine.Evaluate("allocate(); ({ get value() { allocate(); return 42; } })");

            Invoking(() => engine.Advanced.ConvertResult(value))
                .Should().ThrowExactly<MemoryLimitExceededException>();
        }
        finally
        {
            memory.End();
        }

        engine.Advanced.ConvertResult(engine.Evaluate("({ value: 42 })")).Should().NotBeNull();
    }

    [Fact]
    public void ModuleNamespaceCanBeConvertedUnderResultLimits()
    {
        using var engine = new Engine();
        engine.Modules.Add("result", "export const answer = 42; export const nested = { ok: true };");

        var module = engine.Modules.Import("result");
        var converted = (Dictionary<string, object>) engine.Advanced.ConvertResult(
            module,
            Limits(maxDepth: 2, maxPropertyCount: 4));

        converted["answer"].Should().Be(42d);
        converted["nested"].Should().BeAssignableTo<IDictionary<string, object>>();
    }

    [Fact]
    public void FailedConversionReleasesStateForRetry()
    {
        using var engine = new Engine();
        var value = engine.Evaluate("({ a: 1, b: 2 })");

        AssertLimit(
            () => engine.Advanced.ConvertResult(value, Limits(maxPropertyCount: 1)),
            ResultLimit.PropertyCount,
            maximum: 1,
            observed: 2);

        engine.Advanced.ConvertResult(value, Limits(maxPropertyCount: 2)).Should().NotBeNull();
        engine.Evaluate("1 + 1").AsNumber().Should().Be(2);
    }

    [Fact]
    public void ResultLimitFromClrCallbackRemainsFatalWhenClrExceptionsAreCatchable()
    {
        using var engine = new Engine(options => options.CatchClrExceptions());
        var value = engine.Evaluate("[1, 2]");
        engine.SetValue("convert", new Action(() =>
            engine.Advanced.ConvertResult(value, Limits(maxPropertyCount: 1))));

        Invoking(() => engine.Evaluate("""
            try {
                convert();
            } catch {
                globalThis.caught = true;
            }
            """))
            .Should().ThrowExactly<ResultLimitExceededException>();
        engine.GetValue("caught").Should().BeUndefined();
    }

    private static ResultLimits Limits(
        int maxDepth = int.MaxValue,
        long maxPropertyCount = long.MaxValue,
        int maxStringLength = int.MaxValue,
        long maxOutputCharacters = long.MaxValue,
        long maxOutputBytes = long.MaxValue)
        => new(maxDepth, maxPropertyCount, maxStringLength, maxOutputCharacters, maxOutputBytes);

    private static void AssertLimit(
        Action action,
        ResultLimit limit,
        long maximum,
        long observed)
    {
        var exception = Invoking(action).Should().ThrowExactly<ResultLimitExceededException>().Which;
        exception.Limit.Should().Be(limit);
        exception.Maximum.Should().Be(maximum);
        exception.Observed.Should().Be(observed);
    }

    private sealed class HostPayload
    {
        public string Value { get; set; } = "";
    }

    private sealed class ArmedConstraint : Constraint
    {
        public bool Armed { get; set; }

        public override void Check()
        {
            if (Armed)
            {
                throw new InvalidOperationException("conversion constraint checked");
            }
        }

        public override void Reset()
        {
        }
    }
}
