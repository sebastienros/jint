#nullable enable

using System.Linq;
using System.Reflection;
using Jint.Native;

namespace Jint.Tests.PublicInterface;

/// <summary>
/// Narrowing a <see cref="JsValue"/> to a concrete runtime type is C#'s job, not Jint's. Through 4.16.x
/// <c>JsValueExtensions</c> carried four spellings of the language's own <c>as</c> — <c>As&lt;T&gt;()</c>,
/// <c>AsInstance&lt;TInstance&gt;()</c> and two <c>TryCast&lt;T&gt;()</c> overloads — and one of them was
/// declared to return a non-nullable <c>TInstance</c> while returning <c>null</c> whenever the cast missed.
/// A host's nullable flow analysis was told the result could not be null, so no check was written and the
/// <see cref="System.NullReferenceException"/> surfaced somewhere else entirely.
/// <para>
/// These tests are written from outside the assembly, under <c>#nullable enable</c>, because that is the only
/// place the compile-time half of the answer is observable at all.
/// </para>
/// </summary>
public class HostValueNarrowingTests
{
    [Fact]
    public void AMissedNarrowingIsTypedNullableSoTheHostHasToHandleIt()
    {
        var engine = new Engine();

        // `as` types this JsArray?, so the compiler refuses to hand it to Sum below until the null is dealt
        // with. AsInstance<JsArray>() returned this same null through a non-nullable signature.
        var narrowed = engine.Evaluate("({ a: 1 })") as JsArray;

        narrowed.Should().BeNull();
    }

    [Fact]
    public void TheMatchingNarrowingHandsBackACheckedNonNullableBinding()
    {
        var engine = new Engine();

        // The pattern form is the one that produces a binding the compiler already knows is non-null, which
        // is what lets it reach a parameter that does not accept null.
        if (engine.Evaluate("[1, 2, 3]") is not JsArray array)
        {
            Assert.Fail("An array literal must narrow to JsArray.");
            return;
        }

        Sum(array).Should().Be(6);

        static double Sum(JsArray value)
        {
            var total = 0d;
            for (var i = 0; i < (int) value.Length; i++)
            {
                total += value.Get(i).AsNumber();
            }

            return total;
        }
    }

    [Fact]
    public void TheThrowingFormIsACastAndItThrowsWhereItMisses()
    {
        var engine = new Engine();

        Invoking(() => (JsArray) engine.Evaluate("({ a: 1 })")).Should().Throw<System.InvalidCastException>();

        ((JsArray) engine.Evaluate("[1]")).Get(0).AsNumber().Should().Be(1);
    }

    /// <summary>
    /// The regression guard. A helper that narrows for the caller has no way to say "this may be null" that
    /// the caller's compiler can see — its own signature is the only channel, and getting that wrong is
    /// exactly what shipped. So <c>JsValueExtensions</c> declares no such helper at all: nothing on it is
    /// generic in the type being narrowed to.
    /// </summary>
    [Fact]
    public void JsValueExtensionsDeclaresNoGenericNarrowingHelper()
    {
        var offenders = typeof(JsValueExtensions)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(m => m.IsGenericMethodDefinition && m.ReturnType.IsGenericParameter)
            .Select(m => m.Name)
            .ToArray();

        offenders.Should().BeEmpty(
            "narrowing a JsValue is `value as T`, `value is T t` or `(T) value`; a helper cannot express "
            + "the nullability of the answer to the host's compiler, which is how AsInstance<T>() came to "
            + "return null through a non-nullable signature");
    }
}
