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
    public void ParameterWriteAfterMaterializationSurvivesReturn()
    {
        // A parameter (or arguments[i]) write made AFTER the arguments object is first referenced —
        // whether it was materialized virtually (a prior arguments read) or to real descriptors
        // (Object.keys) — must still show through the escaped object once the call returns.
        var engine = new Engine();
        Assert.Equal(9, engine.Evaluate("(function(){ function f(a){ var s=arguments[0]; a=9; return arguments; } return f(1)[0]; })()").AsNumber());
        Assert.Equal(9, engine.Evaluate("(function(){ function f(a){ Object.keys(arguments); a=9; return arguments; } return f(1)[0]; })()").AsNumber());
        Assert.Equal(9, engine.Evaluate("(function(){ function f(a){ arguments[0]=9; Object.keys(arguments); return arguments; } return f(1)[0]; })()").AsNumber());
    }

    [Fact]
    public void FreezingAMappedIndexPreservesUserSetAttributes()
    {
        // A still-mapped index can be made non-enumerable (or non-configurable) without unmapping.
        // Freezing its final binding value at return must update only the value, not reset the
        // attribute the script set.
        var engine = new Engine();
        Assert.Equal("9|false|", engine.Evaluate(
            "(function(){ function f(a){ Object.defineProperty(arguments,'0',{enumerable:false}); a=9; return arguments; } var r=f(1); return r[0]+'|'+Object.getOwnPropertyDescriptor(r,'0').enumerable+'|'+Object.keys(r).join(','); })()").AsString());
        Assert.Equal("{}", engine.Evaluate(
            "(function(){ function f(a){ Object.defineProperty(arguments,'0',{enumerable:false}); a=9; return arguments; } return JSON.stringify(f(1)); })()").AsString());
    }

    [Fact]
    public void DeletedMappedIndexStaysDeletedAfterParameterWrite()
    {
        // A deleted mapped index must not be resurrected by the detach-time freeze: it is no longer
        // mapped, so a later parameter write does not bring it back.
        var engine = new Engine();
        Assert.Equal("false:undefined", engine.Evaluate(
            "(function(){ function f(a){ var s=arguments; delete arguments[0]; a=9; return arguments.hasOwnProperty('0')+':'+arguments[0]; } return f(1); })()").AsString());
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
