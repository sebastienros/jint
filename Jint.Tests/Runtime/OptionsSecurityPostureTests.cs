#nullable enable

using System.Collections.Generic;
using System.Reflection;

namespace Jint.Tests.Runtime;

/// <summary>
/// The classification behind <c>Options.CopySecurityPosture</c>: every value-typed setting an engine's
/// posture is made of is either inherited by a second engine built from the first, or explicitly named as
/// something that stays behind.
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
/// three groups the copy reads — <c>Constraints</c>, <c>Host</c> and <c>Json</c> — and within them the
/// <b>value-typed</b> public settable properties, which is what "a setting" means here. Delegates, collections
/// and anything reference-typed are host wiring rather than posture: a resolver, a loader, a converter or a
/// provider is an object the host hands over deliberately, per engine. Every other option group is excluded
/// wholesale, and <c>Options.SecurityPostureExcludedGroups</c> is where each exclusion is argued — a group
/// added later has to be classified rather than quietly falling outside the rule.
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
    ];

    private static List<string> ValueSettings()
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

                if (property.SetMethod is not { IsPublic: true } || !property.PropertyType.IsValueType)
                {
                    continue;
                }

                names.Add(prefix + property.Name);
            }
        }

        return names;
    }

    [Fact]
    public void EverySecurityShapedOptionIsClassified()
    {
        var inherited = new HashSet<string>(Options.SecurityPostureInherited, StringComparer.Ordinal);
        var notInherited = new HashSet<string>(Options.SecurityPostureNotInherited, StringComparer.Ordinal);

        foreach (var setting in ValueSettings())
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

    [Fact]
    public void NoSettingIsClassifiedBothWays()
    {
        var inherited = new HashSet<string>(Options.SecurityPostureInherited, StringComparer.Ordinal);

        foreach (var setting in Options.SecurityPostureNotInherited)
        {
            inherited.Should().NotContain(setting, "'{0}' cannot both travel and stay behind", setting);
        }
    }

    [Fact]
    public void NeitherClassificationNamesASettingThatNoLongerExists()
    {
        var settings = new HashSet<string>(ValueSettings(), StringComparer.Ordinal);

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
    [Fact]
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
