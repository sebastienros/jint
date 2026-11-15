using BrowserSettings = global::Jint.Browser.Tool.BrowserSettings;
using CommandLine = global::Jint.Browser.Tool.CommandLine;
using OptionKind = global::Jint.Browser.Tool.OptionKind;
using ServeCommand = global::Jint.Browser.Tool.ServeCommand;

namespace Jint.Tests.Browser.Tool;

/// <summary>
/// <c>--max-task-duration</c>: <c>serve</c> gives trusted pages no ceiling, because a protocol client owns its
/// timeouts and Chromium has none (#4619); every other command, and <c>--untrusted</c>, keeps a finite one.
/// </summary>
public sealed class MaxTaskDurationSettingTests
{
    [Test]
    public void ServeGivesTrustedPagesNoCeiling()
    {
        var options = Settings().ToBrowserOptions(ServeCommand.ServeAnimationFrameInterval, ServeCommand.ServeMaxTaskDuration);

        options.MaxTaskDuration.Should().BeLessThanOrEqualTo(TimeSpan.Zero, "a non-positive duration is the package's 'no time bound'");
    }

    [Test]
    public void TheOtherCommandsKeepThePackagesCeiling()
        => Settings().ToBrowserOptions().MaxTaskDuration.Should().Be(new global::Jint.Browser.BrowserOptions().MaxTaskDuration);

    [Test]
    public void AGivenCeilingWinsOverServesDefault()
        => Settings("--max-task-duration", "30s").ToBrowserOptions(ServeCommand.ServeAnimationFrameInterval, ServeCommand.ServeMaxTaskDuration)
            .MaxTaskDuration.Should().Be(TimeSpan.FromSeconds(30));

    /// <summary>
    /// The hardened profile fills in a ceiling the command line left alone; <c>serve</c>'s "none" must not be
    /// mistaken for the command line having named one.
    /// </summary>
    [Test]
    public void UntrustedServeKeepsTheProfilesCeiling()
    {
        var options = Settings("--untrusted").ToBrowserOptions(ServeCommand.ServeAnimationFrameInterval, ServeCommand.ServeMaxTaskDuration);

        options.MaxTaskDuration.Should().Be(global::Jint.UntrustedCodeLimits.Default.MaxOperationDuration);
    }

    private static BrowserSettings Settings(params string[] arguments)
    {
        var syntax = new Dictionary<string, OptionKind>(StringComparer.Ordinal);
        BrowserSettings.Declare(syntax);
        CommandLine.TryParse(arguments, syntax, out var line, out var error).Should().BeTrue(error);
        return BrowserSettings.Read(line!);
    }
}
