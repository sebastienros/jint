using Jint.Native;

namespace Jint.Tests.Runtime;

public class ArgumentsCacheBehaviorTests
{
    [Fact]
    public void MappedArgumentWriteSurvivesFunctionReturn()
    {
        // Regression: a mapped arguments[i] = v write updates only the parameter binding while the
        // call runs; the escaped snapshot taken when arguments is materialized must capture that
        // binding value, or the write is silently lost once the call returns.
        var engine = new Engine();
        Assert.Equal(99, engine.Evaluate("(function(){ function f(a){ arguments[0]=99; return arguments; } return f(1)[0]; })()").AsNumber());
        Assert.Equal("{\"0\":99}", engine.Evaluate("(function(){ function f(a){ arguments[0]=99; return arguments; } return JSON.stringify(f(1)); })()").AsString());
    }

    [Fact]
    public void MappedParameterWriteSurvivesFunctionReturn()
    {
        // The mapping is bidirectional: writing the parameter must also be visible through the
        // escaped arguments object after the call returns.
        var engine = new Engine();
        Assert.Equal(42, engine.Evaluate("(function(){ function f(a){ a=42; return arguments; } return f(1)[0]; })()").AsNumber());
        Assert.Equal("1,88", engine.Evaluate("(function(){ function f(a,b){ b=88; return arguments; } var r=f(1,2); return r[0]+','+r[1]; })()").AsString());
    }

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
