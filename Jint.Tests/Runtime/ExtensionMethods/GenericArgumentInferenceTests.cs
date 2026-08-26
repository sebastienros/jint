using Jint.Runtime;

namespace Jint.Tests.Runtime.ExtensionMethods;

/// <summary>
/// Type arguments a generic method's <em>constructed</em> parameter implies (<c>this IEnumerable&lt;T&gt;</c>)
/// are pinned: that argument is handed to the method unconverted, so a bare <c>T item</c> parameter - whose
/// argument still goes through the type converter - must not re-infer the position and produce an
/// instantiation the receiver cannot satisfy (https://github.com/sebastienros/jint/issues/2987).
/// </summary>
public class GenericArgumentInferenceTests
{
    private static Engine CreateEngine()
    {
        return new Engine(options => options.AddExtensionMethods(typeof(InferenceExtensions)));
    }

    /// <summary>
    /// The receivers below must be sequences that are only <see cref="IEnumerable{T}"/> - no Count, no
    /// indexer - or they become array-like wrappers whose Array.prototype natives win over the registered
    /// extension methods (#2976) and the test silently stops exercising generic inference at all.
    /// </summary>
    private static Engine CreateEngineWithLazyReceiver(object receiver)
    {
        var engine = CreateEngine();
        engine.SetValue("coll", receiver);
        engine.Evaluate("typeof coll.map").AsString().Should().Be("function", "the receiver must not be array-like");
        return engine;
    }

    [Test]
    public void ChainedGenericExtensionsOverLazyEnumerable()
    {
        // the issue's own repro: a LINQ iterator handed to the engine, projected, then searched
        var engine = CreateEngineWithLazyReceiver(new List<string> { "Hello", "World" }.Select(y => y));

        engine.Evaluate("coll.map(x => x).includes('Hello')").AsBoolean().Should().BeTrue();
        engine.Evaluate("coll.map(x => x).includes('Nope')").AsBoolean().Should().BeFalse();
        engine.Evaluate("coll.map(x => x + '!').includes('World!')").AsBoolean().Should().BeTrue();
    }

    [Test]
    public void ReceiverPinsTypeArgumentOverMoreSpecificArgument()
    {
        var engine = CreateEngineWithLazyReceiver(new List<object> { "Hello", "World" }.Where(_ => true));

        // the receiver's element type wins; the 'Hello' argument widens to it instead of re-pinning T
        engine.Evaluate("coll.inferred('Hello')").AsString().Should().Be("Object");
        engine.Evaluate("coll.includes('Hello')").AsBoolean().Should().BeTrue();
        engine.Evaluate("coll.includes('Nope')").AsBoolean().Should().BeFalse();
    }

    [Test]
    public void NumericArgumentDoesNotRepinElementType()
    {
        // no LINQ projection and no 'object' element type - a JsNumber's CLR shape is a double, so the
        // argument alone used to infer includes<double> and then failed to bind its own IEnumerable<int>
        var engine = CreateEngineWithLazyReceiver(new List<int> { 1, 2, 3 }.Where(x => x > 0));

        engine.Evaluate("coll.inferred(2)").AsString().Should().Be("Int32");
        engine.Evaluate("coll.includes(2)").AsBoolean().Should().BeTrue();
        engine.Evaluate("coll.includes(9)").AsBoolean().Should().BeFalse();
    }

    [Test]
    public void BareGenericParameterStillInfersWhenNothingPinsIt()
    {
        var engine = CreateEngineWithLazyReceiver(new List<int> { 1, 2, 3 }.Where(x => x > 0));

        // TExtra appears only as a bare parameter, so it is still inferred from its argument
        engine.Evaluate("coll.inferredBoth('x')").AsString().Should().Be("Int32/String");
    }

    [Test]
    public void ElidedOptionalArgumentInfersNothing()
    {
        var engine = CreateEngineWithLazyReceiver(new List<string> { "Hello" }.Where(_ => true));

        // the missing argument used to be stood in for by typeof(object), whose GetType() is RuntimeType
        engine.Evaluate("coll.inferredOptional()").AsString().Should().Be("String");
    }

    [Test]
    public void ConstraintViolationIsACatchableTypeError()
    {
        var engine = CreateEngineWithLazyReceiver(new List<string> { "Hello" }.Where(_ => true));

        // T pins to string, which violates 'where T : struct' - MakeGenericMethod throws and the candidate
        // has to be declined rather than letting a CLR ArgumentException escape Evaluate
        var exception = Assert.Throws<JavaScriptException>(() => engine.Evaluate("coll.needsStruct('x')"));
        exception.Message.Should().Be("No public methods with the specified arguments were found.");

        engine.Evaluate("(() => { try { coll.needsStruct('x'); } catch (e) { return e.constructor.name; } })()")
            .AsString().Should().Be("TypeError");
    }

    [Test]
    public void MismatchedElementTypeIsACatchableTypeError()
    {
        var engine = new Engine();
        engine.SetValue("host", new ElementTypeHost());
        engine.SetValue("items", new List<object> { "Hello" });

        // a List<object> is not an IEnumerable<string>; the conversion has to decline rather than hand the
        // value over unconverted and die inside the reflection invoke
        Assert.Throws<JavaScriptException>(() => engine.Evaluate("host.Take(items)"));

        engine.Evaluate("(() => { try { host.Take(items); } catch (e) { return e.constructor.name; } })()")
            .AsString().Should().Be("TypeError");

        // the compatible call still binds
        engine.SetValue("strings", new List<string> { "Hello", "World" });
        engine.Evaluate("host.Take(strings)").AsNumber().Should().Be(2);
    }

    [Test]
    public void GenericExtensionOverAnArrayLikeReceiverIsUnaffected()
    {
        // a List<string> receiver reaches Array.prototype.includes (#2976), and the extension stays
        // reachable through its exact C# casing - inference for it must still pin from the receiver
        var engine = CreateEngine();
        engine.SetValue("coll", new List<string> { "Hello", "World" });

        engine.Evaluate("coll.includes('Hello')").AsBoolean().Should().BeTrue();
        engine.Evaluate("coll.Inferred('Hello')").AsString().Should().Be("String");
    }
}

public class ElementTypeHost
{
    public int Take(IEnumerable<string> items) => items.Count();
}

public static class InferenceExtensions
{
    // the exact registration from https://github.com/sebastienros/jint/issues/2987
    public static IEnumerable<TResult> map<T, TResult>(this IEnumerable<T> enumerable, Func<T, TResult> selector)
    {
        return enumerable.Select(selector);
    }

    public static bool includes<T>(this IEnumerable<T> enumerable, T item)
    {
        return enumerable.Contains(item);
    }

    // white-box probes: they report the inferred type argument itself rather than a behavior implying it
    public static string inferred<T>(this IEnumerable<T> enumerable, T probe) => typeof(T).Name;

    public static string Inferred<T>(this IEnumerable<T> enumerable, T probe) => typeof(T).Name;

    public static string inferredBoth<T, TExtra>(this IEnumerable<T> enumerable, TExtra extra)
        => typeof(T).Name + "/" + typeof(TExtra).Name;

    public static string inferredOptional<T>(this IEnumerable<T> enumerable, T item = default) => typeof(T).Name;

    public static bool needsStruct<T>(this IEnumerable<T> enumerable, T item) where T : struct => true;
}
