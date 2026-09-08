#nullable enable

using Test262Harness;

namespace Jint.Tests.Test262;

/// <summary>
/// Custom state for Jint.
/// </summary>
public static partial class State
{
    // Canonical SHA-256 over every /harness and /test path and per-file SHA-256 at GitHubSha.
    // A Test262 pin bump must update this digest from the reviewed corpus too.
    private const string CorpusContentSha256 = "c52be014920e6367aab7af3196ceb4e7aa3fa3b902581df31445d5139fde08cb";

    private static readonly Lazy<Task<Test262Stream>> _test262Stream = new(LoadTest262Stream);

    static State()
    {
        // The generated harness initializes this property to the package's single-attempt downloader.
        // Assign it from the static constructor so this always runs after generated field initializers.
        Test262StreamLoader = () => _test262Stream.Value;
    }

    /// <summary>
    /// Pre-compiled scripts for faster execution.
    /// </summary>
    public static readonly Dictionary<string, Prepared<Script>> Sources = new(StringComparer.OrdinalIgnoreCase);

    internal static Exception? CorpusInitializationFailure { get; private set; }

    private static async Task<Test262Stream> LoadTest262Stream()
    {
        var cacheDirectory = Environment.GetEnvironmentVariable("JINT_TEST262_CACHE");
        if (string.IsNullOrWhiteSpace(cacheDirectory))
        {
            cacheDirectory = Path.GetTempPath();
        }

        var offlineValue = Environment.GetEnvironmentVariable("JINT_TEST262_OFFLINE");
        var offline = offlineValue == "1"
                      || bool.TryParse(offlineValue, out var parsedOffline) && parsedOffline;

        try
        {
            var acquisition = Test262CorpusAcquisition.CreateDefault(
                CorpusContentSha256,
                message => TestContext.Progress.WriteLine(message));
            return await acquisition.LoadAsync(GitHubSha, cacheDirectory, offline);
        }
        catch (Exception exception)
        {
            CorpusInitializationFailure = exception;

            // Let the generated SetUpFixture finish so selected generated cases reach the unavailable
            // filesystem below. Throwing here makes NUnit copy one setup error to every case.
            var fileSystem = new UnavailableTest262FileSystem(exception);
            return Test262Stream.FromFileSystem(fileSystem);
        }
    }
}
