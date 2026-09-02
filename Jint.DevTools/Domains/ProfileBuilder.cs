using Jint.DevTools.Protocol.Profiler;
using ProtocolCallFrame = Jint.DevTools.Protocol.Runtime.CallFrame;

namespace Jint.DevTools.Domains;

/// <summary>
/// Turns one recording into the <c>Profiler.Profile</c> a front end's Performance panel loads: a call tree,
/// a series of samples over it, and the time between them.
/// </summary>
/// <remarks>
/// <para>
/// <b>A sample per interval, not per tick.</b> The recording says exactly when each function was entered and
/// left, so the top of the stack is known for every instant between two activations — and one sample of that
/// node, weighted by the interval, is what the panel wants. It is the same document a sampling profiler
/// produces, filled in exactly rather than statistically: the deltas add up to the recorded duration instead
/// of approximating it.
/// </para>
/// <para>
/// <b>Two synthetic nodes, and one that is deliberately absent.</b> <c>(root)</c> is the tree's root, as it
/// is in every V8 profile, and <c>(program)</c> takes the time when no script function was on the stack —
/// the host between calls. <c>(idle)</c> is never emitted, because an engine target has no idle state to
/// report: a host that is not running script is not idle, it is doing something this package cannot see. Nor
/// is <c>(garbage collector)</c>: the heap is the CLR's.
/// </para>
/// </remarks>
internal sealed class ProfileBuilder
{
    /// <summary>The identifier a location the script registry cannot attribute is reported under.</summary>
    /// <remarks>Chrome's own sentinel, and the one <c>Debugger</c> uses for the same reason.</remarks>
    private const string UnknownScriptId = "0";

    private const long NanosecondsPerMicrosecond = 1000;

    private readonly ScriptRegistry? _scripts;
    private readonly List<ProfileNode> _nodes = [];
    private readonly List<List<int>> _children = [];
    private readonly List<int> _hits = [];
    private readonly Dictionary<(int Parent, int Function), int> _byPosition = [];

    private ProfileBuilder(ScriptRegistry? scripts)
    {
        _scripts = scripts;
    }

    /// <summary>
    /// Builds the profile of <paramref name="recording"/>, timestamped from
    /// <paramref name="startedAtMicroseconds"/>.
    /// </summary>
    /// <param name="recording">What the source saw.</param>
    /// <param name="scripts">The registry that gives a function's source a script identifier, if there is one.</param>
    /// <param name="startedAtMicroseconds">When the recording started, in microseconds since the Unix epoch.</param>
    internal static Profile Build(RecordedProfile recording, ScriptRegistry? scripts, double startedAtMicroseconds)
    {
        var builder = new ProfileBuilder(scripts);
        return builder.Convert(recording, startedAtMicroseconds);
    }

    private Profile Convert(RecordedProfile recording, double startedAtMicroseconds)
    {
        // The two synthetic nodes are minted first so that (root) is 1, which is what every reader of a V8
        // profile assumes even though the protocol does not say it.
        var root = AddNode(parent: -1, Synthetic("(root)"));
        var program = AddNode(root, Synthetic("(program)"));

        var samples = new List<int>();
        var deltas = new List<int>();

        var stack = new Stack<int>();
        var current = program;
        var lastMicroseconds = 0L;

        foreach (var activation in recording.Activations)
        {
            var atMicroseconds = activation.TimestampNanoseconds / NanosecondsPerMicrosecond;
            Sample(samples, deltas, current, atMicroseconds - lastMicroseconds);
            lastMicroseconds = atMicroseconds;

            if (activation.Entered)
            {
                var parent = stack.Count > 0 ? stack.Peek() : root;
                stack.Push(NodeFor(parent, activation.FunctionIndex, recording.Functions));
            }
            else if (stack.Count > 0)
            {
                stack.Pop();
            }

            current = stack.Count > 0 ? stack.Peek() : program;
        }

        // Whatever ran after the last activation, up to the moment the client asked for the profile.
        var endMicroseconds = recording.DurationNanoseconds / NanosecondsPerMicrosecond;
        Sample(samples, deltas, current, endMicroseconds - lastMicroseconds);

        return new Profile
        {
            Nodes = Nodes(),
            StartTime = startedAtMicroseconds,
            EndTime = startedAtMicroseconds + endMicroseconds,
            Samples = [.. samples],
            TimeDeltas = [.. deltas],
        };
    }

    /// <summary>Records that <paramref name="node"/> was on top for <paramref name="microseconds"/>.</summary>
    /// <remarks>
    /// A zero-length interval is still a sample. Two activations at one timestamp is what a tail call looks
    /// like, and dropping the sample between them would lose the hit that says the displaced function ran.
    /// </remarks>
    private void Sample(List<int> samples, List<int> deltas, int node, long microseconds)
    {
        samples.Add(_nodes[node].Id);
        deltas.Add((int) Math.Clamp(microseconds, 0, int.MaxValue));
        _hits[node]++;
    }

    /// <summary>Mints one node under <paramref name="parent"/>, or nothing above it for the root.</summary>
    private int AddNode(int parent, ProtocolCallFrame function)
    {
        var index = _nodes.Count;
        var id = index + 1;

        _nodes.Add(new ProfileNode { Id = id, CallFrame = function });
        _children.Add([]);
        _hits.Add(0);

        if (parent >= 0)
        {
            _children[parent].Add(id);
        }

        return index;
    }

    /// <summary>
    /// Answers the node one function occupies under <paramref name="parent"/>, minting it the first time.
    /// </summary>
    /// <remarks>
    /// A profile is a tree of call <i>positions</i>, not of functions: one function called from two places is
    /// two nodes, which is what lets the panel say where the time went rather than only in what.
    /// </remarks>
    private int NodeFor(int parent, int functionIndex, IReadOnlyList<ProfileFunction> functions)
    {
        if (_byPosition.TryGetValue((parent, functionIndex), out var existing))
        {
            return existing;
        }

        var node = AddNode(parent, Frame(functions[functionIndex]));
        _byPosition[(parent, functionIndex)] = node;
        return node;
    }

    private ProfileNode[] Nodes()
    {
        var nodes = new ProfileNode[_nodes.Count];
        for (var i = 0; i < nodes.Length; i++)
        {
            var children = _children[i];
            nodes[i] = _nodes[i] with
            {
                HitCount = _hits[i],
                Children = children.Count > 0 ? [.. children] : null,
            };
        }

        return nodes;
    }

    /// <summary>One recorded function, in the protocol's counting and against the script it belongs to.</summary>
    /// <remarks>
    /// The engine counts a profile's lines and columns from one; the protocol counts both from zero. A
    /// built-in or a host callable has no position at all, and is reported at the unattributable script with
    /// <c>-1</c>, which is what V8 does for the same frames.
    /// </remarks>
    private ProtocolCallFrame Frame(ProfileFunction function)
    {
        if (function.File is not { } file || function.Line is not { } line)
        {
            return Synthetic(function.Name);
        }

        var column = function.Column ?? 1;
        var script = _scripts?.At(file, line, column - 1);

        return new ProtocolCallFrame
        {
            FunctionName = function.Name,
            ScriptId = script?.ScriptId ?? UnknownScriptId,
            Url = script?.Url ?? file,
            LineNumber = Math.Max(0, line - 1),
            ColumnNumber = Math.Max(0, column - 1),
        };
    }

    private static ProtocolCallFrame Synthetic(string name) => new()
    {
        FunctionName = name,
        ScriptId = UnknownScriptId,
        Url = "",
        LineNumber = -1,
        ColumnNumber = -1,
    };
}
