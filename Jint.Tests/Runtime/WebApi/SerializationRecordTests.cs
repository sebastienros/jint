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
/// Two independent checks, because either one alone can be fooled. The declaration check reads the record
/// types' own fields, which catches a new record type that stores a <c>JsValue</c> outright; it cannot see
/// through <see cref="SerializedValue"/>'s <c>object</c> payload. The graph walk serializes a deliberately
/// rich value and follows every reference the result actually holds, which does see through it but can only
/// speak for the shapes the sample covers. Together they cover the rule.
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
    /// The traversal stops at any type declared outside Jint's own assembly other than an array or a
    /// collection — a <see cref="System.Text.RegularExpressions.Regex"/>, an Acornima parse result — because
    /// such a type cannot name a Jint type in its own declarations. That is the one thing this check takes on
    /// trust, and it is the same thing the record types' documentation claims when they carry a compiled
    /// matcher across.
    /// </remarks>
    private static void Walk(object? node, HashSet<object> visited, ref int reached)
    {
        if (node is null)
        {
            return;
        }

        var type = node.GetType();
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
                Walk(item, visited, ref reached);
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

            Walk(field.GetValue(node), visited, ref reached);
        }
    }
}
#endif
