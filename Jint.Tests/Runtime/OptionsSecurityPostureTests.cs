#nullable enable

using System.Collections.Generic;
using System.Reflection;

namespace Jint.Tests.Runtime;

/// <summary>
/// The classification behind <c>Options.CopySecurityPosture</c>: every setting an engine's posture is made
/// of is either inherited by a second engine built from the first, or explicitly named as something that
/// stays behind.
/// </summary>
/// <remarks>
/// <para>
/// This is the pin that makes the rule survive the next settings PR. The copy exists because a worker (and
/// any other second engine built from a first) must be able to be given <i>less</i> than its creator and
/// never more — so a hardened parent's <c>StringCompilationAllowed = false</c> travels, while its CLR interop
/// grant does not. A new <c>Options</c> setting that is neither copied nor classified would silently become a
/// way around whatever hardening the host had applied; this test fails until somebody decides which it is.
/// </para>
/// <para>
/// The scanned scope is deliberately narrow and stated in one place: <see cref="Options"/> itself plus the
/// groups the copy reads — <c>Constraints</c>, <c>Host</c>, <c>Json</c>, <c>Parsing</c> and <c>Modules</c> —
/// and within them <b>every</b> public settable property, which is what "a setting" means here. Every other
/// option group is excluded wholesale, and <c>Options.SecurityPostureExcludedGroups</c> is where each
/// exclusion is argued — a group added later has to be classified rather than quietly falling outside the
/// rule.
/// </para>
/// <para>
/// <b>Reference-typed settings are scanned too, and that is a correction.</b> The scan used to stop at
/// value-typed properties on the reasoning that a resolver, a loader, a converter or a provider is host
/// wiring the host hands over deliberately, per engine — which is true of almost all of them and is still
/// the reason most of them are in <c>SecurityPostureNotInherited</c>. What it is not true of is
/// <c>Constraints.TimeProvider</c>: a clock is the yardstick two <i>inherited</i> budgets are measured
/// against, and it fell outside the rule entirely, so a worker ran its <c>PromiseTimeout</c> and its
/// <c>LimitExecutionTime</c> against two different clocks and nothing here said a word
/// (<see href="https://github.com/sebastienros/jint/issues/3481">#3481</see>). "Reference-typed, therefore
/// not a setting" was a rule nobody could rely on; each one argues for itself now, which is also what
/// retires the hand-written comment <c>ResultLimits</c> had to carry.
/// </para>
/// </remarks>
public class OptionsSecurityPostureTests
{
    /// <summary>
    /// The types scanned, and the prefix a setting on each is named with. A setting on <see cref="Options"/>
    /// itself is named bare.
    /// </summary>
    private static readonly (string Prefix, Type Type)[] ScannedGroups =
    [
        ("", typeof(Options)),
        ("Constraints.", typeof(Options.ConstraintOptions)),
        ("Host.", typeof(Options.HostOptions)),
        ("Json.", typeof(Options.JsonOptions)),
        ("Parsing.", typeof(Options.ParsingOptions)),
        ("Modules.", typeof(Options.ModuleOptions)),
    ];

    /// <summary>
    /// Every public settable property on the scanned types, named the way the classification lists name it.
    /// </summary>
    /// <remarks>
    /// A public setter is the whole definition of "a setting" here: it is what a host can change, and
    /// therefore what a worker can differ from its creator on. Indexers are skipped because
    /// <see cref="Options"/> has none and a keyed bag is not a setting.
    /// </remarks>
    private static List<string> Settings()
    {
        var names = new List<string>();

        foreach (var (prefix, type) in ScannedGroups)
        {
            foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (property.GetIndexParameters().Length > 0)
                {
                    continue;
                }

                if (property.SetMethod is not { IsPublic: true })
                {
                    continue;
                }

                names.Add(prefix + property.Name);
            }
        }

        return names;
    }

    [Test]
    public void EverySecurityShapedOptionIsClassified()
    {
        var inherited = new HashSet<string>(Options.SecurityPostureInherited, StringComparer.Ordinal);
        var notInherited = new HashSet<string>(Options.SecurityPostureNotInherited, StringComparer.Ordinal);

        foreach (var setting in Settings())
        {
            var classified = inherited.Contains(setting) || notInherited.Contains(setting);

            classified.Should().BeTrue(
                "Options setting '{0}' is neither copied by Options.CopySecurityPosture nor named as deliberately not inherited. "
                + "Decide which it is and add it to Options.SecurityPostureInherited (and to the body of CopySecurityPosture) "
                + "if it restricts what script may do, or to Options.SecurityPostureNotInherited with a one-line reason if it "
                + "grants a capability, is engine-affine, or is meaningless for a freshly built engine. "
                + "Inherited today: [{1}]. Not inherited today: [{2}].",
                setting,
                string.Join(", ", Options.SecurityPostureInherited),
                string.Join(", ", Options.SecurityPostureNotInherited));
        }
    }

    [Test]
    public void NoSettingIsClassifiedBothWays()
    {
        var inherited = new HashSet<string>(Options.SecurityPostureInherited, StringComparer.Ordinal);

        foreach (var setting in Options.SecurityPostureNotInherited)
        {
            inherited.Should().NotContain(setting, "'{0}' cannot both travel and stay behind", setting);
        }
    }

    [Test]
    public void NeitherClassificationNamesASettingThatNoLongerExists()
    {
        var settings = new HashSet<string>(Settings(), StringComparer.Ordinal);

        foreach (var setting in Options.SecurityPostureInherited)
        {
            settings.Should().Contain(setting, "Options.SecurityPostureInherited names a setting that no longer exists");
        }

        foreach (var setting in Options.SecurityPostureNotInherited)
        {
            settings.Should().Contain(setting, "Options.SecurityPostureNotInherited names a setting that no longer exists");
        }
    }

    /// <summary>
    /// The wholesale exclusions are stated in code rather than left implicit, so an option group added later
    /// fails this until it is either scanned or argued away.
    /// </summary>
    [Test]
    public void EveryOptionGroupIsEitherScannedOrExcludedInWriting()
    {
        var scanned = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (prefix, _) in ScannedGroups)
        {
            if (prefix.Length > 0)
            {
                scanned.Add(prefix.TrimEnd('.'));
            }
        }

        var excluded = new HashSet<string>(Options.SecurityPostureExcludedGroups, StringComparer.Ordinal);

        foreach (var property in typeof(Options).GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            // An option group is a type declared inside Options: Constraints, Interop, Host, WebApi, …
            if (property.PropertyType.DeclaringType != typeof(Options))
            {
                continue;
            }

            var classified = scanned.Contains(property.Name) || excluded.Contains(property.Name);

            classified.Should().BeTrue(
                "Options group '{0}' is neither scanned by the security-posture rule nor named in "
                + "Options.SecurityPostureExcludedGroups. Add it to one, with the reason. Scanned: [{1}]. Excluded: [{2}].",
                property.Name,
                string.Join(", ", scanned),
                string.Join(", ", Options.SecurityPostureExcludedGroups));

            (scanned.Contains(property.Name) && excluded.Contains(property.Name)).Should().BeFalse(
                "group '{0}' cannot be both scanned and excluded",
                property.Name);
        }
    }
}
