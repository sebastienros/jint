using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

using static Jint.Tests.SourceGenerators.VerifyHelper;

namespace Jint.Tests.SourceGenerators;

#pragma warning disable NUnit2045 // the assertions describe one location each, and the first failure names it

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

    /// <summary>
    /// Every reported decline has to point at a syntax tree the compilation actually holds, because that is
    /// the one thing an <c>.editorconfig</c> severity depends on: the compiler resolves
    /// <c>dotnet_diagnostic.JINT03x.severity</c> per tree, and falls back to the global configuration alone
    /// when <c>Location.SourceTree</c> is <see langword="null"/>. A diagnostic whose location was rebuilt
    /// from a file path — which is what <c>Location.Create(string, TextSpan, LinePositionSpan)</c> produces,
    /// and what the sibling generator's <c>DiagnosticInfo</c> does — therefore keeps its default severity
    /// whatever a host writes, silently. Nothing else in this suite would notice: the snapshots record the
    /// line and column either way.
    /// </summary>
    [Test]
    public void EveryDiagnosticPointsAtATreeTheCompilationHolds()
    {
        var (diagnostics, compilation) = VerifyHelper.RunJsAccessibleGeneratorFor("""
            using Jint;
            using Jint.Native;

            namespace Sample;

            [JsAccessible]
            public sealed class Model
            {
                public int Kept { get; set; }
                public int PrivateSetter { get; private set; }
                public string Shout(string text) => text;
                public static int StaticMethod() => 0;
                public int this[string key] => 0;
            }

            [JsAccessible]
            public abstract class Abstract
            {
                public int Score { get; set; }
            }

            [JsAccessible]
            public sealed class Marker
            {
            }
            """);

        Assert.That(diagnostics, Is.Not.Empty);

        foreach (var diagnostic in diagnostics)
        {
            Assert.That(
                diagnostic.Location.SourceTree,
                Is.Not.Null,
                $"{diagnostic.Id} has no source tree, so no .editorconfig severity can reach it");

            Assert.That(
                compilation.SyntaxTrees,
                Does.Contain(diagnostic.Location.SourceTree),
                $"{diagnostic.Id} points at a tree the compilation does not hold");
        }
    }

    /// <summary>
    /// Generated code is parsed with the <em>consumer's</em> language version, and the .NET SDK gives a
    /// plain <c>net472</c> or <c>netstandard2.0</c> project C# 7.3 — which is exactly the target where the
    /// run-time compiled lanes decline outright and this feature is worth the most. A <c>#nullable
    /// enable</c>, an <c>object?</c> or a <c>!</c> in the emitted text is therefore not a style question
    /// but a build error in someone else's project, and nothing else in this suite would notice: every
    /// snapshot here is produced with <c>LanguageVersion.Latest</c>.
    /// </summary>
    [Test]
    public void EmittedCodeCompilesAsCSharp7_3()
    {
        // The source is deliberately C# 7.3 itself - block namespace, no target-typed new - so that any
        // error the compilation reports came from the generated files and from nothing else.
        var errors = VerifyHelper.CompileWithJsAccessibleGenerator(
            """
            using Jint;
            using Jint.Native;

            namespace Sample
            {
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
                    public int ReadOnlyScore { get { return 1; } }

                    public JsValue Echo(JsValue value) { return value; }
                    public void Touch(JsValue value) { }
                    public int Count() { return 0; }
                }
            }
            """,
            LanguageVersion.CSharp7_3);

        Assert.That(
            errors,
            Is.Empty,
            "the emitted code must compile at the language version the .NET SDK gives a plain net472 project: "
            + string.Join("; ", errors.Select(static e => e.ToString())));
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
