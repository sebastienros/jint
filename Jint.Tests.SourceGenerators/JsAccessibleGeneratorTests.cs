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

    [Test]
    public Task IneligibleType_ProducesDiagnostic()
    {
        // JINT030, once per reason a whole type stays on the reflection path. The record is the one that
        // used to be dropped by the syntax predicate before any of this could run.
        return VerifyJsAccessibleGenerator("""
            using Jint;

            namespace Sample;

            [JsAccessible]
            public abstract class Abstract
            {
                public int Score { get; set; }
            }

            [JsAccessible]
            public static class Statics
            {
                public static int Score { get; set; }
            }

            [JsAccessible]
            public sealed class Generic<T>
            {
                public int Score { get; set; }
            }

            [JsAccessible]
            public sealed record Recorded
            {
                public int Score { get; set; }
            }

            public sealed class Outer
            {
                [JsAccessible]
                private sealed class Hidden
                {
                    public int Score { get; set; }
                }
            }
            """);
    }

    [Test]
    public Task TypeRegistersNothing_ProducesDiagnostic()
    {
        // JINT031. The static members are not reported themselves: the default reported binding flags for
        // properties and fields are Public | Instance, so declining them takes nothing away.
        return VerifyJsAccessibleGenerator("""
            using Jint;

            namespace Sample;

            [JsAccessible]
            public sealed class Marker
            {
            }

            [JsAccessible]
            public sealed class OnlyStatics
            {
                public static int Version { get; set; }
                public static readonly string Name = "";
            }
            """);
    }

    [Test]
    public Task AmbiguousMethodName_ProducesDiagnostic()
    {
        // JINT032, in its three shapes: two declarations on the type, one inherited from a base type
        // (System.Object counts, which is why an override of ToString is never taken), and one an
        // implemented interface also declares.
        return VerifyJsAccessibleGenerator("""
            using Jint;
            using Jint.Native;

            namespace Sample;

            public interface INamed
            {
                JsValue Describe(JsValue prefix);
            }

            [JsAccessible]
            public sealed class Model : INamed
            {
                public int Kept { get; set; }

                public JsValue Overloaded(JsValue value) => value;
                public JsValue Overloaded(JsValue first, JsValue second) => first;

                public override string ToString() => "";

                public JsValue Describe(JsValue prefix) => prefix;
            }
            """);
    }

    [Test]
    public Task MethodSignature_ProducesDiagnostic()
    {
        // JINT033. Every one of these has a reflected binding steered by engine options or by the binder,
        // and reproducing it in emitted code is where a generated accessor would stop being equivalent.
        return VerifyJsAccessibleGenerator("""
            using Jint;
            using Jint.Native;

            namespace Sample;

            [JsAccessible]
            public sealed class Model
            {
                private int _slot;

                public int Kept { get; set; }

                public static int Utility() => 0;
                public T Generic<T>(JsValue value) => default;
                public ref int Slot() => ref _slot;
                public JsValue Coerced(string text) => text;
                public JsValue Defaulted(JsValue value, JsValue other = null) => value;
                public JsValue Spread(params JsValue[] values) => values[0];
                public JsValue ByRef(ref JsValue value) => value;
            }
            """);
    }

    [Test]
    public Task MemberDeclaration_ProducesDiagnostic()
    {
        // JINT034. Reflection reads and writes every one of these; emitted C# cannot, and half a member
        // would be worse than none. What is deliberately not here is a `const` field: a constant is static,
        // so the default reported field binding flags never report it and declining it costs nothing —
        // ReadOnlyAndWriteOnlyMembers has one, and produces no diagnostics file at all.
        return VerifyJsAccessibleGenerator("""
            using Jint;

            namespace Sample;

            [JsAccessible]
            public sealed class Model
            {
                private int _slot;

                public int Kept { get; set; }

                public int PrivateSetter { get; private set; }
                public int PrivateGetter { private get; set; }
                public int InitOnly { get; init; }
                public ref int Slot => ref _slot;
            }
            """);
    }

    [Test]
    public Task MemberTypeCannotBeNamed_ProducesDiagnostic()
    {
        // JINT035. Not "the engine has no lane for this type" - the boxed lane carries anything - but "the
        // emitted file cannot name or box it at all".
        return VerifyJsAccessibleGenerator("""
            using Jint;

            namespace Sample;

            public ref struct Handle
            {
            }

            [JsAccessible]
            public sealed class Model
            {
                public int Kept { get; set; }

                public Handle Borrowed => default;
                public unsafe int* Address => null;
                public Handle Borrow() => default;
            }
            """);
    }

    [Test]
    public Task Indexer_ProducesDiagnostic()
    {
        // JINT036, which is not merely "this member is declined": an indexer is probed ahead of the
        // declared members, so every name it answers for resolves through it whatever the registry holds.
        return VerifyJsAccessibleGenerator("""
            using Jint;

            namespace Sample;

            [JsAccessible]
            public sealed class Model
            {
                public string Name { get; set; }

                public string this[string key]
                {
                    get => "";
                    set { }
                }
            }
            """);
    }
}
