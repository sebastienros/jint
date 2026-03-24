#nullable enable

using Jint.Native;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;
using Test262Harness;

namespace Jint.Tests.Test262;

public abstract partial class Test262Test
{
    // Thread-local storage for agent manager (tests run in parallel)
    [ThreadStatic]
    private static Test262AgentManager? _currentAgentManager;

    // Thread-local storage for async test result (tests run in parallel)
    [ThreadStatic]
    private static string? _asyncResult;

    private static Engine BuildTestExecutor(Test262File file, Test262AgentManager? agentManager)
    {
        // Reset async result tracking
        _asyncResult = null;

        var engine = new Engine(cfg =>
        {
            var relativePath = Path.GetDirectoryName(file.FileName) ?? "";
            cfg.EnableModules(new Test262ModuleLoader(State.Test262Stream.Options.FileSystem, relativePath));
            cfg.ExperimentalFeatures = ExperimentalFeature.All;
            cfg.TimeoutInterval(TimeSpan.FromSeconds(30));
            // Use ICU-based CLDR provider for better Intl support
            cfg.Intl.CldrProvider = IcuCldrProvider.Instance;
            // Use NodaTime for accurate IANA timezone support (sub-minute offsets, historical DST)
            cfg.Temporal.TimeZoneProvider = NodaTimeZoneProvider.Instance;
        });

        if (file.Flags.Contains("raw"))
        {
            // nothing should be loaded
            return engine;
        }

        engine.Execute(State.Sources["assert.js"]);
        engine.Execute(State.Sources["sta.js"]);

        engine.SetValue("print", new ClrFunction(engine, "print", (_, args) =>
        {
            var message = TypeConverter.ToString(args.At(0));
            // Capture Test262 async test markers from $DONE via doneprintHandle.js
            if (message.StartsWith("Test262:AsyncTest", StringComparison.Ordinal))
            {
                _asyncResult = message;
            }
            return message;
        }));

        // Provide a basic setTimeout for async tests that need it
        engine.SetValue("setTimeout", new ClrFunction(engine, "setTimeout", (thisObj, args) =>
        {
            var callback = args.At(0);
            var delay = (int)TypeConverter.ToNumber(args.At(1));
            if (callback is ICallable callable)
            {
                _ = Task.Run(async () =>
                {
                    await Task.Delay(delay);
                    // Queue callback to event loop instead of calling directly from background thread
                    // to avoid race conditions with concurrent JavaScript execution
                    engine.AddToEventLoop(() =>
                    {
                        callable.Call(JsValue.Undefined, Arguments.Empty);
                    });
                });
            }
            return JsValue.Undefined;
        }));

        var o = Test262Object.Install(engine);

        // Install agent support if needed
        agentManager?.InstallAgent(engine, o);

        foreach (var include in file.Includes)
        {
            engine.Execute(State.Sources[include]);
        }

        if (file.Flags.Contains("async"))
        {
            engine.Execute(State.Sources["doneprintHandle.js"]);
        }

        return engine;
    }

    private static bool NeedsAgentSupport(Test262File file)
    {
        // Check if test includes atomicsHelper.js which indicates multi-agent test
        return file.Includes.Contains("atomicsHelper.js");
    }

    // Wrapper that maintains backward compatibility with generated code
    private static Engine BuildTestExecutor(Test262File file)
    {
        // Clean up any previous agent manager
        _currentAgentManager?.Dispose();
        _currentAgentManager = null;

        // Create agent manager if test needs it
        if (NeedsAgentSupport(file))
        {
            _currentAgentManager = new Test262AgentManager();
        }

        return BuildTestExecutor(file, _currentAgentManager);
    }

    private static void CleanupAgentManager()
    {
        _currentAgentManager?.Dispose();
        _currentAgentManager = null;
    }

    private static void ExecuteTest(Engine engine, Test262File file)
    {
        try
        {
            if (file.Type == ProgramType.Module)
            {
                var specifier = "./" + Path.GetFileName(file.FileName);
                engine.Modules.Add(specifier, builder => builder.AddSource(file.Program));
                engine.Modules.Import(specifier);
            }
            else
            {
                var script = Engine.PrepareScript(file.Program, source: file.FileName, options: new ScriptPreparationOptions
                {
                    ParsingOptions = ScriptParsingOptions.Default with { Tolerant = false },
                });

                engine.Execute(script);
            }

            // For async tests, drain the event loop and validate completion markers
            if (file.Flags.Contains("async"))
            {
                WaitForAsyncTestCompletion(engine);
            }
        }
        finally
        {
            // Cleanup agent manager after test execution
            CleanupAgentManager();
        }
    }

    /// <summary>
    /// Drains the event loop until $DONE is called (producing Test262:AsyncTestComplete or
    /// Test262:AsyncTestFailure marker via print), then validates the result.
    /// See https://github.com/nicolo-ribaudo/tc39-proposal-test262-spec/blob/main/spec.md
    /// </summary>
    private static void WaitForAsyncTestCompletion(Engine engine)
    {
        if (_asyncResult is null)
        {
            var eventLoop = engine.EventLoop;
            var previousWaitingThreadId = eventLoop._waitingThreadId;
            eventLoop._waitingThreadId = Environment.CurrentManagedThreadId;

            try
            {
                var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(30);

                while (_asyncResult is null)
                {
                    engine.RunAvailableContinuations();

                    if (_asyncResult is not null)
                    {
                        break;
                    }

                    if (DateTime.UtcNow >= deadline)
                    {
                        throw new TimeoutException("Async test did not complete - $DONE was not called");
                    }

                    // Poll with short interval for callbacks arriving from setTimeout/promise resolution
                    Thread.Sleep(10);
                }
            }
            finally
            {
                eventLoop._waitingThreadId = previousWaitingThreadId;
            }
        }

        // Validate the async test result
        if (_asyncResult!.StartsWith("Test262:AsyncTestFailure:", StringComparison.Ordinal))
        {
            throw new Exception(_asyncResult["Test262:AsyncTestFailure:".Length..]);
        }

        if (_asyncResult != "Test262:AsyncTestComplete")
        {
            throw new Exception($"Unexpected async test result: {_asyncResult}");
        }
    }

    private partial bool ShouldThrow(Test262File testCase, bool strict)
    {
        return testCase.Negative;
    }
}
