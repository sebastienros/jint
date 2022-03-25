using System.IO;
using System.Threading.Tasks;

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
            State.Sources[Path.GetFileName(file.FileName)] = new JavaScriptParser(source, new ParserOptions(file.FileName)).ParseScript();
        }

        return Task.CompletedTask;
    }
}
