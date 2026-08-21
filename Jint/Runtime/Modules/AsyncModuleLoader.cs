using System.Threading;
using System.Threading.Tasks;
using Jint.Constraints;

namespace Jint.Runtime.Modules;

/// <summary>
/// Base template for a module loader that fetches module source over I/O. The counterpart of
/// <see cref="ModuleLoader"/> for hosts that cannot block: a subclass supplies
/// <see cref="LoadModuleContentsAsync"/> and the engine takes care of the rest — no thread is held while the
/// fetch is in flight, and the resulting module is built and registered on the engine thread.
/// </summary>
/// <example>
/// <code>
/// internal sealed class HttpModuleLoader : AsyncModuleLoader
/// {
///     private readonly HttpClient _client = new();
///
///     public override ResolvedSpecifier Resolve(string? referencingModuleLocation, ModuleRequest moduleRequest)
///         => /* map the specifier to an absolute URI, synchronously */;
///
///     protected override Task&lt;string&gt; LoadModuleContentsAsync(Engine engine, ResolvedSpecifier resolved, CancellationToken cancellationToken)
///         => _client.GetStringAsync(resolved.Uri);
/// }
///
/// var engine = new Engine(options => options.EnableModules(new HttpModuleLoader()));
/// var ns = await engine.Modules.ImportAsync("./main.js");
/// </code>
/// </example>
/// <remarks>
/// Note what the base class does <em>not</em> do: it never blocks on the returned task to satisfy the
/// synchronous <see cref="ModuleLoader.LoadModuleContents"/>, because a blocking wait on the engine thread is
/// the deadlock this class exists to avoid. The synchronous path therefore throws unless a subclass overrides
/// it, and reaching it at all means something asked for a module outside the load phase — see
/// <c>Host.GetImportedModule</c>.
/// <para>
/// The reverse composition costs nothing, though: a <see cref="LoadModuleContentsAsync"/> that returns an
/// already-completed task — a cache hit, source already in hand — finishes the load on the engine's own
/// stack, before the call returns. A graph made entirely of such answers keeps the blocking
/// <c>Engine.Modules.Import</c> fully synchronous, exactly as if a synchronous <see cref="IModuleLoader"/>
/// had served it.
/// </para>
/// </remarks>
public abstract class AsyncModuleLoader : ModuleLoader, IAsyncModuleLoader
{
    /// <inheritdoc />
    public void LoadModuleAsync(Engine engine, ResolvedSpecifier resolved, ModuleLoadCompletion completion)
    {
        // A host that registered options.CancellationToken(token) means it for its I/O too, so the fetch sees
        // the same token the interpreter's cancellation constraint observes.
        var cancellationToken = engine.Constraints.Find<CancellationConstraint>()?.Token ?? CancellationToken.None;

        if (resolved.ModuleRequest.IsBytesModule())
        {
            CompleteWithBytes(LoadModuleContentsAsBytesAsync(engine, resolved, cancellationToken), completion);
        }
        else
        {
            CompleteWithText(LoadModuleContentsAsync(engine, resolved, cancellationToken), completion);
        }
    }

    /// <summary>
    /// Loads the module's source text. Called on the engine thread; the returned task may complete on any
    /// thread, at any time.
    /// </summary>
    /// <remarks>
    /// A faulted task fails the load. Its exception is retained for host-side diagnostics while the
    /// script-visible message is generic unless detailed load errors are explicitly enabled. Cancellation
    /// exceptions remain control flow and abort the engine operation rather than becoming a catchable import
    /// rejection.
    /// </remarks>
    protected abstract Task<string> LoadModuleContentsAsync(Engine engine, ResolvedSpecifier resolved, CancellationToken cancellationToken);

    /// <summary>
    /// Loads the module's content as raw bytes, for a <c>with { type: "bytes" }</c> import. Defaults to
    /// UTF-8-encoding whatever <see cref="LoadModuleContentsAsync"/> returns, exactly as
    /// <see cref="ModuleLoader.LoadModuleContentsAsBytes"/> does on the synchronous path.
    /// </summary>
    protected virtual async Task<byte[]> LoadModuleContentsAsBytesAsync(Engine engine, ResolvedSpecifier resolved, CancellationToken cancellationToken)
    {
        var text = await LoadModuleContentsAsync(engine, resolved, cancellationToken).ConfigureAwait(false);
        return System.Text.Encoding.UTF8.GetBytes(text);
    }

    /// <summary>
    /// The synchronous entry point, which an asynchronous loader has no answer for. Overriding it is only
    /// needed by a loader that can also produce some modules without I/O — a cache in front of the network,
    /// say — and wants the engine's synchronous paths to keep working for those.
    /// </summary>
    protected override string LoadModuleContents(Engine engine, ResolvedSpecifier resolved)
    {
        Throw.NotSupportedException(
            $"'{GetType().Name}' loads modules asynchronously and cannot answer a synchronous load of '{resolved.ModuleRequest.Specifier}'. Import through Engine.Modules.ImportAsync or Engine.Modules.StartImport, or override LoadModuleContents for the specifiers this loader can resolve without I/O.");
        return default!;
    }

    private static void CompleteWithText(Task<string> task, ModuleLoadCompletion completion)
    {
        if (task is null)
        {
            completion.SetError(new InvalidOperationException(
                $"'{nameof(LoadModuleContentsAsync)}' returned null for '{completion.Resolved.ModuleRequest.Specifier}'."));
            return;
        }

        // An already-completed task is settled inline rather than through a continuation: a loader with a warm
        // cache is a common case, and settling before LoadModuleAsync returns is what lets the engine finish
        // the load on this very stack — synchronously, with no event-loop turn at all.
        if (task.IsCompleted)
        {
            SettleText(task, completion);
            return;
        }

        _ = task.ContinueWith(
            static (t, state) => SettleText(t, (ModuleLoadCompletion) state!),
            completion,
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    private static void CompleteWithBytes(Task<byte[]> task, ModuleLoadCompletion completion)
    {
        if (task is null)
        {
            completion.SetError(new InvalidOperationException(
                $"'{nameof(LoadModuleContentsAsBytesAsync)}' returned null for '{completion.Resolved.ModuleRequest.Specifier}'."));
            return;
        }

        if (task.IsCompleted)
        {
            SettleBytes(task, completion);
            return;
        }

        _ = task.ContinueWith(
            static (t, state) => SettleBytes(t, (ModuleLoadCompletion) state!),
            completion,
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    private static void SettleText(Task<string> task, ModuleLoadCompletion completion)
    {
        if (TryFail(task, completion))
        {
            return;
        }

        completion.SetSource(task.Result);
    }

    private static void SettleBytes(Task<byte[]> task, ModuleLoadCompletion completion)
    {
        if (TryFail(task, completion))
        {
            return;
        }

        completion.SetSource(task.Result);
    }

    private static bool TryFail(Task task, ModuleLoadCompletion completion)
    {
        if (task.IsFaulted)
        {
            // The AggregateException wrapper says nothing a script author can use; the fetch failure itself is
            // the single inner exception in every ordinary case.
            var exception = task.Exception?.InnerExceptions.Count == 1
                ? task.Exception.InnerExceptions[0]
                : task.Exception;

            completion.SetError(exception ?? new InvalidOperationException("Module load failed"));
            return true;
        }

        if (task.IsCanceled)
        {
            if (completion.CancellationToken.IsCancellationRequested)
            {
                completion.SetConstraintError(new OperationCanceledException(completion.CancellationToken));
            }
            else
            {
                completion.SetError(new TaskCanceledException(task));
            }
            return true;
        }

        return false;
    }
}
