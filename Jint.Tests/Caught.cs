#nullable enable

namespace Jint.Tests;

/// <summary>
/// Runs a delegate and hands back whatever it threw, or <see langword="null" /> if it threw nothing.
/// </summary>
/// <remarks>
/// The replacement for xUnit's <c>Record.Exception</c>. NUnit's <c>Assert.Throws</c> and <c>Assert.Catch</c>
/// both fail when nothing is thrown, so neither can express the two shapes these suites need: capturing a
/// failure on a thread that is not the one asserting, and asserting that a call did <em>not</em> fail while
/// naming what it did throw when it turns out it has.
/// </remarks>
internal static class Caught
{
    /// <summary>What <paramref name="action" /> threw, or <see langword="null" />.</summary>
    public static Exception? Exception(Action action)
    {
        try
        {
            action();
            return null;
        }
        catch (Exception e)
        {
            return e;
        }
    }

    /// <summary>What <paramref name="action" /> threw, or <see langword="null" />.</summary>
    public static async Task<Exception?> ExceptionAsync(Func<Task> action)
    {
        try
        {
            await action().ConfigureAwait(false);
            return null;
        }
        catch (Exception e)
        {
            return e;
        }
    }
}
