#if NET8_0_OR_GREATER
#nullable enable

using System.Collections;
using System.Reflection;
using Jint.Native;
using Jint.Runtime;
using Jint.WebApi.StructuredClone;

namespace Jint.Tests.Runtime.WebApi;

/// <summary>
/// The property the two-phase structured clone exists for: a <see cref="SerializationRecord"/> belongs to no
/// engine, so it can be built on one engine's thread and consumed on another's.
/// </summary>
/// <remarks>
/// <para>
/// Two independent checks, because either one alone can be fooled. The declaration check reads the record
/// types' own fields, which catches a new record type that stores a <c>JsValue</c> outright; it cannot see
/// through <see cref="SerializedValue"/>'s <c>object</c> payload. The graph walk serializes a deliberately
/// rich value and follows every reference the result actually holds, which does see through it but can only
/// speak for the shapes the sample covers. Together they cover the rule.
/// </para>
/// <para>
/// <b>A transferred <c>MessagePort</c> is the one sanctioned exception</b>, and it has a check of its own
/// rather than a hole in these two: the walk stops at <c>MessagePortEndpoint</c>, which is the class whose
/// whole job is to be touched from another engine's thread, and
/// <see cref="CarriesExactlyOneChannelSideForATransferredPortAndNothingElseEngineAffine"/> pins that a record
/// transferring a port reaches exactly one of them and nothing else the rule forbids.
/// </para>
/// </remarks>
public class SerializationRecordTests
{
    private static readonly Assembly _jintAssembly = typeof(Engine).Assembly;

    /// <summary>
    /// The types that make a graph engine-affine. Any of them reachable from a record means a record could
    /// hand one engine a value belonging to another, which is unsupported and unguarded.
    /// </summary>
    private static readonly Type[] _forbidden =
    [
        typeof(JsValue),
        typeof(Engine),
        typeof(Realm),
        typeof(Intrinsics),
    ];

    [Fact]
    public void DeclaresNoEngineAffineField()
    {
        var recordTypes = _jintAssembly
            .GetTypes()
            .Where(static t => t.Namespace == "Jint.WebApi.StructuredClone")
            .Where(static t => t.Name.StartsWith("Serialized", StringComparison.Ordinal) || t.Name == "SerializationRecord")
            .ToArray();

        // A sanity floor, so the query silently matching nothing cannot pass this test.
        recordTypes.Length.Should().BeGreaterThan(10);

        foreach (var type in recordTypes)
        {
            foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
            {
                foreach (var candidate in DeclaredTypes(field.FieldType))
                {
                    Forbidden(candidate).Should().BeFalse(
                        $"{type.Name}.{field.Name} is declared as {field.FieldType.Name}, which reaches the engine-affine type {candidate.Name}");
                }
            }
        }
    }

    [Fact]
    public void HoldsNothingEngineAffineForARichGraph()
    {
        var engine = new Engine(options => options.UseWebApis(WebApiFeatures.Messaging));

        // One value of every shape the serializer recognizes, plus a cycle and a shared reference.
        var value = engine.Evaluate("""
            (function () {
                var buffer = new ArrayBuffer(8);
                var shared = { tag: 'shared' };
                var graph = {
                    primitives: [undefined, null, true, 1.5, -0, NaN, 'text', 10n],
                    boxed: [new Boolean(true), new Number(1), new String('s'), Object(2n)],
                    date: new Date(0),
                    invalidDate: new Date(NaN),
                    regexp: /ab+c/gi,
                    buffer: buffer,
                    view: new Uint8Array(buffer, 4),
                    dataView: new DataView(buffer),
                    map: new Map([['k', shared]]),
                    set: new Set([shared, 1]),
                    error: new TypeError('bad'),
                    domException: new DOMException('nope', 'AbortError'),
                    array: [1, shared],
                    shared: shared
                };
                graph.self = graph;
                return graph;
            })()
            """);

        var record = new StructuredSerializer(engine, engine.Realm).Serialize(value, transferList: null);

        var visited = new HashSet<object>(ReferenceEqualityComparer.Instance);
        var reached = 0;
        Walk(record, visited, ref reached);

        // The walk actually went somewhere — a silently empty traversal would pass vacuously.
        reached.Should().BeGreaterThan(20);
    }

    [Fact]
    public void MovesATransferredBuffersBytesIntoTheRecord()
    {
        var engine = new Engine(options => options.UseWebApis(WebApiFeatures.Messaging));

        engine.Execute("var buffer = new ArrayBuffer(3); var bytes = new Uint8Array(buffer); bytes[0] = 7; bytes[2] = 9;");
        var buffer = engine.Evaluate("buffer");

        var record = new StructuredSerializer(engine, engine.Realm).Serialize(buffer, [buffer]);

        // The record owns the storage now, and the source is detached — which is what makes a transfer a move
        // across an engine boundary rather than a copy that leaves the sender holding a live buffer.
        engine.Evaluate("buffer.byteLength").AsNumber().Should().Be(0);

        var serializedBuffer = record.Root.AsObject().Should().BeOfType<SerializedArrayBuffer>().Subject;
        serializedBuffer.Bytes.Should().Equal([(byte) 7, (byte) 0, (byte) 9]);

        // ... and it deserializes into a different engine, which is the whole point.
        var other = new Engine(options => options.UseWebApis(WebApiFeatures.Messaging));
        var revived = new StructuredDeserializer(other, other.Realm).Deserialize(in record);

        other.SetValue("revived", revived);
        other.Evaluate("revived.byteLength").AsNumber().Should().Be(3);
        other.Evaluate("new Uint8Array(revived)[2]").AsNumber().Should().Be(9);
    }

    [Fact]
    public void CarriesExactlyOneChannelSideForATransferredPortAndNothingElseEngineAffine()
    {
        var engine = new Engine(options => options.UseWebApis(WebApiFeatures.Messaging));

        var channel = engine.Evaluate("new MessageChannel()");
        var port = channel.Get("port2");

        var record = new StructuredSerializer(engine, engine.Realm).Serialize(
            engine.Evaluate("({ tag: 'with a port' })"),
            [port]);

        // The record does reach an Engine and a MessagePort — through the channel side, which is the one
        // engine-crossing handle the design sanctions and whose members a foreign thread may touch. What must
        // NOT be true is that anything else does, so the walk stops there and counts.
        var sides = new List<object>();
        var visited = new HashSet<object>(ReferenceEqualityComparer.Instance);
        var reached = 0;
        Walk(record, visited, ref reached, sides);

        sides.Should().HaveCount(1);
        reached.Should().BeGreaterThan(3);

        // ... and it really is the side the transfer detached, not a copy: the port is now inert.
        engine.SetValue("moved", port);
        engine.Evaluate("moved.postMessage('inert') === undefined").AsBoolean().Should().BeTrue();
    }

    // ---------------------------------------------------------------- helpers

    private static bool Forbidden(Type type) => Array.Exists(_forbidden, forbidden => forbidden.IsAssignableFrom(type));

    /// <summary>
    /// A declared type and every generic argument it names, so <c>List&lt;SerializedProperty&gt;</c> is
    /// checked as both.
    /// </summary>
    private static IEnumerable<Type> DeclaredTypes(Type type)
    {
        yield return type;

        if (type.IsArray && type.GetElementType() is { } element)
        {
            yield return element;
        }

        foreach (var argument in type.GetGenericArguments())
        {
            foreach (var nested in DeclaredTypes(argument))
            {
                yield return nested;
            }
        }
    }

    /// <summary>
    /// Follows every reference a live record graph holds.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The traversal stops at any type declared outside Jint's own assembly other than an array or a
    /// collection — a <see cref="System.Text.RegularExpressions.Regex"/>, an Acornima parse result — because
    /// such a type cannot name a Jint type in its own declarations. That is the one thing this check takes on
    /// trust, and it is the same thing the record types' documentation claims when they carry a compiled
    /// matcher across.
    /// </para>
    /// <para>
    /// It also stops at <c>MessagePortEndpoint</c>, which is the deliberate exception described in the class
    /// remarks: a channel side is engine-affine by definition, because re-pointing a channel is what a port
    /// transfer <i>is</i>. Passing <paramref name="channelSides"/> collects them so that a caller can assert
    /// how many a record actually holds; passing <see langword="null"/> forbids them outright, which is what
    /// every other record in the suite has to satisfy.
    /// </para>
    /// </remarks>
    private static void Walk(object? node, HashSet<object> visited, ref int reached, List<object>? channelSides = null)
    {
        if (node is null)
        {
            return;
        }

        var type = node.GetType();

        if (type.FullName == "Jint.WebApi.Messaging.MessagePortEndpoint")
        {
            channelSides.Should().NotBeNull("only a record that transferred a port may reach a channel side");
            channelSides!.Add(node);
            return;
        }

        Forbidden(type).Should().BeFalse($"a {type.Name} was reachable from a serialization record");

        if (type == typeof(string) || type.IsPrimitive)
        {
            return;
        }

        // Only reference types can be revisited; a struct is copied out of its holder on every read.
        if (!type.IsValueType && !visited.Add(node))
        {
            return;
        }

        reached++;

        if (node is IEnumerable enumerable)
        {
            foreach (var item in enumerable)
            {
                Walk(item, visited, ref reached, channelSides);
            }

            return;
        }

        if (!type.IsValueType && type.Assembly != _jintAssembly)
        {
            return;
        }

        foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
        {
            if (field.FieldType.IsPrimitive || field.FieldType.IsEnum)
            {
                continue;
            }

            Walk(field.GetValue(node), visited, ref reached, channelSides);
        }
    }
}
#endif
