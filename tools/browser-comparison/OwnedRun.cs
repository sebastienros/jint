using System.Text.Json;

namespace BrowserComparison;

/// <summary>Persists every lifecycle transition and the terminal cleanup verdict, including failed rows.</summary>
internal sealed class OwnedRun(AdapterOptions options, BrowserAdapter adapter, string? journalPath)
{
    internal ScopeMemorySampler Memory { get; } = new(options.AccountingDirectory);
    internal ScopeSnapshot? FinalScope { get; private set; }
    internal bool CleanupSucceeded { get; private set; }
    internal List<object> Events { get; } = [];

    internal void Stage(string stage)
    {
        Memory.Stage(stage);
        Record(stage);
    }

    private void Record(string state, Exception? failure = null, Exception? cleanupFailure = null)
    {
        Events.Add(new { State = state, RecordedAt = DateTimeOffset.UtcNow, adapter.ProcessId,
            Failure = failure is null ? null : new { Type = failure.GetType().FullName, failure.Message },
            CleanupFailure = cleanupFailure is null ? null : new { Type = cleanupFailure.GetType().FullName, cleanupFailure.Message },
            CleanupSucceeded, adapter.ForcedTermination, Accounting = FinalScope });
        if (journalPath is not null)
        {
            var temporary = journalPath + ".tmp";
            File.WriteAllText(temporary, JsonSerializer.Serialize(new { SchemaVersion = 1, options.Name, options.Kind, Events,
                Terminal = state is "completed" or "failed", CleanupSucceeded, Accounting = FinalScope,
                ScopeMemorySamples = Memory.Snapshot() }));
            File.Move(temporary, journalPath, overwrite: true);
        }
    }

    internal async Task<T> ExecuteAsync<T>(Func<BrowserAdapter, Task<T>> work, CancellationToken cancellationToken = default)
    {
        T value = default!;
        Exception? failure = null;
        Exception? cleanupFailure = null;
        try
        {
            Stage("launch");
            await adapter.StartAsync(cancellationToken);
            Stage("preparation");
            value = await work(adapter);
        }
        catch (Exception exception)
        {
            failure = exception;
        }
        finally
        {
            try
            {
                Stage("teardown");
            }
            catch (Exception exception)
            {
                cleanupFailure = exception;
            }
            // Journal/counter failures must never prevent the actual owned-process cleanup.
            try
            {
                await adapter.DisposeAsync();
                FinalScope = ScopeSnapshot.Read(options.AccountingDirectory);
                CleanupSucceeded = FinalScope?.Populated != true;
            }
            catch (Exception exception)
            {
                cleanupFailure ??= exception;
            }
            try
            {
                await Memory.DisposeAsync();
            }
            catch (Exception exception)
            {
                cleanupFailure ??= exception;
            }
            Record(failure is null && cleanupFailure is null ? "completed" : "failed", failure, cleanupFailure);
        }
        if (failure is not null || cleanupFailure is not null)
        {
            throw new AggregateException("Owned browser row failed; inspect its lifecycle journal.",
                new[] { failure, cleanupFailure }.OfType<Exception>());
        }
        return value;
    }
}
