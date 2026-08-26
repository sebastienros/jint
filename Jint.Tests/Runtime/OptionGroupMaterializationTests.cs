#nullable enable

using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Jint.Tests.Runtime;

/// <summary>
/// Every option group is materialized on first access through the one <c>Options.Materialize</c> helper, so a
/// default <see cref="Options"/> — the one every <c>new Engine()</c> builds — allocates none of them.
/// </summary>
/// <remarks>
/// Ten of the groups were allocated eagerly with the <see cref="Options"/> instance before v5, and only
/// <c>WebApi</c> was lazy. This is the pin that keeps the shape uniform: it reflects over the backing fields
/// rather than the properties, because reading a property is exactly what materializes it, and it derives the
/// set of groups from the fields' own types so a group added later is covered without touching this file.
/// </remarks>
public class OptionGroupMaterializationTests
{
    private static IReadOnlyList<FieldInfo> GroupFields(object instance) =>
        instance.GetType()
            .GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
            .Where(static f => f.FieldType.DeclaringType == typeof(Options) && f.FieldType.Name.EndsWith("Options"))
            .ToList();

    [Test]
    public void ADefaultOptionsInstanceMaterializesNoGroup()
    {
        var options = new Options();
        var fields = GroupFields(options);

        fields.Should().NotBeEmpty("Options is expected to hold its groups in backing fields");
        foreach (var field in fields)
        {
            field.GetValue(options).Should().BeNull(
                $"a default Options must not allocate {field.FieldType.Name}");
        }
    }

    [Test]
    public void ReadingOneGroupMaterializesOnlyThatGroup()
    {
        var options = new Options();
        _ = options.Constraints;

        foreach (var field in GroupFields(options))
        {
            var value = field.GetValue(options);
            if (field.FieldType == typeof(Options.ConstraintOptions))
            {
                value.Should().NotBeNull();
            }
            else
            {
                value.Should().BeNull($"nothing touched {field.FieldType.Name}");
            }
        }
    }

    [Test]
    public void EveryGroupCanBeCloned()
    {
        // CreateEngineOptions clones every materialized group so the untrusted-code profile's private
        // snapshot never shares state with the host's options. A group without a Clone() would be shared by
        // reference and the profile would harden the caller's object instead of its own copy.
        var options = new Options();
        foreach (var field in GroupFields(options))
        {
            field.FieldType
                .GetMethod("Clone", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                .Should().NotBeNull($"{field.FieldType.Name} needs a Clone() for CreateEngineOptions");
        }
    }
}
