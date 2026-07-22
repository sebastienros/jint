#nullable enable

using System.Diagnostics;
using AwesomeAssertions.Execution;
using AwesomeAssertions.Primitives;
using Jint.Native;

namespace Jint.Tests;

/// <summary>
/// Makes <see cref="JsValue"/> a first-class citizen in assertions, so that a script result can be
/// compared against a CLR value directly: <c>engine.Evaluate("1 + 1").Should().Be(2)</c>. The implicit
/// conversions on <see cref="JsValue"/> turn the expectation into the matching JavaScript value, and
/// the comparison is the same strict equality the engine itself uses.
/// </summary>
internal static class JsValueAssertionExtensions
{
    public static JsValueAssertions Should(this JsValue? value) => new(value, AssertionChain.GetOrCreate());
}

/// <inheritdoc cref="JsValueAssertionExtensions" />
[DebuggerNonUserCode]
internal sealed class JsValueAssertions(JsValue? subject, AssertionChain assertionChain)
    : ReferenceTypeAssertions<JsValue?, JsValueAssertions>(subject, assertionChain)
{
    protected override string Identifier => "value";

    /// <summary>
    /// Asserts that the value is strictly equal to <paramref name="expected"/>.
    /// </summary>
    [CustomAssertion]
    public AndConstraint<JsValueAssertions> Be(JsValue? expected, string because = "", params object[] becauseArgs)
    {
        CurrentAssertionChain
            .BecauseOf(because, becauseArgs)
            .ForCondition(StrictEquals(Subject, expected))
            .FailWith("Expected {context:value} to be {0}{reason}, but found {1}.", Describe(expected), Describe(Subject));

        return new AndConstraint<JsValueAssertions>(this);
    }

    /// <summary>
    /// Asserts that the value is not strictly equal to <paramref name="unexpected"/>.
    /// </summary>
    [CustomAssertion]
    public AndConstraint<JsValueAssertions> NotBe(JsValue? unexpected, string because = "", params object[] becauseArgs)
    {
        CurrentAssertionChain
            .BecauseOf(because, becauseArgs)
            .ForCondition(!StrictEquals(Subject, unexpected))
            .FailWith("Did not expect {context:value} to be {0}{reason}.", Describe(unexpected));

        return new AndConstraint<JsValueAssertions>(this);
    }

    /// <summary>
    /// Asserts that the value is the JavaScript <c>true</c>.
    /// </summary>
    [CustomAssertion]
    public AndConstraint<JsValueAssertions> BeTrue(string because = "", params object[] becauseArgs) =>
        Be(JsBoolean.True, because, becauseArgs);

    /// <summary>
    /// Asserts that the value is the JavaScript <c>false</c>.
    /// </summary>
    [CustomAssertion]
    public AndConstraint<JsValueAssertions> BeFalse(string because = "", params object[] becauseArgs) =>
        Be(JsBoolean.False, because, becauseArgs);

    /// <summary>
    /// Asserts that the value is <c>undefined</c>.
    /// </summary>
    [CustomAssertion]
    public AndConstraint<JsValueAssertions> BeUndefined(string because = "", params object[] becauseArgs) =>
        Be(JsValue.Undefined, because, becauseArgs);

    private static bool StrictEquals(JsValue? left, JsValue? right) => left is null ? right is null : left.Equals(right);

    // The default formatter renders every JsValue as its bare string form, which makes "expected 1 but
    // found 1" style messages when only the JavaScript type differs. Spell the type out instead.
    private static string Describe(JsValue? value) => value is null ? "<null>" : $"{value} ({value.Type})";
}
