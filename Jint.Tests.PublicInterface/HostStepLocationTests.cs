#nullable enable

using Jint.Runtime.Debugger;

namespace Jint.Tests.PublicInterface;

/// <summary>
/// <c>DebugHandler.GetStepLocations</c> from the outside: the positions a debugger stops at in a program,
/// which is what a tooling protocol answers <c>Debugger.getPossibleBreakpoints</c> with and what it snaps a
/// breakpoint request onto.
/// </summary>
/// <remarks>
/// A front end sets a breakpoint by line with <c>columnNumber: 0</c>, while Jint matches a step-eligible
/// node's exact start position. Without this list an indented statement is never hit, which is the gap the
/// two dead community debug adapters both named.
/// </remarks>
public class HostStepLocationTests
{
    private const string Source = "host.js";

    private const string Script = """
        var total = 0;
        function add(value) {
                total += value;
                return total;
        }
        for (var i = 0; i < 2; i++) {
            add(i);
        }
        debugger;
        """;

    private static Program Prepare(string code = Script) => Engine.PrepareScript(code, Source).Program!;

    /// <summary>
    /// The walk is reachable without an engine at all, which is what lets a host ask about a prepared script
    /// before anything runs it.
    /// </summary>
    [Test]
    public void TheLocationsOfAPreparedProgramAreReachableWithoutAnEngine()
    {
        var locations = DebugHandler.GetStepLocations(Prepare());

        locations.Should().NotBeEmpty();
        locations.Should().OnlyContain(location => location.Source == Source);
    }

    /// <summary>
    /// Ordering is the contract a caller binary-searches on, so it is asserted rather than assumed.
    /// </summary>
    [Test]
    public void LocationsAreOrderedByLineThenColumnThenKind()
    {
        var locations = DebugHandler.GetStepLocations(Prepare());

        for (var i = 1; i < locations.Count; i++)
        {
            var previous = locations[i - 1];
            var current = locations[i];

            var key = (previous.Line, previous.Column, (int) previous.Kind);
            var next = (current.Line, current.Column, (int) current.Kind);

            (key.CompareTo(next) < 0).Should().BeTrue($"{previous} must sort before {current}");
        }
    }

    /// <summary>
    /// The three kinds a caller maps onto the protocol's <c>BreakLocation.type</c>, each present exactly
    /// where the engine pauses for that reason.
    /// </summary>
    [Test]
    public void EachKindIsReportedWhereTheEnginePausesForIt()
    {
        var locations = DebugHandler.GetStepLocations(Prepare());

        locations.Should().Contain(location => location.Kind == StepLocationKind.DebuggerStatement && location.Line == 9);

        // The function body ends on line 5, and its closing brace is column 0, so the return point is 1.
        locations.Should().Contain(location => location.Kind == StepLocationKind.Return && location.Line == 5 && location.Column == 1);

        locations.Should().Contain(location => location.Kind == StepLocationKind.Statement && location.Line == 1 && location.Column == 0);
    }

    /// <summary>
    /// The snap a front end needs: an indented statement is what a request for column 0 of its line resolves
    /// to, and a breakpoint set there is hit.
    /// </summary>
    [Test]
    public void ARequestForTheStartOfALineSnapsOntoTheStatementOnIt()
    {
        var prepared = Engine.PrepareScript(Script, Source);

        var snapped = DebugHandler.FindStepLocation(prepared.Program!, line: 3, column: 0);

        snapped.Should().NotBeNull();
        snapped!.Value.Line.Should().Be(3);
        snapped.Value.Column.Should().Be(8);

        var breakLocation = snapped.Value.ToBreakLocation();
        breakLocation.Source.Should().Be(Source);

        using var engine = new Engine(options => options.Debugger.Enabled = true);
        var hits = 0;
        engine.Debugger.Break += (_, info) =>
        {
            hits++;
            info.Location.Start.Line.Should().Be(3);
            info.Location.Start.Column.Should().Be(8);
            return StepMode.None;
        };
        engine.Debugger.BreakPoints.Set(new BreakPoint(breakLocation.Source, breakLocation.Line, breakLocation.Column));

        engine.Execute(prepared);

        hits.Should().Be(2, "the loop calls the function twice");
    }

    /// <summary>
    /// A position past the last location has nothing to snap onto, which a caller must be able to tell from
    /// a match.
    /// </summary>
    [Test]
    public void ARequestPastTheLastLocationSnapsOntoNothing()
    {
        DebugHandler.FindStepLocation(Prepare(), line: 1000, column: 0).Should().BeNull();
    }

    /// <summary>
    /// The range overload answers with the same locations the full list holds inside the range, start
    /// inclusive and end exclusive.
    /// </summary>
    [Test]
    public void TheRangeOverloadIsTheFullListFilteredToTheRange()
    {
        var program = Prepare();

        var all = DebugHandler.GetStepLocations(program);
        var range = DebugHandler.GetStepLocations(program, startLine: 3, startColumn: 0, endLine: 5, endColumn: 0);

        range.Should().NotBeEmpty();
        range.Should().Equal(all.Where(location => location.Line is 3 or 4));
    }

    /// <summary>
    /// A module reaches the same walk through the same event, so a host holding nothing but a
    /// <see cref="Program"/> treats the two alike.
    /// </summary>
    [Test]
    public void AModuleIsWalkedLikeAScript()
    {
        using var engine = new Engine(options => options.Debugger.Enabled = true);

        Program? captured = null;
        engine.Debugger.BeforeEvaluate += (_, ast) => captured ??= ast;

        engine.Modules.Add("main", """
            export function twice(x) {
                return x * 2;
            }
            twice(2);
            """);
        engine.Modules.Import("main");

        captured.Should().NotBeNull();

        var locations = DebugHandler.GetStepLocations(captured!);

        locations.Should().Contain(location => location.Kind == StepLocationKind.Return);
        locations.Should().Contain(location => location.Kind == StepLocationKind.Statement && location.Line == 1);
    }

    /// <summary>
    /// The argument check a host relies on, rather than a null reference from inside the walk.
    /// </summary>
    [Test]
    public void ANullProgramIsRefused()
    {
        var act = () => DebugHandler.GetStepLocations(null!);

        act.Should().Throw<ArgumentNullException>();
    }
}
