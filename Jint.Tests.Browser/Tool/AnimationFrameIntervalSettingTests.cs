using BrowserSettings = global::Jint.Browser.Tool.BrowserSettings;
using CommandLine = global::Jint.Browser.Tool.CommandLine;
using OptionKind = global::Jint.Browser.Tool.OptionKind;
using ServeCommand = global::Jint.Browser.Tool.ServeCommand;
using ToolUsageException = global::Jint.Browser.Tool.ToolUsageException;

namespace Jint.Tests.Browser.Tool;

/// <summary>
/// <c>--animation-frame-interval</c>: <c>serve</c> answers a frame in a millisecond unless told otherwise, the
/// other commands keep the package's 16.
/// </summary>
public sealed class AnimationFrameIntervalSettingTests
{
    [Test]
    public void AGivenIntervalWinsOverTheCommandsDefault()
    {
        Settings("--animation-frame-interval", "5ms").ToBrowserOptions(ServeCommand.ServeAnimationFrameInterval)
            .AnimationFrameInterval.Should().Be(TimeSpan.FromMilliseconds(5));
        Settings("--animation-frame-interval", "0").ToBrowserOptions()
            .AnimationFrameInterval.Should().Be(TimeSpan.Zero);
    }

    [Test]
    public void ServeDefaultsToOneMillisecondAndTheOtherCommandsToThePackagesInterval()
    {
        Settings().ToBrowserOptions(ServeCommand.ServeAnimationFrameInterval)
            .AnimationFrameInterval.Should().Be(TimeSpan.FromMilliseconds(1));
        Settings().ToBrowserOptions().AnimationFrameInterval.Should().Be(new global::Jint.Browser.BrowserOptions().AnimationFrameInterval);
    }

    [Test]
    public void AnIntervalThatIsNotADurationIsAUsageError()
    {
        Caught.Exception(() => Settings("--animation-frame-interval", "soon"))
            .Should().BeOfType<ToolUsageException>().Which.Message.Should().Contain("animation-frame-interval");
    }

    private static BrowserSettings Settings(params string[] arguments)
    {
        var syntax = new Dictionary<string, OptionKind>(StringComparer.Ordinal);
        BrowserSettings.Declare(syntax);
        CommandLine.TryParse(arguments, syntax, out var line, out var error).Should().BeTrue(error);
        return BrowserSettings.Read(line!);
    }
}
