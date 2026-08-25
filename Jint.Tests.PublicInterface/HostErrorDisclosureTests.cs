using Jint.Native;
using Jint.Runtime;
using Jint.Runtime.Interop;
using Jint.Runtime.Modules;
using System.Runtime.CompilerServices;

using Module = Jint.Runtime.Modules.Module;

#nullable enable

namespace Jint.Tests.PublicInterface;

public class HostErrorDisclosureTests
{
    private const string Secret = "password=correct-horse; /srv/private/app.js; https://user:token@internal.example/module.js";

    private sealed class HostFailure(Exception? inner = null) : Exception(Secret, inner);

    private sealed class SecretApi
    {
        public SecretApi(string value)
        {
        }

        public void Invoke(string value)
        {
        }
    }

    private sealed class DeferredLoader : IAsyncModuleLoader
    {
        public ModuleLoadCompletion? Completion { get; private set; }

        public ResolvedSpecifier Resolve(string? referencingModuleLocation, ModuleRequest moduleRequest)
            => new(moduleRequest, moduleRequest.Specifier, new Uri("https://user:token@internal.example/module.js"), SpecifierType.RelativeOrAbsolute);

        public Module LoadModule(Engine engine, ResolvedSpecifier resolved)
            => throw new InvalidOperationException("Synchronous loading is not used.");

        public void LoadModuleAsync(Engine engine, ResolvedSpecifier resolved, ModuleLoadCompletion completion)
            => Completion = completion;
    }

    private sealed class FaultedTaskLoader(Exception exception) : AsyncModuleLoader
    {
        public override ResolvedSpecifier Resolve(string? referencingModuleLocation, ModuleRequest moduleRequest)
            => new(moduleRequest, moduleRequest.Specifier, new Uri("https://user:token@internal.example/module.js"), SpecifierType.RelativeOrAbsolute);

        protected override Task<string> LoadModuleContentsAsync(
            Engine engine,
            ResolvedSpecifier resolved,
            CancellationToken cancellationToken)
            => Task.FromException<string>(exception);
    }

    private sealed class CanceledTaskLoader : AsyncModuleLoader
    {
        private readonly CancellationToken _cancellationToken;

        public CanceledTaskLoader()
        {
            using var source = new CancellationTokenSource();
            source.Cancel();
            _cancellationToken = source.Token;
        }

        public override ResolvedSpecifier Resolve(string? referencingModuleLocation, ModuleRequest moduleRequest)
            => new(moduleRequest, moduleRequest.Specifier, Uri: null, SpecifierType.Bare);

        protected override Task<string> LoadModuleContentsAsync(
            Engine engine,
            ResolvedSpecifier resolved,
            CancellationToken cancellationToken)
            => Task.FromCanceled<string>(_cancellationToken);
    }

    private sealed class ThrowingSynchronousLoader(Exception exception) : ModuleLoader
    {
        public override ResolvedSpecifier Resolve(string? referencingModuleLocation, ModuleRequest moduleRequest)
            => new(moduleRequest, moduleRequest.Specifier, new Uri("https://user:token@internal.example/module.js"), SpecifierType.RelativeOrAbsolute);

        protected override string LoadModuleContents(Engine engine, ResolvedSpecifier resolved) => throw exception;
    }

    private sealed class ReimplementedModuleLoader : ModuleLoader, IModuleLoader
    {
        public override ResolvedSpecifier Resolve(string? referencingModuleLocation, ModuleRequest moduleRequest)
            => new(moduleRequest, moduleRequest.Specifier, Uri: null, SpecifierType.Bare);

        protected override string LoadModuleContents(Engine engine, ResolvedSpecifier resolved)
            => "export const unused = true;";

        Module IModuleLoader.LoadModule(Engine engine, ResolvedSpecifier resolved)
            => throw new JavaScriptException(engine.Intrinsics.Error, Secret);
    }

    private sealed class ThrowingResolveLoader : IAsyncModuleLoader
    {
        public ResolvedSpecifier Resolve(string? referencingModuleLocation, ModuleRequest moduleRequest)
            => throw new JavaScriptException(new JsString(Secret));

        public Module LoadModule(Engine engine, ResolvedSpecifier resolved)
            => throw new InvalidOperationException("Resolve must fail first.");

        public void LoadModuleAsync(Engine engine, ResolvedSpecifier resolved, ModuleLoadCompletion completion)
            => throw new InvalidOperationException("Resolve must fail first.");
    }

    [Fact]
    public void CaughtHostExceptionsAreGenericToScriptButCompleteToHost()
    {
        var original = new HostFailure(new InvalidOperationException("inner-secret"));
        var engine = new Engine(options => options.CatchClrExceptions());
        engine.SetValue("fail", new Action(() => throw original));

        engine.Evaluate("try { fail(); } catch (e) { e.message }").AsString()
            .Should().Be("A host operation failed.");

        var exception = Invoking(() => engine.Evaluate("fail()")).Should().Throw<JavaScriptException>().Which;
        exception.GetJavaScriptErrorString().Should().NotContain(Secret).And.NotContain("inner-secret");
        JintException.TryGetClrException(exception, out var clrException).Should().BeTrue();
        clrException.Should().BeSameAs(original);
        clrException!.InnerException!.Message.Should().Be("inner-secret");
    }

    [Fact]
    public void DetailedDevelopmentOptInRestoresHostExceptionMessages()
    {
        var engine = new Engine(options => options.CatchClrExceptions().ExposeDetailedErrors());
        engine.SetValue("fail", new Action(() => throw new HostFailure()));

        engine.Evaluate("try { fail(); } catch (e) { e.message }").AsString().Should().Be(Secret);
    }

    [Fact]
    public void NullJavaScriptExceptionMessageUsesTheSafeDefault()
    {
        var original = new HostFailure();
        var engine = new Engine();
        engine.SetValue("fail", new Action(() =>
            throw new JavaScriptException(engine.Intrinsics.Error, message: null, original)));

        var exception = Invoking(() => engine.Evaluate("fail()")).Should().Throw<JavaScriptException>().Which;
        exception.GetJavaScriptErrorString().Should().NotContain(Secret);
        JintException.TryGetClrException(exception, out var clrException).Should().BeTrue();
        clrException.Should().BeSameAs(original);
    }

    [Fact]
    public void HostCancellationIsNeverConvertedIntoAScriptError()
    {
        var engine = new Engine(options => options.CatchClrExceptions());
        engine.SetValue("cancel", new Action(() => throw new OperationCanceledException(Secret)));

        Invoking(() => engine.Evaluate("try { cancel(); } catch (e) { 'caught'; }"))
            .Should().Throw<OperationCanceledException>();
    }

    [Fact]
    public void ResolutionFailuresHideTypesAndSignaturesByDefault()
    {
        ClrResolutionErrorInfo? resolution = null;
        var engine = new Engine(options => options.Interop.ClrResolutionErrorDecorator = (_, _, info) => resolution = info);
        engine.SetValue("api", new SecretApi("value"));
        engine.SetValue("SecretCtor", TypeReference.CreateTypeReference<SecretApi>(engine));

        var methodError = Invoking(() => engine.Evaluate("api.Invoke(1, 2)"))
            .Should().Throw<JavaScriptException>().Which;
        var constructorError = Invoking(() => engine.Evaluate("new SecretCtor(1, 2)"))
            .Should().Throw<JavaScriptException>().Which;

        methodError.Message.Should().Be("No public methods with the specified arguments were found.");
        constructorError.Message.Should().Be("Could not resolve a constructor for the specified arguments.");
        methodError.Message.Should().NotContain(nameof(SecretApi)).And.NotContain("Invoke(String");
        constructorError.Message.Should().NotContain(nameof(SecretApi)).And.NotContain("SecretApi(String");

        JintException.TryGetClrType(methodError, out var type).Should().BeTrue();
        type.Should().Be(typeof(SecretApi));
        resolution.Should().NotBeNull();
        resolution!.DetailedMessage.Should().Contain(nameof(SecretApi)).And.Contain("SecretApi(String value)");
    }

    [Fact]
    public void SetErrorExceptionIsGenericAndRetainsTheOriginal()
    {
        var original = new HostFailure(new InvalidOperationException("inner-secret"));
        var loader = new DeferredLoader();
        var engine = new Engine(options => options.UseModules(loader));
        var import = engine.Modules.StartImport("https://user:token@internal.example/module.js");

        loader.Completion!.SetError(original);
        engine.Tasks.ProcessTasks();

        import.Error!.Get("message").AsString().Should().Be("Could not load module.");
        import.Error.Get("message").AsString().Should().NotContain("internal.example").And.NotContain("/srv/private");
        var rejection = Invoking(() => import.GetResult()).Should().Throw<PromiseRejectedException>().Which;
        JintException.TryGetClrException(rejection, out var clrException).Should().BeTrue();
        clrException.Should().BeSameAs(original);
    }

    [Fact]
    public void ModuleParseFailuresHideCanonicalUrlsAndPaths()
    {
        var loader = new DeferredLoader();
        var decoratorCalls = 0;
        var engine = new Engine(options =>
        {
            options.UseModules(loader);
            options.Interop.ClrExceptionErrorDecorator = (_, _, _) => decoratorCalls++;
        });
        var import = engine.Modules.StartImport("https://user:token@internal.example/private/module.js");

        loader.Completion!.SetSource("export const = invalid;");
        engine.Tasks.ProcessTasks();

        import.Error!.Get("message").AsString().Should().Be("Could not load module.");
        import.Error.Get("stack").AsString().Should().NotContain("internal.example").And.NotContain("/private/");
        var rejection = Invoking(() => import.GetResult()).Should().Throw<PromiseRejectedException>().Which;
        JintException.TryGetClrException(rejection, out var parserException).Should().BeTrue();
        parserException.Should().NotBeNull();
        decoratorCalls.Should().Be(1);
    }

    [Fact]
    public void ModuleErrorDecoratorReceivesOriginalBeforeCustomizingSafeError()
    {
        Exception? decorated = null;
        var decoratorCalls = 0;
        var original = new HostFailure();
        var loader = new DeferredLoader();
        var engine = new Engine(options =>
        {
            options.UseModules(loader);
            options.Interop.ClrExceptionErrorDecorator = (_, error, exception) =>
            {
                decoratorCalls++;
                decorated = exception;
                error.Set("code", "MODULE_LOAD_FAILED");
            };
        });
        var import = engine.Modules.StartImport("./module.js");

        loader.Completion!.SetError(original);
        engine.Tasks.ProcessTasks();

        decorated.Should().BeSameAs(original);
        decoratorCalls.Should().Be(1);
        import.Error!.Get("message").AsString().Should().Be("Could not load module.");
        import.Error.Get("code").AsString().Should().Be("MODULE_LOAD_FAILED");
    }

    [Fact]
    public async Task FaultedTasksAreGenericAndRetainTheOriginal()
    {
        var original = new HostFailure();
        var engine = new Engine(options => options.UseModules(new FaultedTaskLoader(original)));

        var rejection = await Invoking(() => engine.Modules.ImportAsync("./module.js"))
            .Should().ThrowAsync<PromiseRejectedException>();

        rejection.Which.RejectedValue.Get("message").AsString().Should().Be("Could not load module.");
        JintException.TryGetClrException(rejection.Which, out var clrException).Should().BeTrue();
        clrException.Should().BeSameAs(original);
    }

    [Fact]
    public void OrdinaryLoaderCancellationIsSanitizedWhileHostCancellationRemainsControlFlow()
    {
        var canceled = new Engine(options => options.UseModules(new CanceledTaskLoader()));

        var canceledImport = canceled.Modules.StartImport("./module.js");
        canceled.Tasks.ProcessTasks();
        canceledImport.Error!.Get("message").AsString().Should().Be("Could not load module.");

        var faultedCancellation = new Engine(options =>
            options.UseModules(new FaultedTaskLoader(new OperationCanceledException(Secret))));
        var faultedImport = faultedCancellation.Modules.StartImport("./module.js");
        faultedCancellation.Tasks.ProcessTasks();
        faultedImport.Error!.Get("message").AsString().Should().Be("Could not load module.");

        var loader = new DeferredLoader();
        var explicitCancellation = new Engine(options => options.UseModules(loader));
        var explicitImport = explicitCancellation.Modules.StartImport("./module.js");
        var transportCancellation = new OperationCanceledException(Secret);
        loader.Completion!.SetError(transportCancellation);
        explicitCancellation.Tasks.ProcessTasks();
        explicitImport.Error!.Get("message").AsString().Should().Be("Could not load module.");
        var rejection = Invoking(() => explicitImport.GetResult()).Should().Throw<PromiseRejectedException>().Which;
        JintException.TryGetClrException(rejection, out var original).Should().BeTrue();
        original.Should().BeSameAs(transportCancellation);

        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var hostCanceled = new Engine(options =>
        {
            options.ObserveCancellation(cancellation.Token);
            options.UseModules(new FaultedTaskLoader(new OperationCanceledException(cancellation.Token)));
        });
        Invoking(() => hostCanceled.Modules.StartImport("./module.js"))
            .Should().Throw<OperationCanceledException>();
    }

    [Fact]
    public void SynchronousLoaderFailuresAreGenericToDynamicImportAndOriginalToHostImport()
    {
        var original = new HostFailure();
        var loader = new ThrowingSynchronousLoader(original);
        var decoratorCalls = 0;
        var engine = new Engine(options =>
        {
            options.UseModules(loader);
            options.Interop.ClrExceptionErrorDecorator = (_, _, _) => decoratorCalls++;
        });

        var operation = engine.Modules.StartImport("https://user:token@internal.example/module.js");
        engine.Tasks.ProcessTasks();

        operation.Error!.Get("message").AsString().Should().Be("Could not load module.");
        var rejection = Invoking(() => operation.GetResult()).Should().Throw<PromiseRejectedException>().Which;
        JintException.TryGetClrException(rejection, out var clrException).Should().BeTrue();
        clrException.Should().BeSameAs(original);
        decoratorCalls.Should().Be(1);

        var synchronous = Invoking(() => new Engine(options => options.UseModules(loader)).Modules.Import("./module.js"))
            .Should().Throw<JavaScriptException>().Which;
        synchronous.Message.Should().Be("Could not load module.");
        JintException.TryGetClrException(synchronous, out var synchronousClrException).Should().BeTrue();
        synchronousClrException.Should().BeSameAs(original);
    }

    [Fact]
    public void ReimplementedModuleLoaderCannotBypassTheDisclosurePolicy()
    {
        var engine = new Engine(options => options.UseModules(new ReimplementedModuleLoader()));

        var import = engine.Modules.StartImport("./module.js");
        engine.Tasks.ProcessTasks();

        import.Error!.Get("message").AsString().Should().Be("Could not load module.");
        import.Error.Get("message").AsString().Should().NotContain(Secret);
    }

    [Fact]
    public void BlockingImportDoesNotRethrowAnUnprocessedLoaderError()
    {
        var decoratorCalls = 0;
        var engine = new Engine(options =>
        {
            options.UseModules(new ReimplementedModuleLoader());
            options.Interop.ClrExceptionErrorDecorator = (_, _, _) => decoratorCalls++;
        });

        var exception = Invoking(() => engine.Modules.Import("./module.js"))
            .Should().Throw<JavaScriptException>().Which;

        exception.Message.Should().Be("Could not load module.");
        exception.Message.Should().NotContain(Secret);
        decoratorCalls.Should().Be(1);
        JintException.TryGetClrException(exception, out var original).Should().BeTrue();
        original.Should().BeOfType<JavaScriptException>().Which.Message.Should().Be(Secret);
    }

    [Fact]
    public void ModulePolicyProvenanceIsSpecificToTheActiveEngineAndConfiguration()
    {
        var trustedLoader = new DeferredLoader();
        var trusted = new Engine(options =>
            options.UseModules(trustedLoader).ExposeDetailedErrors());
        var trustedImport = trusted.Modules.StartImport("./trusted.js");
        trustedLoader.Completion!.SetError(new HostFailure());
        trusted.Tasks.ProcessTasks();
        trustedImport.Error!.Get("message").AsString().Should().Be(Secret);

        var safeDecoratorCalls = 0;
        var safeLoader = new DeferredLoader();
        var safe = new Engine(options =>
        {
            options.UseModules(safeLoader);
            options.Interop.ClrExceptionErrorDecorator = (_, _, _) => safeDecoratorCalls++;
        });
        var safeImport = safe.Modules.StartImport("./safe.js");
        safeLoader.Completion!.SetError(new JavaScriptException(trustedImport.Error));
        safe.Tasks.ProcessTasks();

        safeImport.Error!.Get("message").AsString().Should().Be("Could not load module.");
        safeImport.Error.Get("message").AsString().Should().NotContain(Secret);
        safeDecoratorCalls.Should().Be(1);

        // The third engine is the configuration half: an error value a detailed engine produced, handed to
        // an engine configured to redact, is redacted. It is a second engine rather than the same one
        // reconfigured, because an engine's Options are read-only once it has been built from them.
        var detailedLoader = new DeferredLoader();
        var detailed = new Engine(new Options().UseModules(detailedLoader).ExposeDetailedErrors());
        var detailedImport = detailed.Modules.StartImport("./detailed.js");
        detailedLoader.Completion!.SetError(new HostFailure());
        detailed.Tasks.ProcessTasks();
        detailedImport.Error!.Get("message").AsString().Should().Be(Secret);

        var redactingLoader = new DeferredLoader();
        var redacting = new Engine(new Options().UseModules(redactingLoader));
        var redactedImport = redacting.Modules.StartImport("./redacted.js");
        redactingLoader.Completion!.SetError(new JavaScriptException(detailedImport.Error!));
        redacting.Tasks.ProcessTasks();

        redactedImport.Error!.Get("message").AsString().Should().Be("Could not load module.");
        redactedImport.Error.Get("message").AsString().Should().NotContain(Secret);
    }

    [Fact]
    public void DebuggerFailureCannotBypassTheDisclosurePolicy()
    {
        var decoratorCalls = 0;
        var loader = new DeferredLoader();
        var engine = new Engine(options =>
        {
            options.UseModules(loader);
            options.Interop.ClrExceptionErrorDecorator = (_, _, _) => decoratorCalls++;
        });
        engine.Debugger.BeforeEvaluate += (_, _) =>
            throw new JavaScriptException(engine.Intrinsics.Error, Secret);
        var import = engine.Modules.StartImport("./module.js");

        loader.Completion!.SetSource("export const value = 1;");
        engine.Tasks.ProcessTasks();

        import.Error!.Get("message").AsString().Should().Be("Could not load module.");
        import.Error.Get("message").AsString().Should().NotContain(Secret);
        decoratorCalls.Should().Be(1);
    }

    [Fact]
    public void DetailedResolveFailureStillCrossesTheDecoratorBoundaryOnce()
    {
        var decoratorCalls = 0;
        var engine = new Engine(options =>
        {
            options.UseModules(new ThrowingResolveLoader());
            options.ExposeDetailedErrors();
            options.Interop.ClrExceptionErrorDecorator = (_, _, _) => decoratorCalls++;
        });

        var import = engine.Modules.StartImport("./module.js");
        engine.Tasks.ProcessTasks();

        import.Error!.Get("message").AsString().Should().Be(Secret);
        decoratorCalls.Should().Be(1);
    }

    [Fact]
    public void DetailedJavaScriptSetErrorStillCrossesTheDecoratorBoundaryOnce()
    {
        var decoratorCalls = 0;
        var loader = new DeferredLoader();
        var engine = new Engine(options =>
        {
            options.UseModules(loader);
            options.ExposeDetailedErrors();
            options.Interop.ClrExceptionErrorDecorator = (_, _, _) => decoratorCalls++;
        });
        var import = engine.Modules.StartImport("./module.js");

        loader.Completion!.SetError(new JavaScriptException(engine.Intrinsics.TypeError, Secret));
        engine.Tasks.ProcessTasks();

        import.Error!.Get("name").AsString().Should().Be("TypeError");
        import.Error.Get("message").AsString().Should().Be(Secret);
        decoratorCalls.Should().Be(1);
    }

    [Fact]
    public void CanceledCompletionDoesNotRetainItsPendingImportGraph()
    {
        var (completion, operation) = CreateCanceledImport();

        for (var i = 0; i < 5 && operation.IsAlive; i++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }

        operation.IsAlive.Should().BeFalse();
        GC.KeepAlive(completion);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (ModuleLoadCompletion Completion, WeakReference Operation) CreateCanceledImport()
    {
        var loader = new DeferredLoader();
        var engine = new Engine(options => options.UseModules(loader));
        var operation = engine.Modules.StartImport("./module.js");
        var completion = loader.Completion!;

        completion.SetError(new OperationCanceledException("canceled"));
        engine.Tasks.ProcessTasks();
        operation.IsCompleted.Should().BeTrue();

        return (completion, new WeakReference(operation));
    }

    [Fact]
    public void DetailedDevelopmentOptInRestoresModuleFailureMessages()
    {
        var loader = new DeferredLoader();
        var engine = new Engine(options => options.UseModules(loader).ExposeDetailedErrors());
        var import = engine.Modules.StartImport("./module.js");

        loader.Completion!.SetError(new HostFailure());
        engine.Tasks.ProcessTasks();

        import.Error!.Get("message").AsString().Should().Be(Secret);
    }
}
