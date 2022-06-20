using Esprima;

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
            State.Sources[Path.GetFileName(file.FileName)] = new JavaScriptParser().ParseScript(source, source: file.FileName);
        }

        return Task.CompletedTask;
    }
}
