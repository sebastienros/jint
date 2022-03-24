using System;
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
        // NOTE: The Date tests in test262 assume the local timezone is Pacific Standard Time
        try
        {
            State.TimeZone = TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time");
        }
        catch (TimeZoneNotFoundException)
        {
            // https://stackoverflow.com/questions/47848111/how-should-i-fetch-timezoneinfo-in-a-platform-agnostic-way
            // should be natively supported soon https://github.com/dotnet/runtime/issues/18644
            State.TimeZone = TimeZoneInfo.FindSystemTimeZoneById("America/Los_Angeles");
        }

        foreach (var file in State.HarnessFiles)
        {
            var source = file.Program;
            State.Sources[Path.GetFileName(file.FileName)] = new JavaScriptParser(source, new ParserOptions(file.FileName)).ParseScript();
        }

        return Task.CompletedTask;
    }
}