using System.Threading;
using System.Threading.Tasks;
using Jint.Constraints;
using Jint.Native;
using Jint.Native.ShadowRealm;
using Jint.Runtime.Debugger;
using Jint.Runtime.Modules;

#nullable enable

namespace Jint.Tests.PublicInterface;

public class ParsingLimitTests
{
    [Fact]
    public void ExecuteAndEvaluateEnforceEngineSourceLength()
    {
        var engine = CreateEngine(maxSourceLength: 1);

        engine.Execute("0");
        engine.Evaluate("0").Should().Be(0);

        AssertSourceLimit(() => engine.Execute("00"), 1, 2);
        AssertSourceLimit(() => engine.Evaluate("00"), 1, 2);
    }

    [Fact]
    public void SourceLengthCountsUtf16CodeUnits()
    {
        var engine = CreateEngine(maxSourceLength: 4);

        engine.Evaluate("'😀'").Should().Be("😀");

        var exception = AssertSourceLimit(() => engine.Evaluate("'😀' "), 4, 5);
        exception.Kind.Should().Be(ParsingLimitKind.SourceLength);
    }

    [Fact]
    public void SourceOffsetPaddingCountsBeforeItIsAllocated()
    {
        var options = new ScriptParsingOptions
        {
            MaxSourceLength = 5,
            SourceOffset = Position.From(3, 2),
        };

        new Engine().Evaluate("0", parsingOptions: options).Should().Be(0);

        var exception = AssertSourceLimit(
            () => new Engine().Evaluate("00", parsingOptions: options),
            5,
            6);
        exception.Kind.Should().Be(ParsingLimitKind.SourceLength);
    }

    [Fact]
    public void PerCallOptionsCannotRelaxEngineLimits()
    {
        var engine = CreateEngine(maxSourceLength: 1);
        var parsingOptions = new ScriptParsingOptions { MaxSourceLength = 100 };

        AssertSourceLimit(() => engine.Evaluate("00", parsingOptions: parsingOptions), 1, 2);
    }

    [Fact]
    public void PerCallOptionsCanTightenEngineLimits()
    {
        var engine = CreateEngine(maxSourceLength: 100);
        var parsingOptions = new ScriptParsingOptions { MaxSourceLength = 1 };

        AssertSourceLimit(() => engine.Evaluate("00", parsingOptions: parsingOptions), 1, 2);
    }

    [Fact]
    public void PerCallLimitsFlowIntoEvalAndCannotReuseALooserCacheEntry()
    {
        var payload = string.Join(";", Enumerable.Repeat("0", 30));
        var engine = new Engine().SetValue("payload", payload);
        var loose = new ScriptParsingOptions { MaxSourceLength = 100 };
        var tight = new ScriptParsingOptions { MaxSourceLength = 20 };

        engine.Evaluate("eval(payload)", parsingOptions: loose);
        engine.Evaluate("eval(payload)", parsingOptions: loose);

        AssertSourceLimit(() => engine.Evaluate("eval(payload)", parsingOptions: tight), 20, payload.Length);
    }

    [Fact]
    public void PerCallLimitsFlowIntoFunctionConstructors()
    {
        var payload = new string(' ', 30);
        var engine = new Engine().SetValue("payload", payload);
        var parsingOptions = new ScriptParsingOptions { MaxSourceLength = 25 };

        Invoking(() => engine.Evaluate("new Function(payload)", parsingOptions: parsingOptions))
            .Should().ThrowExactly<ParsingLimitException>()
            .Which.Kind.Should().Be(ParsingLimitKind.SourceLength);
    }

    [Fact]
    public void NodeCountBoundaryIsExactAndResetsForEveryParse()
    {
        var engine = CreateEngine(maxNodeCount: 3);

        engine.Evaluate("0").Should().Be(0);
        engine.Evaluate("0").Should().Be(0);

        var exception = Invoking(() => engine.Evaluate("0 + 1"))
            .Should().ThrowExactly<ParsingLimitException>().Which;
        exception.Kind.Should().Be(ParsingLimitKind.NodeCount);
        exception.Limit.Should().Be(3);
        exception.Actual.Should().Be(4);
    }

    [Fact]
    public void ZeroLimitsHaveDeterministicBoundaries()
    {
        var sourceLimited = CreateEngine(maxSourceLength: 0);
        sourceLimited.Evaluate("").Should().Be(JsValue.Undefined);
        AssertSourceLimit(() => sourceLimited.Evaluate("0"), 0, 1);

        var nodeLimited = CreateEngine(maxNodeCount: 0);
        var exception = Invoking(() => nodeLimited.Evaluate(""))
            .Should().ThrowExactly<ParsingLimitException>().Which;
        exception.Kind.Should().Be(ParsingLimitKind.NodeCount);
        exception.Limit.Should().Be(0);
        exception.Actual.Should().Be(1);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void PrepareScriptEnforcesSourceAndNodeLimits(bool staticAnalysis)
    {
        var sourceOptions = new ScriptPreparationOptions
        {
            StaticAnalysis = staticAnalysis,
            ParsingOptions = new ScriptParsingOptions { MaxSourceLength = 1 },
        };
        AssertSourceLimit(() => Engine.PrepareScript("00", options: sourceOptions), 1, 2);

        var nodeOptions = sourceOptions with
        {
            ParsingOptions = new ScriptParsingOptions { MaxNodeCount = 3 },
        };
        Invoking(() => Engine.PrepareScript("0 + 1", options: nodeOptions))
            .Should().ThrowExactly<ParsingLimitException>()
            .Which.Kind.Should().Be(ParsingLimitKind.NodeCount);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void PrepareModuleEnforcesSourceAndNodeLimits(bool staticAnalysis)
    {
        var sourceOptions = new ModulePreparationOptions
        {
            StaticAnalysis = staticAnalysis,
            ParsingOptions = new ModuleParsingOptions { MaxSourceLength = 1 },
        };
        AssertSourceLimit(() => Engine.PrepareModule("00", options: sourceOptions), 1, 2);

        var nodeOptions = sourceOptions with
        {
            ParsingOptions = new ModuleParsingOptions { MaxNodeCount = 3 },
        };
        Invoking(() => Engine.PrepareModule("0 + 1", options: nodeOptions))
            .Should().ThrowExactly<ParsingLimitException>()
            .Which.Kind.Should().Be(ParsingLimitKind.NodeCount);
    }

    [Fact]
    public void PreparedCodeIsNotRecheckedButNestedEvalIs()
    {
        var oversized = Engine.PrepareScript("'source longer than five'");
        var nestedEval = Engine.PrepareScript("eval('source longer than five')");
        var engine = CreateEngine(maxSourceLength: 5);

        engine.Evaluate(oversized).Should().Be("source longer than five");
        AssertSourceLimit(() => engine.Evaluate(nestedEval), 5, 23);
    }

    [Fact]
    public void PreparationLimitsFlowIntoNestedEval()
    {
        var preparationOptions = new ScriptPreparationOptions
        {
            ParsingOptions = new ScriptParsingOptions { MaxSourceLength = 20 },
        };
        var prepared = Engine.PrepareScript("eval(payload)", options: preparationOptions);
        var payload = string.Join(";", Enumerable.Repeat("0", 30));
        var engine = new Engine().SetValue("payload", payload);

        AssertSourceLimit(() => engine.Evaluate(prepared), 20, payload.Length);
    }

    [Fact]
    public void EscapedFunctionKeepsItsPreparationLimits()
    {
        var preparationOptions = new ScriptPreparationOptions
        {
            ParsingOptions = new ScriptParsingOptions { MaxSourceLength = 40 },
        };
        var prepared = Engine.PrepareScript(
            "globalThis.saved = () => eval(payload)",
            options: preparationOptions);
        var payload = string.Join(";", Enumerable.Repeat("0", 30));
        var engine = new Engine().SetValue("payload", payload);
        engine.Execute(prepared);

        AssertSourceLimit(() => engine.Invoke("saved"), 40, payload.Length);
    }

    [Theory]
    [InlineData("return (0, eval)(payload)")]
    [InlineData("return Function(payload)()")]
    public void LeafFunctionKeepsItsPreparationLimits(string body)
    {
        var preparationOptions = new ScriptPreparationOptions
        {
            ParsingOptions = new ScriptParsingOptions { MaxSourceLength = 120 },
        };
        var prepared = Engine.PrepareScript(
            $"globalThis.saved = function() {{ {body}; }}",
            options: preparationOptions);
        var payload = string.Join(";", Enumerable.Repeat("0", 300));
        var engine = new Engine().SetValue("payload", payload);
        engine.Execute(prepared);

        var exception = Invoking(() => engine.Invoke("saved"))
            .Should().ThrowExactly<ParsingLimitException>().Which;
        exception.Kind.Should().Be(ParsingLimitKind.SourceLength);
        exception.Limit.Should().Be(120);
        exception.Actual.Should().BeGreaterThanOrEqualTo(payload.Length);
    }

    [Fact]
    public void EvalLimitCannotBeCaughtByJavaScript()
    {
        var prepared = Engine.PrepareScript("""
            try {
                eval("source longer than five");
            } catch (e) {
                "caught";
            }
            """);
        var engine = CreateEngine(maxSourceLength: 5);

        AssertSourceLimit(() => engine.Evaluate(prepared), 5, 23);
    }

    [Fact]
    public void FunctionConstructorsCountGeneratedSource()
    {
        var prepared = Engine.PrepareScript("new Function('return 1')");
        var engine = CreateEngine(maxSourceLength: 20);

        var exception = Invoking(() => engine.Evaluate(prepared))
            .Should().ThrowExactly<ParsingLimitException>().Which;
        exception.Kind.Should().Be(ParsingLimitKind.SourceLength);
        exception.Actual.Should().BeGreaterThan(20);
    }

    [Fact]
    public void ShadowRealmHostEvaluationEnforcesLimits()
    {
        var engine = CreateEngine(maxSourceLength: 1);
        var shadowRealm = engine.Intrinsics.ShadowRealm.Construct();

        shadowRealm.Evaluate("0").Should().Be(0);
        AssertSourceLimit(() => shadowRealm.Evaluate("00"), 1, 2);
    }

    [Fact]
    public void ShadowRealmHostEvaluationHonorsPerCallLimits()
    {
        var shadowRealm = new Engine().Intrinsics.ShadowRealm.Construct();
        var parsingOptions = new ScriptParsingOptions { MaxSourceLength = 1 };

        AssertSourceLimit(() => shadowRealm.Evaluate("00", parsingOptions), 1, 2);
    }

    [Fact]
    public async Task ConcurrentShadowRealmParsingAndEvaluationIsRejected()
    {
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var shadowRealm = new Engine().Intrinsics.ShadowRealm.Construct();
        shadowRealm.SetValue("block", new Action(() =>
        {
            entered.Set();
            release.Wait(TimeSpan.FromSeconds(10));
        }));
        var running = Task.Run(() => shadowRealm.Evaluate("block()"));

        entered.Wait(TimeSpan.FromSeconds(10)).Should().BeTrue();
        try
        {
            Invoking(() => shadowRealm.Evaluate("1"))
                .Should().Throw<InvalidOperationException>()
                .WithMessage("*already in use by another thread or has an asynchronous operation in progress*");
        }
        finally
        {
            release.Set();
        }

        await running;
    }

    [Fact]
    public void ShadowRealmJavaScriptEvaluationEnforcesLimits()
    {
        var prepared = Engine.PrepareScript("new ShadowRealm().evaluate('00')");
        var engine = CreateEngine(maxSourceLength: 1);

        AssertSourceLimit(() => engine.Evaluate(prepared), 1, 2);
    }

    [Fact]
    public void DebuggerEvaluationEnforcesLimits()
    {
        var engine = CreateEngine(maxSourceLength: 1);

        AssertSourceLimit(() => engine.Debugger.Evaluate("00"), 1, 2);
    }

    [Fact]
    public void DebuggerEvaluationHonorsPerCallLimits()
    {
        var engine = new Engine();
        var parsingOptions = new ScriptParsingOptions { MaxSourceLength = 1 };

        AssertSourceLimit(() => engine.Debugger.Evaluate("00", parsingOptions), 1, 2);
    }

    [Fact]
    public void PreparedDebuggerEvaluationKeepsItsLimitsAndFatalException()
    {
        var preparationOptions = new ScriptPreparationOptions
        {
            ParsingOptions = new ScriptParsingOptions { MaxSourceLength = 20 },
        };
        var prepared = Engine.PrepareScript("eval(payload)", options: preparationOptions);
        var engine = new Engine(options => { options.Debugger.Enabled = true; options.Debugger.StatementHandling = DebuggerStatementHandling.Script; });
        engine.SetValue("payload", new string(' ', 30));
        engine.Debugger.Break += (_, _) =>
        {
            engine.Debugger.Evaluate(prepared);
            return StepMode.None;
        };

        AssertSourceLimit(() => engine.Execute("debugger;"), 20, 30);
    }

    [Fact]
    public void ClosureCreatedByPreparedDebuggerEvaluationKeepsItsLimits()
    {
        var prepared = Engine.PrepareScript(
            "globalThis.debugClosure = () => eval(payload)",
            options: new ScriptPreparationOptions
            {
                ParsingOptions = new ScriptParsingOptions { MaxSourceLength = 50 },
            });
        var engine = new Engine(options => { options.Debugger.Enabled = true; options.Debugger.StatementHandling = DebuggerStatementHandling.Script; });
        engine.SetValue("payload", new string(' ', 60));
        engine.Debugger.Break += (_, _) =>
        {
            engine.Debugger.Evaluate(prepared);
            return StepMode.None;
        };

        engine.Execute("debugger;");

        AssertSourceLimit(() => engine.Evaluate("debugClosure()"), 50, 60);
    }

    [Fact]
    public void SynchronousModuleLoaderSourceEnforcesLimits()
    {
        var loader = new SourceModuleLoader("export const value = 1;");
        var engine = new Engine(options =>
        {
            options.Parsing.MaxSourceLength = 5;
            options.UseModules(loader);
        });

        AssertSourceLimit(() => engine.Modules.Import("main"), 5, 23);
    }

    [Fact]
    public void AsynchronousModuleLoaderSourceEnforcesLimits()
    {
        var loader = new ImmediateAsyncModuleLoader("export const value = 1;");
        var engine = new Engine(options =>
        {
            options.Parsing.MaxSourceLength = 5;
            options.UseModules(loader);
        });

        AssertSourceLimit(() => engine.Modules.Import("main"), 5, 23);
    }

    [Fact]
    public void AsynchronousLoaderParsingLimitFaultIsNotARejection()
    {
        var engine = new Engine(options => options.UseModules(new ParsingFaultAsyncModuleLoader()));

        Invoking(() => engine.Modules.Import("main"))
            .Should().ThrowExactly<ParsingLimitException>();
    }

    [Fact]
    public async Task AsyncModuleCompletionCarriesParsingAndMemoryStateAcrossAThreadHop()
    {
        var loader = new DeferredAsyncModuleLoader();
        var preparationOptions = new ScriptPreparationOptions
        {
            ParsingOptions = new ScriptParsingOptions { MaxSourceLength = 20 },
        };
        var prepared = Engine.PrepareScript("import('main')", options: preparationOptions);
        var engine = new Engine(options =>
        {
            options.LimitMemory(16_000_000);
            options.UseModules(loader);
        });
        var memory = engine.Constraints.Find<MemoryLimitConstraint>()!;

        engine.Evaluate(prepared);
        await Task.Run(() => loader.Deliver("export const value = 1;"));

        var failure = await Record.ExceptionAsync(() => Task.Run(engine.Tasks.ProcessTasks));
        var parsingFailure = failure.Should().BeOfType<ParsingLimitException>().Subject;
        parsingFailure.Limit.Should().Be(20);
        parsingFailure.Actual.Should().Be(23);
        memory.IsOperationActive.Should().BeFalse();
        engine.Evaluate("1").Should().Be(1);
    }

    [Fact]
    public async Task ImportAsyncParsingFailureReleasesOwnershipMemoryAndPendingLoad()
    {
        var loader = new RetriableAsyncModuleLoader();
        var engine = new Engine(options =>
        {
            options.LimitMemory(16_000_000);
            options.Parsing.MaxSourceLength = 20;
            options.UseModules(loader);

            // The source is delivered from a thread-pool worker, twice. What is asserted is the
            // ParsingLimitException, the released ownership and the removed pending-load entry - never a
            // duration - so the promise budget is a ceiling on a wedge and must not be one the pool's
            // injection rate can beat.
            options.Constraints.PromiseTimeout = TestBudgets.WedgeCeiling;
        });
        var memory = engine.Constraints.Find<MemoryLimitConstraint>()!;

        var failedImport = engine.Modules.ImportAsync("main");
        Invoking(() => engine.Evaluate("1"))
            .Should().Throw<InvalidOperationException>()
            .WithMessage("*already in use by another thread or has an asynchronous operation in progress*");

        await Task.Run(() => loader.Deliver("export const value = 1;"));
        await Awaiting(() => failedImport).Should().ThrowExactlyAsync<ParsingLimitException>();

        memory.IsOperationActive.Should().BeFalse();
        engine.Evaluate("1").Should().Be(1);

        var retry = engine.Modules.ImportAsync("main");
        await Task.Run(() => loader.Deliver("export {};"));
        await retry;

        loader.Fetches.Should().Be(2, "the fatal parsing failure must remove the pending-load entry");
        memory.IsOperationActive.Should().BeFalse();
    }

    [Fact]
    public void DeferredDependencyKeepsOriginatingLimitsDuringNestedDrain()
    {
        var loader = new RetriableAsyncModuleLoader();
        var importing = Engine.PrepareScript(
            "import('main')",
            options: new ScriptPreparationOptions
            {
                ParsingOptions = new ScriptParsingOptions { MaxSourceLength = 100 },
            });
        var draining = Engine.PrepareScript(
            "drain()",
            options: new ScriptPreparationOptions
            {
                ParsingOptions = new ScriptParsingOptions { MaxSourceLength = 10 },
            });
        var engine = new Engine(options => options.UseModules(loader));
        engine.SetValue("drain", new Action(engine.Tasks.ProcessTasks));

        engine.Evaluate(importing);
        loader.Deliver("import 'dep';");
        engine.Evaluate(draining);

        loader.Deliver("export const value = 1;");
        Invoking(() => engine.Tasks.ProcessTasks()).Should().NotThrow();
        loader.Fetches.Should().Be(2);
    }

    [Fact]
    public void ModuleFactoryHonorsPerCallLimits()
    {
        var engine = new Engine();
        var resolved = Resolved("main");
        var parsingOptions = new ModuleParsingOptions { MaxSourceLength = 1 };

        AssertSourceLimit(
            () => ModuleFactory.BuildSourceTextModule(engine, resolved, "00", parsingOptions),
            1,
            2);
    }

    [Fact]
    public void DefaultModuleLoaderStopsReadingAfterTheLimit()
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var file = Path.Combine(directory, "main.js");
        File.WriteAllText(file, "export const value = 1;");

        try
        {
            var engine = new Engine(options =>
            {
                options.Parsing.MaxSourceLength = 5;
                options.UseModules(directory);
            });

            AssertSourceLimit(() => engine.Modules.Import("./main.js"), 5, 6);
        }
        finally
        {
            File.Delete(file);
            Directory.Delete(directory);
        }
    }

    private sealed class RetriableAsyncModuleLoader : IAsyncModuleLoader
    {
        private readonly Queue<ModuleLoadCompletion> _pending = new();

        public int Fetches { get; private set; }

        public ResolvedSpecifier Resolve(string? referencingModuleLocation, ModuleRequest moduleRequest)
            => new(moduleRequest, moduleRequest.Specifier, Uri: null, SpecifierType.Bare);

        public Jint.Runtime.Modules.Module LoadModule(Engine engine, ResolvedSpecifier resolved)
            => throw new InvalidOperationException();

        public void LoadModuleAsync(Engine engine, ResolvedSpecifier resolved, ModuleLoadCompletion completion)
        {
            Fetches++;
            _pending.Enqueue(completion);
        }

        public void Deliver(string source)
        {
            _pending.Should().NotBeEmpty();
            _pending.Dequeue().SetSource(source);
        }
    }

    [Fact]
    public void DefaultModuleLoaderAcceptsIntMaxValueLimit()
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var file = Path.Combine(directory, "main.js");
        File.WriteAllText(file, "export const value = 1;");

        try
        {
            var engine = new Engine(options =>
            {
                options.Parsing.MaxSourceLength = int.MaxValue;
                options.UseModules(directory);
            });

            engine.Modules.Import("./main.js").Get("value").Should().Be(1);
        }
        finally
        {
            File.Delete(file);
            Directory.Delete(directory);
        }
    }

    [Fact]
    public void ModuleBuilderChecksCombinedSourceBeforeJoining()
    {
        var engine = CreateEngine(maxSourceLength: 2);
        engine.Modules.Add("main", builder => builder.AddSource("0").AddSource("0"));

        var exception = Invoking(() => engine.Modules.Import("main"))
            .Should().ThrowExactly<ParsingLimitException>().Which;
        exception.Kind.Should().Be(ParsingLimitKind.SourceLength);
        exception.Limit.Should().Be(2);
        exception.Actual.Should().Be(2 + Environment.NewLine.Length);
    }

    [Fact]
    public void JsonModuleSourceEnforcesSourceLength()
    {
        var engine = CreateEngine(maxSourceLength: 2);
        var request = new ModuleRequest("data", [new ModuleImportAttribute("type", "json")]);
        var resolved = new ResolvedSpecifier(request, "data", Uri: null, SpecifierType.Bare);

        AssertSourceLimit(() => ModuleFactory.BuildJsonModule(engine, resolved, "{} "), 2, 3);
    }

    [Fact]
    public void PreparedModuleIsNotRechecked()
    {
        var prepared = Engine.PrepareModule("export const value = 'source longer than five';", "main");
        var engine = CreateEngine(maxSourceLength: 5);
        engine.Modules.Add("main", builder => builder.AddModule(prepared));

        engine.Modules.Import("main").Get("value").Should().Be("source longer than five");
    }

    [Fact]
    public void ModulePreparationLimitsFlowIntoNestedEval()
    {
        var preparationOptions = new ModulePreparationOptions
        {
            ParsingOptions = new ModuleParsingOptions { MaxSourceLength = 20 },
        };
        var prepared = Engine.PrepareModule("eval(payload);", "main", preparationOptions);
        var payload = string.Join(";", Enumerable.Repeat("0", 30));
        var engine = new Engine().SetValue("payload", payload);
        engine.Modules.Add("main", builder => builder.AddModule(prepared));

        AssertSourceLimit(() => engine.Modules.Import("main"), 20, payload.Length);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ModulePreparationLimitsFlowIntoStaticDependencies(bool asynchronous)
    {
        var preparationOptions = new ModulePreparationOptions
        {
            ParsingOptions = new ModuleParsingOptions { MaxSourceLength = 20 },
        };
        var prepared = Engine.PrepareModule("import 'dep';", "root", preparationOptions);
        IModuleLoader loader = asynchronous
            ? new DependencyAsyncModuleLoader()
            : new DependencyModuleLoader();
        var engine = new Engine(options => options.UseModules(loader));
        engine.Modules.Add("root", builder => builder.AddModule(prepared));

        AssertSourceLimit(() => engine.Modules.Import("root"), 20, 23);
    }

    [Fact]
    public void NullLimitsPreserveUnboundedParsing()
    {
        var source = "/*" + new string('x', 100_000) + "*/ 42";
        var engine = new Engine();

        engine.Evaluate(source).Should().Be(42);
        Engine.PrepareScript(source).Program.Should().NotBeNull();
        Engine.PrepareModule(source).Program.Should().NotBeNull();
    }

    [Fact]
    public void ExplicitDefaultParsingOptionsKeepEngineSourceRetention()
    {
        var engine = new Engine(options => options.RetainFunctionSourceText = true);

        engine.Evaluate(
                "eval('(function inner(){ return 4 })').toString()",
                "script",
                ScriptParsingOptions.Default)
            .Should().Be("function inner(){ return 4 }");

        engine.Execute(
            "globalThis.dynamic = new Function('return 5')",
            "script",
            ScriptParsingOptions.Default);
        engine.Evaluate("dynamic.toString()")
            .AsString().Should().Contain("return 5");
    }

    [Theory]
    [InlineData("eval(payload)", false, false)]
    [InlineData("Function(payload)", false, false)]
    [InlineData("new ShadowRealm().evaluate(payload)", false, false)]
    [InlineData("", true, false)]
    [InlineData("", true, true)]
    public void DynamicCompilationLimitsSurviveGuardedHostCallbackReentry(
        string source,
        bool hostReentry,
        bool execute)
    {
        var preparationOptions = new ScriptPreparationOptions
        {
            ParsingOptions = new ScriptParsingOptions { MaxSourceLength = 40 },
        };
        var prepared = Engine.PrepareScript(
            hostReentry ? "reenter()" : source,
            options: preparationOptions);
        var engine = new Engine();
        engine.SetValue("payload", new string(' ', 50));
        engine.SetValue("reenter", new Action(() =>
        {
            if (execute)
            {
                engine.Execute(new string(' ', 50));
            }
            else
            {
                engine.Evaluate(new string(' ', 50));
            }
        }));

        var failure = Invoking(() => engine.Evaluate(prepared))
            .Should().ThrowExactly<ParsingLimitException>().Which;
        failure.Limit.Should().Be(40);
        failure.Actual.Should().BeGreaterThan(40);
    }

    [Fact]
    public void ParsingLimitCannotBeCaughtAsAClrException()
    {
        var prepared = Engine.PrepareScript(
            "try { reenter(); } catch { globalThis.caught = true; }",
            options: new ScriptPreparationOptions
            {
                ParsingOptions = new ScriptParsingOptions { MaxSourceLength = 60 },
            });
        var engine = new Engine(options => options.CatchClrExceptions());
        engine.SetValue("reenter", new Action(() => engine.Evaluate(new string(' ', 70))));

        AssertSourceLimit(() => engine.Evaluate(prepared), 60, 70);
        engine.GetValue("caught").Should().BeUndefined();
    }

    [Fact]
    public void DefaultDynamicParsingRetainsSourceUnderActiveLimits()
    {
        var prepared = Engine.PrepareScript(
            "reenter()",
            options: new ScriptPreparationOptions
            {
                ParsingOptions = new ScriptParsingOptions { MaxSourceLength = 100 },
            });
        var engine = new Engine(options => options.RetainFunctionSourceText = true);
        JsValue? result = null;
        engine.SetValue("reenter", new Action(() =>
        {
            result = engine.Evaluate(
                "eval('(function inner(){ return 4 })').toString()",
                "nested",
                ScriptParsingOptions.Default);
        }));

        engine.Evaluate(prepared);

        result.Should().Be("function inner(){ return 4 }");
    }

    [Fact]
    public void EscapedFunctionUsesItsOwningParserOptions()
    {
        var engine = new Engine();
        var retaining = new ScriptParsingOptions { RetainFunctionSourceText = true };
        engine.Execute(
            "globalThis.describe = () => eval('(function inner(){ return 4 })').toString()",
            "owner",
            retaining);

        engine.Evaluate("describe()", "caller", ScriptParsingOptions.Default)
            .Should().Be("function inner(){ return 4 }");
    }

    [Fact]
    public void NegativeLimitsAreRejected()
    {
        Invoking(() => new Engine(options => options.Parsing.MaxSourceLength = -1))
            .Should().ThrowExactly<ArgumentOutOfRangeException>();

        Invoking(() => Engine.PrepareScript(
                "0",
                options: new ScriptPreparationOptions
                {
                    ParsingOptions = new ScriptParsingOptions { MaxNodeCount = -1 },
                }))
            .Should().ThrowExactly<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void ExistingParsingOptionsInterfaceRemainsImplementable()
    {
        IParsingOptions options = new ExternalParsingOptions();

        options.Tolerant.Should().BeFalse();
    }

    private static Engine CreateEngine(int? maxSourceLength = null, int? maxNodeCount = null)
    {
        return new Engine(options =>
        {
            options.Parsing.MaxSourceLength = maxSourceLength;
            options.Parsing.MaxNodeCount = maxNodeCount;
        });
    }

    private static ParsingLimitException AssertSourceLimit(Action action, int limit, long actual)
    {
        var exception = Invoking(action).Should().ThrowExactly<ParsingLimitException>().Which;
        exception.Kind.Should().Be(ParsingLimitKind.SourceLength);
        exception.Limit.Should().Be(limit);
        exception.Actual.Should().Be(actual);
        return exception;
    }

    private static ResolvedSpecifier Resolved(string specifier)
    {
        var request = new ModuleRequest(specifier, []);
        return new ResolvedSpecifier(request, specifier, Uri: null, SpecifierType.Bare);
    }

    private sealed class SourceModuleLoader(string source) : ModuleLoader
    {
        public override ResolvedSpecifier Resolve(string? referencingModuleLocation, ModuleRequest moduleRequest)
            => new(moduleRequest, moduleRequest.Specifier, Uri: null, SpecifierType.Bare);

        protected override string LoadModuleContents(Engine engine, ResolvedSpecifier resolved) => source;
    }

    private sealed class ImmediateAsyncModuleLoader(string source) : AsyncModuleLoader
    {
        public override ResolvedSpecifier Resolve(string? referencingModuleLocation, ModuleRequest moduleRequest)
            => new(moduleRequest, moduleRequest.Specifier, Uri: null, SpecifierType.Bare);

        protected override Task<string> LoadModuleContentsAsync(
            Engine engine,
            ResolvedSpecifier resolved,
            CancellationToken cancellationToken)
            => Task.FromResult(source);
    }

    private sealed class ParsingFaultAsyncModuleLoader : AsyncModuleLoader
    {
        public override ResolvedSpecifier Resolve(string? referencingModuleLocation, ModuleRequest moduleRequest)
            => new(moduleRequest, moduleRequest.Specifier, Uri: null, SpecifierType.Bare);

        protected override Task<string> LoadModuleContentsAsync(
            Engine engine,
            ResolvedSpecifier resolved,
            CancellationToken cancellationToken)
        {
            try
            {
                Engine.PrepareScript(
                    "00",
                    options: new ScriptPreparationOptions
                    {
                        ParsingOptions = new ScriptParsingOptions { MaxSourceLength = 1 },
                    });
            }
            catch (Exception exception)
            {
                return Task.FromException<string>(exception);
            }

            return Task.FromResult("");
        }
    }

    private sealed class DeferredAsyncModuleLoader : IAsyncModuleLoader
    {
        private ModuleLoadCompletion? _completion;

        public ResolvedSpecifier Resolve(string? referencingModuleLocation, ModuleRequest moduleRequest)
            => new(moduleRequest, moduleRequest.Specifier, Uri: null, SpecifierType.Bare);

        public Jint.Runtime.Modules.Module LoadModule(Engine engine, ResolvedSpecifier resolved)
            => throw new InvalidOperationException();

        public void LoadModuleAsync(Engine engine, ResolvedSpecifier resolved, ModuleLoadCompletion completion)
            => _completion = completion;

        public void Deliver(string source)
        {
            _completion.Should().NotBeNull();
            _completion!.SetSource(source);
        }
    }

    private sealed class DependencyModuleLoader : ModuleLoader
    {
        public override ResolvedSpecifier Resolve(string? referencingModuleLocation, ModuleRequest moduleRequest)
            => new(moduleRequest, moduleRequest.Specifier, Uri: null, SpecifierType.Bare);

        protected override string LoadModuleContents(Engine engine, ResolvedSpecifier resolved)
            => "export const value = 1;";
    }

    private sealed class DependencyAsyncModuleLoader : AsyncModuleLoader
    {
        public override ResolvedSpecifier Resolve(string? referencingModuleLocation, ModuleRequest moduleRequest)
            => new(moduleRequest, moduleRequest.Specifier, Uri: null, SpecifierType.Bare);

        protected override Task<string> LoadModuleContentsAsync(
            Engine engine,
            ResolvedSpecifier resolved,
            CancellationToken cancellationToken)
            => Task.FromResult("export const value = 1;");
    }

    private sealed class ExternalParsingOptions : IParsingOptions
    {
        public bool? CompileRegex { get; init; }
        public TimeSpan? RegexTimeout { get; init; }
        public bool Tolerant { get; init; }
        public bool RetainFunctionSourceText { get; init; }
    }
}
