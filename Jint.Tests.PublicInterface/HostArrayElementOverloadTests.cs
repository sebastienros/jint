#nullable enable

using System.Globalization;
using Jint.Native;
using Jint.Runtime;

namespace Jint.Tests.PublicInterface;

/// <summary>
/// Overload candidates that differ only in the element type of an array or collection parameter are chosen
/// by that element type, and an element the conversion cannot perform declines instead of throwing.
/// </summary>
/// <remarks>
/// <para>
/// A <c>params</c> call bundles its trailing arguments into one JavaScript array before scoring, so scoring
/// saw a single argument against <c>CDecimal[]</c>, <c>CInteger[]</c> and <c>CLong[]</c> alike and answered a
/// blanket "is an array, wants an array" for every one of them. Every candidate tied, the converter probe
/// below that rule — the rule that would have declined <c>CInteger -&gt; CDecimal</c> — was never reached, and
/// declaration order decided. Where the first-declared candidate happened to be convertible the wrong
/// overload answered silently; where it was not, the conversion's <see cref="InvalidCastException"/> left
/// through the embedder's <c>Evaluate</c>, because the composite branches of <c>TryConvert</c> converted their
/// parts through the throwing <c>Convert</c> and so escaped the candidate loop that exists to move on.
/// </para>
/// <para>
/// <see href="https://github.com/sebastienros/jint/issues/3754">#3754</see>, reported in
/// <see href="https://github.com/sebastienros/jint/discussions/3746">discussion #3746</see>; the
/// same shape as <see href="https://github.com/sebastienros/jint/issues/3407">#3407</see> and
/// <see href="https://github.com/sebastienros/jint/issues/3577">#3577</see>, one level down — a score that
/// claims a binding the conversion cannot perform.
/// </para>
/// </remarks>
public class HostArrayElementOverloadTests
{
    // Three unrelated host classes, none of them IConvertible and none declaring a conversion to any other:
    // exactly the shape of the report, where nothing but the element type tells the overloads apart.

    public sealed class CDecimal
    {
        public CDecimal(decimal value) => Value = value;

        public decimal Value { get; }

        public string Kind => nameof(CDecimal);
    }

    public sealed class CInteger
    {
        public CInteger(int value) => Value = value;

        public int Value { get; }

        public string Kind => nameof(CInteger);
    }

    public sealed class CLong
    {
        public CLong(long value) => Value = value;

        public long Value { get; }

        public string Kind => nameof(CLong);
    }

    public sealed class MathHost
    {
        public CDecimal Add(params CDecimal[] args)
        {
            decimal sum = 0;
            foreach (var arg in args)
            {
                sum += arg.Value;
            }

            return new CDecimal(sum);
        }

        public CInteger Add(params CInteger[] args)
        {
            var sum = 0;
            foreach (var arg in args)
            {
                sum += arg.Value;
            }

            return new CInteger(sum);
        }

        public CLong Add(params CLong[] args)
        {
            long sum = 0;
            foreach (var arg in args)
            {
                sum += arg.Value;
            }

            return new CLong(sum);
        }
    }

    /// <summary>Both elements convert to both parameter types; only the score can tell the two apart.</summary>
    public sealed class StringDeclaredFirst
    {
        public string Join(params string[] values) => string.Concat(values);

        public string Join(params int[] values)
        {
            var sum = 0;
            foreach (var value in values)
            {
                sum += value;
            }

            return sum.ToString(CultureInfo.InvariantCulture);
        }
    }

    /// <summary>The same pair in the opposite declaration order.</summary>
    public sealed class IntDeclaredFirst
    {
        public string Join(params int[] values)
        {
            var sum = 0;
            foreach (var value in values)
            {
                sum += value;
            }

            return sum.ToString(CultureInfo.InvariantCulture);
        }

        public string Join(params string[] values) => string.Concat(values);
    }

    public sealed class CollectionHost
    {
        public string Sum(List<CDecimal> values) => "CDecimal:" + values.Count;

        public string Sum(List<CInteger> values)
        {
            var sum = 0;
            foreach (var value in values)
            {
                sum += value.Value;
            }

            return "CInteger:" + sum.ToString(CultureInfo.InvariantCulture);
        }
    }

    public sealed class CatchAllHost
    {
        public string Take(object value) => "object";

        public string Take(params object[] values) => "params:" + values.Length;
    }

    public sealed class JsValueParamsHost
    {
        public string Take(char value) => "char:" + value;

        public string Take(params JsValue[] values) => "params:" + values.Length;
    }

    public sealed class Bag
    {
        public Bag(params CDecimal[] items) => Kind = "CDecimal:" + items.Length;

        public Bag(params CInteger[] items) => Kind = "CInteger:" + items.Length;

        public string Kind { get; }
    }

    private static Engine NewEngine()
    {
        var engine = new Engine();
        engine.SetValue("math", new MathHost());
        engine.SetValue("stringFirst", new StringDeclaredFirst());
        engine.SetValue("intFirst", new IntDeclaredFirst());
        engine.SetValue("collections", new CollectionHost());
        engine.SetValue("catchAll", new CatchAllHost());
        engine.SetValue("jsValues", new JsValueParamsHost());
        engine.SetValue("Bag", typeof(Bag));
        engine.SetValue("a", new CInteger(1));
        engine.SetValue("b", new CInteger(2));
        return engine;
    }

    // ---- method lane ---------------------------------------------------------------------------------

    [Test]
    public void AParamsOverloadIsChosenByTheElementTypeTheArgumentsActuallyAre()
    {
        // The report verbatim. CDecimal is declared first and nothing converts a CInteger to it, so the call
        // used to die inside Convert.ChangeType with "Object must implement IConvertible" - a CLR exception
        // out of Evaluate that neither a script catch nor a host catch (JavaScriptException) could see.
        var engine = NewEngine();

        engine.Evaluate("math.Add(a, b).Kind").AsString().Should().Be("CInteger");
        engine.Evaluate("math.Add(a, b).Value").Should().Be(3);
    }

    [Test]
    public void AConvertibleButWrongEarlierOverloadNoLongerWinsSilently()
    {
        // The half that never threw: a double element converts to string through Convert.ChangeType, so the
        // first-declared params string[] answered the concatenation "12" for Join(1, 2). The element type is
        // an exact match for int[] and a forced conversion for string[], so int[] is now the better score.
        NewEngine().Evaluate("stringFirst.Join(1, 2)").AsString().Should().Be("3");
    }

    [Test]
    public void DeclarationOrderDoesNotDecide()
    {
        var engine = NewEngine();

        engine.Evaluate("stringFirst.Join(1, 2)").AsString()
            .Should().Be(engine.Evaluate("intFirst.Join(1, 2)").AsString());
    }

    [Test]
    public void AnArgumentNoOverloadAcceptsIsACatchableResolutionFailure()
    {
        // Nothing converts a string to any of the three element types, so every candidate declines and the
        // call ends in resolution rather than in whatever the first candidate's conversion threw.
        var engine = NewEngine();

        Invoking(() => engine.Evaluate("math.Add('text')"))
            .Should().Throw<JavaScriptException>()
            .WithMessage("No public methods with the specified arguments were found.");

        engine.Evaluate("try { math.Add('text') } catch (e) { e instanceof TypeError }")
            .AsBoolean().Should().BeTrue();
    }

    [Test]
    public void AGenericCollectionParameterTakesTheSamePath()
    {
        // List<T> and the other single-argument generic collection types are scored by the same branch, and
        // read their element type off the generic argument rather than off Type.GetElementType().
        NewEngine().Evaluate("collections.Sum([a, b])").AsString().Should().Be("CInteger:3");
    }

    [Test]
    public void AnExplicitJavaScriptArrayArgumentTakesTheSamePath()
    {
        // A single array argument is passed through to the params parameter unwrapped, so it reaches scoring
        // as the very same JsArray the bundling would have built.
        var engine = NewEngine();

        engine.Evaluate("math.Add([a, b]).Kind").AsString().Should().Be("CInteger");
        engine.Evaluate("math.Add([a, b]).Value").Should().Be(3);
    }

    [Test]
    public void AnEmptyArrayKeepsTodaysAnswer()
    {
        // Deliberately unchanged: with no element to read, the candidates are genuinely indistinguishable and
        // the base score is all there is. Pinned so that a future element rule cannot quietly change it.
        NewEngine().Evaluate("math.Add().Kind").AsString().Should().Be("CDecimal");
    }

    // ---- carve-out guards ----------------------------------------------------------------------------

    [Test]
    public void AnObjectElementTypeKeepsItsScoreBesideAScalarObjectOverload()
    {
        // params object[] is what every host writes for "anything"; scoring its elements would rate each of
        // them the catch-all 5 and hand the call to the scalar object overload instead.
        NewEngine().Evaluate("catchAll.Take(1)").AsString().Should().Be("params:1");
    }

    [Test]
    public void AJsValueElementTypeKeepsItsScoreBesideAConvertibleScalarOverload()
    {
        // params JsValue[] is the other "anything" signature. A JsString is an is-a match for JsValue rather
        // than an exact one, so scoring the elements would add 1 and tie this with the char overload beside
        // it - and a tie is decided by the candidate order, which puts params last.
        NewEngine().Evaluate("jsValues.Take('x')").AsString().Should().Be("params:1");
    }

    // ---- constructor lane ----------------------------------------------------------------------------

    [Test]
    public void AConstructorParamsOverloadIsChosenByTheElementTypeToo()
    {
        // Constructor resolution has no retry - TypeReference calls the first match and stops - so the wrong
        // selection was not merely a preference here, it was the whole answer.
        NewEngine().Evaluate("new Bag(a, b).Kind").AsString().Should().Be("CInteger:2");
    }

    [Test]
    public void AConstructorArgumentNoOverloadAcceptsIsACatchableResolutionFailure()
    {
        var engine = NewEngine();

        Invoking(() => engine.Evaluate("new Bag('text')"))
            .Should().Throw<JavaScriptException>()
            .WithMessage("Could not resolve a constructor for the specified arguments.");

        engine.Evaluate("try { new Bag('text') } catch (e) { e instanceof TypeError }")
            .AsBoolean().Should().BeTrue();
    }
}
