namespace Jint.Tests.Test262;

/// <summary>
/// Handles initializing testing state.
/// </summary>
public partial class TestHarness
{
    private static partial Task InitializeCustomState()
    {
        foreach (var file in State.HarnessFiles)
        {
            var source = file.Program;
            var script = Engine.PrepareScript(source, source: file.FileName);
            State.Sources[Path.GetFileName(file.FileName)] = script;
        }

        return Task.CompletedTask;
    }
}
