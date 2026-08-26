namespace Jint.Tests.Runtime;

/// <summary>
/// <c>String.prototype.concat</c> asks its receiver for a growable buffer before appending. A
/// receiver that is itself the result of concatenation used to hand back <em>itself</em>, which
/// made the append mutate a value the script can still see, and threw when the receiver had not
/// built a buffer yet. Strings are immutable, so the receiver must never be affected.
/// </summary>
public class StringConcatReceiverTests
{
    // A compound assignment through a member reference stores the growable value back into the
    // object/array rather than a flattened copy, so the receiver below really is that value.
    private const string OneAppend = "var o = { p: 'a' }; o.p += 'b';";
    private const string TwoAppends = "var o = { p: 'a' }; o.p += 'b'; o.p += 'c';";

    private static Engine CreateEngine() => new();

    [Test]
    public void ConcatOnASingleAppendReceiver()
    {
        // no buffer had been created yet, so asking to grow one used to dereference null
        var engine = CreateEngine();

        engine.Evaluate(OneAppend + " o.p.concat('c')").AsString().Should().Be("abc");
    }

    [Test]
    public void ConcatOnASingleAppendReceiverLeavesItUnchanged()
    {
        var engine = CreateEngine();

        engine.Evaluate(OneAppend + " o.p.concat('c'); o.p").AsString().Should().Be("ab");
    }

    [Test]
    public void ConcatOnAMultiAppendReceiver()
    {
        var engine = CreateEngine();

        engine.Evaluate(TwoAppends + " o.p.concat('d')").AsString().Should().Be("abcd");
    }

    [Test]
    public void ConcatDoesNotMutateAMultiAppendReceiver()
    {
        var engine = CreateEngine();

        engine.Evaluate(TwoAppends + " o.p.concat('d'); o.p").AsString().Should().Be("abc");
    }

    [Test]
    public void RepeatedConcatOffTheSameReceiverIsIndependent()
    {
        var engine = CreateEngine();

        engine.Evaluate(TwoAppends + " o.p.concat('1'); o.p.concat('2')").AsString().Should().Be("abc2");
        engine.Evaluate(TwoAppends + " var a = o.p.concat('1'); var b = o.p.concat('2'); a + '|' + b + '|' + o.p")
            .AsString().Should().Be("abc1|abc2|abc");
    }

    [Test]
    public void ConcatWithSeveralArguments()
    {
        var engine = CreateEngine();

        engine.Evaluate(TwoAppends + " o.p.concat('d', 'e', 'f')").AsString().Should().Be("abcdef");
        engine.Evaluate(TwoAppends + " o.p.concat('d', 'e', 'f'); o.p").AsString().Should().Be("abc");
        engine.Evaluate(OneAppend + " o.p.concat('c', 'd')").AsString().Should().Be("abcd");
    }

    [Test]
    public void ConcatWithNoArguments()
    {
        var engine = CreateEngine();

        engine.Evaluate(OneAppend + " o.p.concat()").AsString().Should().Be("ab");
        engine.Evaluate(TwoAppends + " o.p.concat()").AsString().Should().Be("abc");
        engine.Evaluate(TwoAppends + " o.p.concat(); o.p").AsString().Should().Be("abc");
    }

    [Test]
    public void ConcatOnAnAppendChainReceiver()
    {
        var engine = CreateEngine();

        engine.Evaluate("var a = ['x']; for (var i = 0; i < 5; i++) { a[0] += i; } a[0].concat('!')")
            .AsString().Should().Be("x01234!");
        engine.Evaluate("var a = ['x']; for (var i = 0; i < 5; i++) { a[0] += i; } a[0].concat('!'); a[0]")
            .AsString().Should().Be("x01234");
    }

    [Test]
    public void ConcatOnAnArrayElementReceiver()
    {
        var engine = CreateEngine();

        engine.Evaluate("var a = ['a']; a[0] += 'b'; a[0] += 'c'; a[0].concat('!')").AsString().Should().Be("abc!");
        engine.Evaluate("var a = ['a']; a[0] += 'b'; a[0] += 'c'; a[0].concat('!'); a[0]").AsString().Should().Be("abc");
    }

    [Test]
    public void ConcatChainedOnItsOwnResult()
    {
        var engine = CreateEngine();

        engine.Evaluate(TwoAppends + " o.p.concat('d').concat('e')").AsString().Should().Be("abcde");
        engine.Evaluate(TwoAppends + " o.p.concat('d').concat('e'); o.p").AsString().Should().Be("abc");
    }

    [Test]
    public void ConcatOnAFlatReceiverIsUnaffected()
    {
        // control: a plain literal receiver never had either problem
        var engine = CreateEngine();

        engine.Evaluate("'ab'.concat('c')").AsString().Should().Be("abc");
        engine.Evaluate("'ab'.concat('c', 'd')").AsString().Should().Be("abcd");
        engine.Evaluate("'ab'.concat()").AsString().Should().Be("ab");
        engine.Evaluate("var s = 'ab'; s.concat('c'); s").AsString().Should().Be("ab");
    }

    [Test]
    public void ConcatOnANonStringReceiverIsUnaffected()
    {
        // control: the coercion branch builds its own value and never touches the receiver
        var engine = CreateEngine();

        engine.Evaluate("String.prototype.concat.call(12, '3')").AsString().Should().Be("123");
        engine.Evaluate("String.prototype.concat.call(true, '!')").AsString().Should().Be("true!");
    }
}
