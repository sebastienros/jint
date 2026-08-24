using static Jint.Tests.SourceGenerators.VerifyHelper;

namespace Jint.Tests.SourceGenerators;

#pragma warning disable NUnit1032 // Verify is used as a static helper, not async-disposable infra

/// <summary>
/// The emitted text for <c>[JsAccessible]</c>. The behavioural contract lives in
/// <c>Jint.Tests.PublicInterface</c>, where it is asserted against the reflected path from outside the
/// assembly; these snapshots are what makes a change to the emitted code visible rather than merely
/// still-passing.
/// </summary>
[TestFixture]
public class JsAccessibleGeneratorTests
{
    [Test]
    public Task EveryMemberLane()
    {
        // int / long / double / bool / string / JsValue take both typed lanes; a JsValue subtype takes the
        // typed read lane only (a write of one goes through the type converter, exactly as the run-time
        // compiled lane leaves it); anything else is reached through the boxed lanes alone.
        return VerifyJsAccessibleGenerator("""
            using Jint;
            using Jint.Native;

            namespace Sample;

            [JsAccessible]
            public sealed class Model
            {
                public int Score { get; set; }
                public long Ticks { get; set; }
                public double Ratio { get; set; }
                public bool Active { get; set; }
                public string Name { get; set; }
                public JsValue Payload { get; set; }
                public JsString Tag { get; set; }
                public string[] Tags { get; set; }
            }
            """);
    }

    [Test]
    public Task ReadOnlyAndWriteOnlyMembers()
    {
        return VerifyJsAccessibleGenerator("""
            using Jint;

            namespace Sample;

            [JsAccessible]
            public sealed class Model
            {
                public int GetOnly => 1;
                public int SetOnly { set { } }
                public int Field;
                public readonly string ReadOnlyField = "";
                public const int Constant = 3;
            }
            """);
    }

    [Test]
    public Task Methods()
    {
        return VerifyJsAccessibleGenerator("""
            using Jint;
            using Jint.Native;

            namespace Sample;

            [JsAccessible]
            public sealed class Model
            {
                public JsValue Echo(JsValue value) => value;
                public void Touch(JsValue value) { }
                public int Count() => 0;
                public string Describe(JsValue a, JsValue b) => "";
            }
            """);
    }

    [Test]
    public Task ShapesTheGeneratorDeclines()
    {
        // Each of these has a reflected binding the generator will not reproduce, so none of them appears in
        // the output and every one keeps the reflection path it had before the type was annotated.
        return VerifyJsAccessibleGenerator("""
            using Jint;
            using Jint.Native;
            using System.Collections.Generic;

            namespace Sample;

            [JsAccessible]
            public sealed class Model
            {
                public int Kept { get; set; }

                // reflection writes a non-public accessor; generated code cannot
                public int PrivateSetter { get; private set; }
                public int PrivateGetter { private get; set; }
                public int InitOnly { get; init; }

                // resolved by MethodInfoFunction's overload scoring
                public int Overloaded(int value) => value;
                public int Overloaded(int first, int second) => first + second;

                // a parameter whose reflected binding is a conversion steered by engine options
                public string Shout(string text) => text;
                public int Optional(JsValue value, JsValue other = null) => 0;
                public int Params(params JsValue[] values) => 0;
                public T Generic<T>(JsValue value) => default;

                // static members are not instance members
                public static int Static { get; set; }
                public static int StaticMethod() => 0;

                // an indexer is served by IndexerAccessor
                public int this[int index] => index;

                // not public
                internal int Internal { get; set; }
            }
            """);
    }

    [Test]
    public Task NestedTypeAndItsTopLevelNamesake()
    {
        // The MVP derived a hint name from {Namespace}.{Name}, so these two claimed the same one and the
        // second AddSource threw "hint name already added" instead of reporting anything.
        return VerifyJsAccessibleGenerator("""
            using Jint;

            namespace Sample;

            public static class Outer
            {
                [JsAccessible]
                public sealed class Player
                {
                    public string Name { get; set; }
                }
            }

            [JsAccessible]
            public sealed class Player
            {
                public string Name { get; set; }
            }
            """);
    }

    [Test]
    public Task PartialDeclarations()
    {
        // Two files, one symbol: every part's members have to be emitted, and exactly one file produced.
        return VerifyJsAccessibleGenerator([
            """
            using Jint;

            namespace Sample;

            [JsAccessible]
            public sealed partial class Model
            {
                public int First { get; set; }
            }
            """,
            """
            namespace Sample;

            public sealed partial class Model
            {
                public int Second { get; set; }
            }
            """
        ]);
    }

    [Test]
    public Task GlobalNamespace()
    {
        return VerifyJsAccessibleGenerator("""
            using Jint;

            [JsAccessible]
            public sealed class Rootless
            {
                public int Score { get; set; }
            }
            """);
    }

    [Test]
    public Task TypesTheGeneratorDeclinesEntirely()
    {
        // Nothing is emitted at all - not even a registration entry point.
        return VerifyJsAccessibleGenerator("""
            using Jint;

            namespace Sample;

            [JsAccessible]
            public abstract class Abstract
            {
                public int Score { get; set; }
            }

            [JsAccessible]
            public sealed class Generic<T>
            {
                public int Score { get; set; }
            }

            [JsAccessible]
            public sealed class NoExpressibleMembers
            {
                public static int Score { get; set; }
            }
            """);
    }
}
