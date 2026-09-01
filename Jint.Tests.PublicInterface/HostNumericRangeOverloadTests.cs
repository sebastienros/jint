#nullable enable

using Jint.Runtime;

namespace Jint.Tests.PublicInterface;

/// <summary>
/// Overload scoring rates a JavaScript number against a numeric parameter by magnitude, so a number outside
/// the parameter's range must not be proposed as a fit for it.
/// </summary>
/// <remarks>
/// <para>
/// <c>float</c>, <c>short</c>, <c>ushort</c>, <c>byte</c> and <c>sbyte</c> always gated on the value fitting;
/// <c>int</c> and <c>long</c> did not, so <c>3000000000</c> was a perfect match for an <c>int</c> parameter
/// and <c>1e300</c> a good one for a <c>long</c>. A perfect score ends the search, so on the operator lane the
/// wider overload beside it was never scored and the failed conversion reached the host as a CLR
/// <see cref="OverflowException"/>; on the method lane the candidate list came back holding only the
/// candidate that cannot bind.
/// </para>
/// <para>
/// Every operator case gets an operand type of its own on purpose. The operator lane remembers the
/// <em>selected</em> method per (operator, left type, right type), and every JavaScript number reaches that
/// key as <see cref="double"/>, so two values sharing one operand type would share one entry
/// (<see href="https://github.com/sebastienros/jint/issues/3567">#3567</see>) and this file would be
/// measuring that instead.
/// </para>
/// </remarks>
public class HostNumericRangeOverloadTests
{
    public sealed class IntOrObjectInRange
    {
        public static string operator +(IntOrObjectInRange left, int right) => "int:" + right;
        public static string operator +(IntOrObjectInRange left, object right) => "object";
    }

    public sealed class IntOrObjectOutOfRange
    {
        public static string operator +(IntOrObjectOutOfRange left, int right) => "int:" + right;
        public static string operator +(IntOrObjectOutOfRange left, object right) => "object";
    }

    public sealed class LongOrObjectInRange
    {
        public static string operator +(LongOrObjectInRange left, long right) => "long:" + right;
        public static string operator +(LongOrObjectInRange left, object right) => "object";
    }

    public sealed class LongOrObjectOutOfRange
    {
        public static string operator +(LongOrObjectOutOfRange left, long right) => "long:" + right;
        public static string operator +(LongOrObjectOutOfRange left, object right) => "object";
    }

    public sealed class IntOnly
    {
        public static string operator +(IntOnly left, int right) => "int:" + right;
    }

    public sealed class ByteOnly
    {
        public static string operator +(ByteOnly left, byte right) => "byte:" + right;
    }

    public sealed class Host
    {
        public string Take(int value) => "int:" + value;
        public string Take(object value) => "object";

        public string TakeLong(long value) => "long:" + value;
        public string TakeLong(object value) => "object";

        public string TakeShort(short value) => "short:" + value;
        public string TakeShort(object value) => "object";

        public string TakeByte(byte value) => "byte:" + value;
        public string TakeByte(object value) => "object";

        public string TakeFloat(float value) => "float:" + value;
        public string TakeFloat(object value) => "object";

        public string Widen(int value) => "int:" + value;
        public string Widen(long value) => "long:" + value;

        public string Sole(int value) => "int:" + value;
    }

    public sealed class Boxed
    {
        public Boxed(int value) => Description = "int:" + value;
        public Boxed(object value) => Description = "object";

        public string Description { get; }
    }

    public sealed class IntOnlyBoxed
    {
        public IntOnlyBoxed(int value) => Description = "int:" + value;

        public string Description { get; }
    }

    public sealed class LongBoxed
    {
        public LongBoxed(long value) => Description = "long:" + value;
        public LongBoxed(object value) => Description = "object";

        public string Description { get; }
    }

    private static Engine OperatorEngine(object operand)
    {
        var engine = new Engine(options => options.Interop.AllowOperatorOverloading = true);
        engine.SetValue("m", operand);
        return engine;
    }

    private static Engine MethodEngine()
    {
        var engine = new Engine();
        engine.SetValue("h", new Host());
        engine.SetValue("Boxed", typeof(Boxed));
        engine.SetValue("LongBoxed", typeof(LongBoxed));
        engine.SetValue("IntOnlyBoxed", typeof(IntOnlyBoxed));
        return engine;
    }

    // ---- operator lane -------------------------------------------------------------------------------

    [Test]
    public void AnInRangeIntegerStillSelectsTheIntOperator()
    {
        OperatorEngine(new IntOrObjectInRange()).Evaluate("m + 5").AsString().Should().Be("int:5");
    }

    [Test]
    public void AnIntegerBeyondIntSelectsTheObjectOperator()
    {
        OperatorEngine(new IntOrObjectOutOfRange()).Evaluate("m + 3000000000").AsString().Should().Be("object");
    }

    [Test]
    public void AnInRangeIntegerStillSelectsTheLongOperator()
    {
        OperatorEngine(new LongOrObjectInRange()).Evaluate("m + 5").AsString().Should().Be("long:5");
    }

    [Test]
    public void AnIntegerBeyondLongSelectsTheObjectOperator()
    {
        OperatorEngine(new LongOrObjectOutOfRange()).Evaluate("m + 1e300").AsString().Should().Be("object");
    }

    [Test]
    public void AnOutOfRangeIntegerLeavesALoneIntOperatorUnselectedTheWayALoneByteOperatorAlreadyIs()
    {
        // The byte column is the control: an out-of-range value has never selected a lone byte operator, and
        // the pair falls back to ordinary JavaScript semantics rather than throwing out of Evaluate. int now
        // answers the same way instead of being selected and overflowing.
        var byteAnswer = OperatorEngine(new ByteOnly()).Evaluate("typeof (m + 300)").AsString();
        var intAnswer = OperatorEngine(new IntOnly()).Evaluate("typeof (m + 3000000000)").AsString();

        intAnswer.Should().Be(byteAnswer);
    }

    // ---- method lane ---------------------------------------------------------------------------------

    [Test]
    public void AnInRangeIntegerStillBindsToTheIntOverload()
    {
        MethodEngine().Evaluate("h.Take(5)").AsString().Should().Be("int:5");
    }

    [Test]
    public void AnIntegerBeyondIntBindsToTheObjectOverload()
    {
        MethodEngine().Evaluate("h.Take(3000000000)").AsString().Should().Be("object");
    }

    [Test]
    public void AnIntegerBelowIntBindsToTheObjectOverload()
    {
        MethodEngine().Evaluate("h.Take(-3000000000)").AsString().Should().Be("object");
    }

    [Test]
    public void AnInRangeIntegerStillBindsToTheLongOverload()
    {
        MethodEngine().Evaluate("h.TakeLong(5)").AsString().Should().Be("long:5");
    }

    [Test]
    public void AnIntegerBeyondLongStillBindsToTheObjectOverload()
    {
        // This one already answered correctly: long scored 2 rather than 0, so the object candidate stayed in
        // the list and MethodInfoFunction's retry reached it after the long conversion failed. It is the
        // answer that must not change now that the long candidate is refused up front instead.
        MethodEngine().Evaluate("h.TakeLong(1e300)").AsString().Should().Be("object");
    }

    [Test]
    public void AnIntegerBeyondIntBindsToTheLongOverloadWhenThatIsTheWiderCandidate()
    {
        // The point of the guard, stated without an object overload in the way: the value still has a home,
        // and it is the narrowest parameter that can actually hold it.
        var engine = MethodEngine();

        engine.Evaluate("h.Widen(5)").AsString().Should().Be("int:5");
        engine.Evaluate("h.Widen(3000000000)").AsString().Should().Be("long:3000000000");
    }

    [Test]
    public void ALoneIntMethodIsUnaffectedBecauseASingleCandidateNeverReachesScoring()
    {
        // MethodInfoFunction binds a single candidate directly and never scores it, so the guard cannot reach
        // this shape - it answered with a resolution failure before and answers with one now. Pinned because
        // it is the half of "the only candidate" that does not change.
        var engine = MethodEngine();

        engine.Evaluate("h.Sole(5)").AsString().Should().Be("int:5");

        Invoking(() => engine.Evaluate("h.Sole(3000000000)"))
            .Should().Throw<JavaScriptException>()
            .WithMessage("No public methods with the specified arguments were found.");
    }

    [Test]
    public void TheNarrowerIntegralParametersKeepAnsweringAsTheyDid()
    {
        var engine = MethodEngine();

        engine.Evaluate("h.TakeShort(5)").AsString().Should().Be("short:5");
        engine.Evaluate("h.TakeShort(3000000000)").AsString().Should().Be("object");
        engine.Evaluate("h.TakeByte(5)").AsString().Should().Be("byte:5");
        engine.Evaluate("h.TakeByte(300)").AsString().Should().Be("object");
        engine.Evaluate("h.TakeFloat(5)").AsString().Should().Be("float:5");

        // float is the one that keeps a candidate out of range, and deliberately so: its range rule declines,
        // but Convert.ToSingle saturates to infinity rather than throwing, so the conversion rule below it
        // accepts. Only the tag is asserted - how a host renders float.PositiveInfinity is its own business.
        engine.Evaluate("h.TakeFloat(1e300)").AsString().Should().StartWith("float:");
    }

    [Test]
    public void ANonIntegralNumberStillReachesTheIntOverload()
    {
        // 1.5 was never integral, so it never took the perfect-score lane and still converts the way
        // Convert.ChangeType does. Pinned because the range guard sits on that same lane.
        MethodEngine().Evaluate("h.Take(1.5)").AsString().Should().Be("int:2");
    }

    [Test]
    public void TheIntBoundariesThemselvesStillBindToTheIntOverload()
    {
        var engine = MethodEngine();

        engine.Evaluate("h.Take(2147483647)").AsString().Should().Be("int:2147483647");
        engine.Evaluate("h.Take(-2147483648)").AsString().Should().Be("int:-2147483648");
        engine.Evaluate("h.Take(2147483648)").AsString().Should().Be("object");
        engine.Evaluate("h.Take(-2147483649)").AsString().Should().Be("object");
    }

    [Test]
    public void TheLongUpperBoundIsExclusiveBecauseTheDoubleRoundsUp()
    {
        // (double) long.MaxValue rounds up to 2^63, which does not fit a long - the same exclusive bound the
        // fast numeric conversion uses, so the score and the conversion agree about what a long can hold.
        var engine = MethodEngine();

        engine.Evaluate("h.TakeLong(9223372036854775807)").AsString().Should().Be("object");
        engine.Evaluate("h.TakeLong(-9223372036854775808)").AsString().Should().Be("long:-9223372036854775808");
    }

    // ---- constructor lane ----------------------------------------------------------------------------

    [Test]
    public void AnInRangeIntegerStillSelectsTheIntConstructor()
    {
        MethodEngine().Evaluate("new Boxed(5).Description").AsString().Should().Be("int:5");
    }

    [Test]
    public void AnIntegerBeyondIntSelectsTheObjectConstructor()
    {
        // Constructor resolution has no retry - TypeReference calls the first match and stops - so a
        // perfect-scored int candidate meant the conversion's failure escaped Evaluate.
        MethodEngine().Evaluate("new Boxed(3000000000).Description").AsString().Should().Be("object");
    }

    [Test]
    public void ALoneIntConstructorRefusesRatherThanOverflowing()
    {
        // The other half: constructor selection does score its single candidate, so an out-of-range number
        // now leaves through resolution as a catchable TypeError rather than as a CLR OverflowException.
        var engine = MethodEngine();

        engine.Evaluate("new IntOnlyBoxed(5).Description").AsString().Should().Be("int:5");

        Invoking(() => engine.Evaluate("new IntOnlyBoxed(3000000000)"))
            .Should().Throw<JavaScriptException>()
            .WithMessage("Could not resolve a constructor for the specified arguments.");
    }

    [Test]
    public void AnIntegerBeyondLongSelectsTheObjectConstructor()
    {
        MethodEngine().Evaluate("new LongBoxed(1e300).Description").AsString().Should().Be("object");
    }
}
