using Jint.Native;

namespace Jint.Tests.Runtime;

public class ArgumentsCacheBehaviorTests
{
    [Fact]
    public void ArgsForGeneratorsAreNotReusedFromCache()
    {
        // Arrange
        List<JsValue> logValues = new();
        var engine = new Engine();
        engine.SetValue("log", logValues.Add);

        // Act
        engine.Evaluate(
            """
            function *method() {
              log(arguments[0]);
              log(arguments[1]);
            }
              
            function *other() {
              log(arguments[0]);
              log(arguments[1]);
            }

            var generator1 = method(42, undefined);
            var generator2 = other(10, undefined);
            generator1.next();
            generator2.next();
            """);

        // Assert
        Assert.Equal([
            JsNumber.Create(42),
            JsValue.Undefined,
            JsNumber.Create(10),
            JsValue.Undefined,
        ], logValues);
    }

    [Fact]
    public void NamedArgsForGeneratorsAreNotReusedFromCache()
    {
        // Arrange
        List<JsValue> logValues = new();
        var engine = new Engine();
        engine.SetValue("log", logValues.Add);

        // Act
        engine.Evaluate(
            """
            function *method(a, b) {
              log(a);
              log(b);
            }
              
            function *other(a, b) {
              log(a);
              log(b);
            }

            var generator1 = method(42, undefined);
            var generator2 = other(10, undefined);
            generator1.next();
            generator2.next();
            """);

        // Assert
        Assert.Equal([
            JsNumber.Create(42),
            JsValue.Undefined,
            JsNumber.Create(10),
            JsValue.Undefined,
        ], logValues);
    }

    [Fact]
    public void ArgsForGeneratorsWithBindAreNotReusedFromCache()
    {
        // Arrange
        List<JsValue> logValues = new();
        var engine = new Engine();
        engine.SetValue("log", logValues.Add);

        // Act
        engine.Evaluate(
            """
            function *method() {
              log(arguments[0]);
              log(arguments[1]);
            }
              
            function *other() {
              log(arguments[0]);
              log(arguments[1]);
            }

            var methodWithBind = method.bind({});
            var otherWithBind = other.bind({});

            var generator1 = methodWithBind(42, undefined);
            var generator2 = otherWithBind(10, undefined);
            generator1.next();
            generator2.next();
            """);

        // Assert
        Assert.Equal([
            JsNumber.Create(42),
            JsValue.Undefined,
            JsNumber.Create(10),
            JsValue.Undefined,
        ], logValues);
    }

    [Fact]
    public void ArgsForAsyncFunctionsAreNotReused()
    {
        // Arrange
        List<JsValue> logValues = new();
        var engine = new Engine(new Options(){ ExperimentalFeatures = ExperimentalFeature.All });
        engine.SetValue("log", logValues.Add);

        // Act
        engine.Execute(
            """
            async function method() {
              log(arguments[0]);
              log(arguments[1]);
            }
              
            async function other() {
              log(arguments[0]);
              log(arguments[1]);
            }

            method(42, undefined);
            other(10, undefined);
            """);
        engine.RunAvailableContinuations();

        // Assert
        Assert.Equal([
            JsNumber.Create(42),
            JsValue.Undefined,
            JsNumber.Create(10),
            JsValue.Undefined,
        ], logValues);
    }
}
