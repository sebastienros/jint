using Jint.Runtime;
using Jint.Native.Function;

namespace Jint.Tests.Runtime;

public class TailCallOptimizationTests
{
    [Fact]
    public void OptimizesStrictDirectTailRecursion()
    {
        var engine = new Engine();

        var result = engine.Evaluate("""
            "use strict";
            function sum(n, total) {
                return n === 0 ? total : sum(n - 1, total + n);
            }
            sum(10_000, 0);
            """);

        result.Should().Be(50_005_000);
    }

    [Fact]
    public void OptimizesStrictMutualTailRecursion()
    {
        var engine = new Engine();

        var result = engine.Evaluate("""
            "use strict";
            function even(n) {
                return n === 0 || odd(n - 1);
            }
            function odd(n) {
                return n !== 0 && even(n - 1);
            }
            even(10_000);
            """);

        result.Should().Be(true);
    }

    [Fact]
    public void OptimizesStrictConciseArrowTailRecursion()
    {
        var engine = new Engine();

        var result = engine.Evaluate("""
            "use strict";
            let count;
            count = n => n === 0 ? 0 : count(n - 1);
            count(10_000);
            """);

        result.Should().Be(0);
    }

    [Fact]
    public void OptimizesTailRecursionInvokedByHost()
    {
        var engine = new Engine();
        engine.Execute("""
            "use strict";
            function count(n) {
                return n === 0 ? 0 : count(n - 1);
            }
            """);

        engine.Invoke("count", 10_000).Should().Be(0);
    }

    [Fact]
    public void OptimizesTailRecursionFromWarmedRegisterCallSite()
    {
        var engine = new Engine();

        var result = engine.Evaluate("""
            "use strict";
            function count(n) {
                return n === 0 ? 0 : count(n - 1);
            }
            let result;
            for (let i = 0; i < 2; i++) {
                result = count(i === 0 ? 1 : 10_000);
            }
            result;
            """);

        result.Should().Be(0);
    }

    [Fact]
    public void DoesNotOptimizeSloppyTailRecursion()
    {
        var engine = new Engine(options => options.Constraints.MaxRecursionDepth = 20);

        Invoking(() => engine.Evaluate("""
            function recurse(n) {
                return n === 0 ? 0 : recurse(n - 1);
            }
            recurse(100);
            """)).Should().ThrowExactly<RecursionDepthOverflowException>();
    }

    [Fact]
    public void DoesNotOptimizeNonTailRecursion()
    {
        var engine = new Engine(options => options.Constraints.MaxRecursionDepth = 20);

        Invoking(() => engine.Evaluate("""
            "use strict";
            function fibonacci(n) {
                return n < 2 ? n : fibonacci(n - 1) + fibonacci(n - 2);
            }
            fibonacci(100);
            """)).Should().ThrowExactly<RecursionDepthOverflowException>();
    }

    [Fact]
    public void DoesNotMoveTailCallPastFinally()
    {
        var engine = new Engine(options => options.Constraints.MaxRecursionDepth = 20);

        Invoking(() => engine.Evaluate("""
            "use strict";
            function recurse(n) {
                try {
                    return n === 0 ? 0 : recurse(n - 1);
                } finally {
                    globalThis.finallyCount = (globalThis.finallyCount ?? 0) + 1;
                }
            }
            recurse(100);
            """)).Should().ThrowExactly<RecursionDepthOverflowException>();
    }

    [Fact]
    public void DoesNotMoveTailCallPastCatch()
    {
        var engine = new Engine(options => options.Constraints.MaxRecursionDepth = 1);

        var result = engine.Evaluate("""
            "use strict";
            function fail() {
                throw new Error("expected");
            }
            function invoke() {
                try {
                    return fail();
                } catch {
                    return 42;
                }
            }
            invoke();
            """);

        result.Should().Be(42);
    }

    [Fact]
    public void PreservesUsingDisposalOrder()
    {
        var engine = new Engine(options => options.Constraints.MaxRecursionDepth = 1);

        var result = engine.Evaluate("""
            "use strict";
            const log = [];
            function target() {
                log.push("target");
                return 1;
            }
            function invoke() {
                using resource = {
                    [Symbol.dispose]() {
                        log.push("dispose");
                    }
                };
                return target();
            }
            invoke();
            log.join(",");
            """);

        result.Should().Be("target,dispose");
    }

    [Fact]
    public void UsingDeclarationOnlyBlocksFollowingReturns()
    {
        var stack = new Engine().Evaluate("""
            "use strict";
            function target() {
                return new Error("expected").stack;
            }
            function invoke(returnEarly) {
                if (returnEarly) {
                    return target();
                }
                using resource = {
                    [Symbol.dispose]() {}
                };
                return "late";
            }
            invoke(true);
            """).AsString();

        stack.Should().Contain("at target");
        stack.Should().NotContain("at invoke");
    }

    [Fact]
    public void PreservesIteratorCloseOrder()
    {
        var engine = new Engine(options => options.Constraints.MaxRecursionDepth = 1);

        var result = engine.Evaluate("""
            "use strict";
            const log = [];
            const iterable = {
                [Symbol.iterator]() {
                    return {
                        next() {
                            return { value: 1, done: false };
                        },
                        return() {
                            log.push("close");
                            return { done: true };
                        }
                    };
                }
            };
            function target() {
                log.push("target");
                return 1;
            }
            function invoke() {
                for (const value of iterable) {
                    return target();
                }
            }
            invoke();
            log.join(",");
            """);

        result.Should().Be("target,close");
    }

    [Fact]
    public void ResolvesTailCallReturnValueFromConstructor()
    {
        var engine = new Engine(options => options.Constraints.MaxRecursionDepth = 1);

        var result = engine.Evaluate("""
            "use strict";
            function makeResult() {
                return { expected: true };
            }
            function Constructor() {
                return makeResult();
            }
            const value = new Constructor();
            value.expected && !(value instanceof Constructor);
            """);

        result.Should().Be(true);
    }

    [Fact]
    public void AppliesDerivedConstructorReturnValidationAfterTailCall()
    {
        var engine = new Engine(options => options.Constraints.MaxRecursionDepth = 1);

        Invoking(() => engine.Evaluate("""
            function primitive() {
                return 42;
            }
            class Base {}
            class Derived extends Base {
                constructor() {
                    super();
                    return primitive();
                }
            }
            new Derived();
            """)).Should().Throw<JavaScriptException>();
    }

    [Fact]
    public void TailCallFromFramelessCallbackPreservesBuiltinFrame()
    {
        var engine = new Engine();

        var stack = engine.Evaluate("""
            "use strict";
            function target() {
                return new Error("expected").stack;
            }
            function callback(value) {
                return target();
            }
            [1].map(callback)[0];
            """).AsString();

        stack.Should().Contain("at target");
        stack.Should().Contain("at map");
        stack.Should().NotContain("at callback");
    }

    [Fact]
    public void ConstructorTailCallReplacesConstructorFrame()
    {
        var engine = new Engine();

        var stack = engine.Evaluate("""
            "use strict";
            function target() {
                return { stack: new Error("expected").stack };
            }
            function Constructor() {
                return target();
            }
            new Constructor().stack;
            """).AsString();

        stack.Should().Contain("at target");
        stack.Should().NotContain("at Constructor");
    }

    [Fact]
    public void ConstructorTailCallWorksThroughFramelessWrappers()
    {
        var engine = new Engine();

        var result = engine.Evaluate("""
            "use strict";
            function target() {
                return { expected: true };
            }
            function Constructor() {
                return target();
            }
            const ProxyConstructor = new Proxy(Constructor, {});
            const proxyValue = new ProxyConstructor();
            const boundValue = new (Constructor.bind(null))();
            proxyValue.expected && boundValue.expected;
            """);

        result.Should().Be(true);
        engine.CallStack.Count.Should().Be(0);
    }

    [Fact]
    public void BaseConstructorTailCallDoesNotReplaceDerivedFrame()
    {
        var engine = new Engine();

        var result = engine.Evaluate("""
            "use strict";
            function target() {
                return { expected: true };
            }
            class Base {
                constructor() {
                    return target();
                }
            }
            class Derived extends Base {}
            new Derived().expected;
            """);

        result.Should().Be(true);
        engine.CallStack.Count.Should().Be(0);
    }

    [Fact]
    public void TailCallRecoversAfterNestedEvaluationResetsCallStack()
    {
        var engine = new Engine();
        engine.SetValue("resetCallStack", new Action(() =>
        {
            try
            {
                engine.Evaluate("throw new Error('expected')");
            }
            catch (JavaScriptException)
            {
            }
        }));

        var result = engine.Evaluate("""
            "use strict";
            function target() {
                return 42;
            }
            function invoke() {
                resetCallStack();
                return target();
            }
            invoke();
            """);

        result.Should().Be(42);
        engine.CallStack.Count.Should().Be(0);
    }

    [Fact]
    public void ConstructorTailCallRecoversAfterNestedEvaluationResetsCallStack()
    {
        var engine = new Engine();
        engine.SetValue("resetCallStack", new Action(() =>
        {
            try
            {
                engine.Evaluate("throw new Error('expected')");
            }
            catch (JavaScriptException)
            {
            }
        }));

        var result = engine.Evaluate("""
            "use strict";
            function target() {
                return { expected: true };
            }
            function Constructor() {
                resetCallStack();
                return target();
            }
            new Constructor().expected;
            """);

        result.Should().Be(true);
        engine.CallStack.Count.Should().Be(0);
    }

    [Fact]
    public void RecursionLimitFailureLeavesCallStackBalanced()
    {
        var engine = new Engine(options => options.Constraints.MaxRecursionDepth = 0);

        Invoking(() => engine.Evaluate("""
            "use strict";
            function recurse(n) {
                return n === 0 ? 0 : 1 + bridge(n - 1);
            }
            function bridge(n) {
                return recurse(n);
            }
            recurse(100);
            """)).Should().ThrowExactly<RecursionDepthOverflowException>();

        engine.CallStack.Count.Should().Be(0);
    }

    [Fact]
    public void InfiniteStrictTailRecursionHonorsRecursionLimit()
    {
        var engine = new Engine(options => options.Constraints.MaxRecursionDepth = 8);

        Invoking(() => engine.Evaluate("""
            "use strict";
            function recurse() {
                return recurse();
            }
            recurse();
            """)).Should().ThrowExactly<RecursionDepthOverflowException>();

        engine.CallStack.Count.Should().Be(0);
    }

    [Fact]
    public void MultiFunctionTailCycleHonorsRecursionLimit()
    {
        var engine = new Engine(options => options.Constraints.MaxRecursionDepth = 1);

        Invoking(() => engine.Evaluate("""
            "use strict";
            function first() {
                return second();
            }
            function second() {
                return third();
            }
            function third() {
                return first();
            }
            first();
            """)).Should().ThrowExactly<RecursionDepthOverflowException>();

        engine.CallStack.Count.Should().Be(0);
    }

    /// <summary>
    /// A tail call replaces its caller's frame, but the caller's activation is not over while the
    /// trampoline runs — and here the tail-call target leaves it again through a getter, which is not
    /// a tail position and so really does grow the native stack. The limit has to keep seeing the
    /// displaced getter across those re-entries; when it did not, this ran until the process died.
    /// <para>
    /// Each <c>calc</c> is deliberately a definition of its own. The limit counts occurrences of one
    /// function definition, so a stable <c>calc</c> would be counted and would stop the recursion by
    /// itself, leaving the defect untested.
    /// </para>
    /// </summary>
    [Fact]
    public void RecursionLimitFiresWhenATailCallReEntersThroughAGetter()
    {
        var engine = new Engine(options => options.Constraints.MaxRecursionDepth = 20);

        Invoking(() => engine.Evaluate("""
            "use strict";
            var version = 0;
            var entity = {
                get calc() {
                    (0, eval)("function calc() { return entity.calc; } //" + version++);
                    return calc();
                }
            };
            entity.calc;
            """)).Should().ThrowExactly<RecursionDepthOverflowException>();

        engine.CallStack.Count.Should().Be(0);
    }

    /// <summary>
    /// The depth statistic must come back to zero after the limit fires, not only the frame stack. A
    /// retention the trampoline failed to hand back is invisible in <c>CallStack.Count</c> and shows up
    /// only later, as a second run of the same functions overflowing before it has recursed at all.
    /// </summary>
    [Fact]
    public void RecursionLimitFailureLeavesTheDepthStatisticBalanced()
    {
        var engine = new Engine(options => options.Constraints.MaxRecursionDepth = 0);
        engine.Execute("""
            "use strict";
            var stop = false;
            function first() {
                return stop ? 42 : second();
            }
            function second() {
                return first();
            }
            """);

        Invoking(() => engine.Evaluate("first()")).Should().ThrowExactly<RecursionDepthOverflowException>();
        engine.CallStack.Count.Should().Be(0);

        engine.Evaluate("stop = true; first();").Should().Be(42);
    }

    [Fact]
    public void DebugModeKeepsTailCallerFrame()
    {
        var stack = new Engine(options => options.Debugger.Enabled = true).Evaluate("""
            "use strict";
            function target() {
                return new Error("expected").stack;
            }
            function invoke() {
                return target();
            }
            invoke();
            """).AsString();

        stack.Should().Contain("at target");
        stack.Should().Contain("at invoke");
    }

    [Fact]
    public void GeneratorDoesNotReceiveTailCallRequest()
    {
        var result = new Engine().Evaluate("""
            function target() {
                return 42;
            }
            function* invoke() {
                "use strict";
                return target();
            }
            invoke().next().value;
            """);

        result.Should().Be(42);
    }

    [Fact]
    public async Task AsyncFunctionDoesNotReceiveTailCallRequest()
    {
        var result = await new Engine().EvaluateAsync("""
            function target() {
                return 42;
            }
            async function invoke() {
                "use strict";
                return target();
            }
            invoke();
            """);

        result.Should().Be(42);
    }

    [Fact]
    public void SharedPreparedTailCallMarkersArePublishedBeforeHandlerConstruction()
    {
        var prepared = Engine.PrepareScript("""
            "use strict";
            function sum(n, total) {
                return n === 0 ? total : sum(n - 1, total + n);
            }
            sum(2_000, 0);
            """);
        var results = new double[16];

        Parallel.For(0, results.Length, i => results[i] = new Engine().Evaluate(prepared).AsNumber());

        results.Should().AllSatisfy(result => result.Should().Be(2_001_000));
    }

    [Fact]
    public void TailCallRequestCannotMasqueradeAsUndefined()
    {
        var request = new TailCallRequest();

        request.IsUndefined().Should().BeFalse();
        Invoking(request.ToObject).Should().ThrowExactly<InvalidOperationException>();
    }
}
