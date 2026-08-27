#nullable enable

using System;

namespace Jint.Tests.Runtime;

/// <summary>
/// A group cloned from a frozen <see cref="Options"/> comes back frozen, registries included.
/// </summary>
/// <remarks>
/// <para>
/// Every group's own <c>Clone</c> is <c>MemberwiseClone</c>, which carries the group's frozen state for
/// nothing. A registry cannot be copied that way — its list has to be a new one, or the copy would go on
/// sharing the source's — so <see cref="OptionsList{T}"/> has a hand-written <c>Clone</c>, and a freshly
/// constructed registry used to be born writable. That left a frozen group wrapping writable registries:
/// <c>IsReadOnly</c> answering <see langword="true"/> on <c>Options.Interop</c> and <see langword="false"/>
/// on <c>Interop.ObjectConverters</c> beside it.
/// </para>
/// <para>
/// Nothing shipped was broken by it, because the two callers each compensated — <c>CloneWithPrivateWebApiOptions</c>
/// re-froze the subtree it had just copied, and <c>CreateEngineOptions</c> thaws the whole clone immediately.
/// This is what makes those compensations unnecessary rather than load-bearing, so that a third caller of
/// <c>InteropOptions.Clone</c> or <c>ConstraintOptions.Clone</c> — a future per-engine copy, a profile that
/// snapshots one group — gets the state the source had instead of having to know to repair it.
/// </para>
/// </remarks>
public class OptionsCloneReadOnlyTests
{
    [Test]
    public void AGroupClonedFromFrozenOptionsIsFrozenAroundItsRegistriesToo()
    {
        var options = new Options();
        options.Interop.ExtensionMethodTypes.Add(typeof(string));
        options.MakeReadOnly();

        // The two groups the issue names, because neither caller of theirs compensates for the asymmetry.
        var interop = options.Interop.Clone();
        var constraints = options.Constraints.Clone();

        const string Because = "a clone of a frozen group must not be frozen around a writable registry";

        interop.ExtensionMethodTypes.IsReadOnly.Should().BeTrue(Because);
        interop.ObjectConverters.IsReadOnly.Should().BeTrue(Because);
        interop.ImmutableCrossingTypes.IsReadOnly.Should().BeTrue(Because);
        interop.AllowedAssemblies.IsReadOnly.Should().BeTrue(Because);
        constraints.Constraints.IsReadOnly.Should().BeTrue(Because);
        constraints.ConstraintFactories.IsReadOnly.Should().BeTrue(Because);

        Invoking(() => interop.ExtensionMethodTypes.Add(typeof(int)))
            .Should().Throw<InvalidOperationException>();
        Invoking(() => constraints.Constraints.Clear())
            .Should().Throw<InvalidOperationException>();

        // It is still a copy rather than the source's own list: freezing it must not have made it shared.
        interop.ExtensionMethodTypes.Should().ContainSingle().Which.Should().Be(typeof(string));
        interop.ExtensionMethodTypes.Should().NotBeSameAs(options.Interop.ExtensionMethodTypes);
    }

    /// <summary>
    /// The other half: the one caller that wants a writable copy asks for it, and gets it all the way down.
    /// </summary>
    [Test]
    public void TheUntrustedProfilesPrivateCopyIsThawedThroughout()
    {
        var options = new Options();
        options.Interop.ExtensionMethodTypes.Add(typeof(string));
        options.ForUntrustedCode(new UntrustedCodeLimits
        {
            TimeoutInterval = TimeSpan.FromSeconds(5),
            MaxStatements = 100_000,
            MemoryLimit = 16_000_000,
            MaxRecursionDepth = 64,
            MaxArraySize = 10_000,
            RegexTimeout = TimeSpan.FromMilliseconds(100),
            PromiseTimeout = TimeSpan.FromMilliseconds(100),
            MaxOperationDuration = TimeSpan.FromSeconds(10),
        });
        options.MakeReadOnly();

        var forEngine = options.CreateEngineOptions();

        forEngine.Should().NotBeSameAs(options);
        forEngine.IsReadOnly.Should().BeFalse();
        forEngine.Interop.ExtensionMethodTypes.IsReadOnly.Should().BeFalse();
        forEngine.Constraints.Constraints.IsReadOnly.Should().BeFalse();
        forEngine.Constraints.ConstraintFactories.IsReadOnly.Should().BeFalse();
    }
}
