using System.Text.Json;
using System.Text.Json.Serialization;

namespace Histogram;

/// <summary>
/// The two things a scenario has to do besides driving the browser: tell the proxy which step it is about
/// to perform, and write down what it observed so the analyzer can annotate the recording.
/// </summary>
internal sealed class ScenarioIo(string proxyUrl, string metaPath, string client, string clientVersion, string entryStyle)
{
    // Protocol traffic is asynchronous, so a step is given this long to fall quiet before the next mark is
    // written; without it the events a step causes would be attributed to the step after it. Same value as
    // SETTLE_MS in steps.mjs, and it has to stay the same or the Node and .NET columns would not compare.
    private const int SettleMs = 250;

    private readonly HttpClient _http = new();

    public Dictionary<string, string> Notes { get; } = [];

    public Dictionary<string, string> Skipped { get; } = [];

    public async Task MarkAsync(string step)
    {
        await Task.Delay(SettleMs);
        await _http.GetStringAsync($"{proxyUrl}/__mark?name={Uri.EscapeDataString(step)}");
    }

    /// <summary>Runs one step, recording a failure as "skipped" rather than aborting the whole recording.</summary>
    public async Task StepAsync(string step, Func<Task> body)
    {
        await MarkAsync(step);
        try
        {
            await body();
        }
        catch (Exception error)
        {
            Skipped[step] = $"{error.GetType().Name}: {error.Message}";
            Console.Error.WriteLine($"step {step} failed: {error.Message}");
        }
    }

    public async Task WriteAsync(string? failure = null)
    {
        await Task.Delay(SettleMs);
        var meta = new Meta(client, clientVersion, entryStyle, Notes, Skipped, failure);
        await File.WriteAllTextAsync(metaPath, JsonSerializer.Serialize(meta, new JsonSerializerOptions { WriteIndented = true }));
    }

    internal sealed record Meta(
        [property: JsonPropertyName("client")] string Client,
        [property: JsonPropertyName("clientVersion")] string ClientVersion,
        [property: JsonPropertyName("entryStyle")] string EntryStyle,
        [property: JsonPropertyName("notes")] Dictionary<string, string> Notes,
        [property: JsonPropertyName("skipped")] Dictionary<string, string> Skipped,
        [property: JsonPropertyName("failed")] string? Failed);

    public static Dictionary<string, string> ParseArguments(string[] args) => args
        .Select(a => a.TrimStart('-').Split('=', 2))
        .Where(parts => parts.Length == 2)
        .ToDictionary(parts => parts[0], parts => parts[1]);
}
