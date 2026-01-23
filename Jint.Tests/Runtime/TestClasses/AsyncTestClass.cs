namespace Jint.Tests.Runtime.TestClasses;

internal class AsyncTestClass
{
    public static readonly string TestString = "Hello World";

    public string StringToAppend { get; set; } = string.Empty;

    public async Task AddToStringDelayedAsync(string appendWith)
    {
        await Task.Delay(100).ConfigureAwait(false);

        StringToAppend += appendWith;
    }

    public async Task<string> ReturnDelayedTaskAsync()
    {
        await Task.Delay(100).ConfigureAwait(false);

        return TestString;
    }

    public Task<string> ReturnCompletedTask()
    {
        return Task.FromResult(TestString);
    }

    public Task<string> ReturnCancelledTask(CancellationToken token)
    {
        return Task.FromCanceled<string>(token);
    }

    public async Task<string> ThrowAfterDelayAsync()
    {
        await Task.Delay(100).ConfigureAwait(false);

        throw new Exception("Task threw exception");
    }

#if !NETFRAMEWORK
    public async ValueTask<string> ReturnDelayedValueTaskAsync()
    {
        await Task.Delay(100).ConfigureAwait(false);

        return TestString;
    }

    public ValueTask<string> ReturnCompletedValueTask()
    {
        return ValueTask.FromResult(TestString);
    }

    public ValueTask<string> ReturnCancelledValueTask(CancellationToken token)
    {
        return ValueTask.FromCanceled<string>(token);
    }

    public async ValueTask<string> ThrowAfterDelayValueTaskAsync()
    {
        await Task.Delay(100).ConfigureAwait(false);

        throw new Exception("ValueTask threw exception");
    }

    public async IAsyncEnumerable<string> AsyncEnumerable()
    {
        yield return "Hello";
        await Task.Delay(10).ConfigureAwait(false);
        yield return " ";
        await Task.Delay(10).ConfigureAwait(false);
        yield return "World";
    }
#endif
}
