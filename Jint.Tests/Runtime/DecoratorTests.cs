namespace Jint.Tests.Runtime;

public class DecoratorTests
{
    private readonly Engine _engine;

    public DecoratorTests()
    {
        _engine = new Engine();
    }

    [Fact]
    public void MethodDecoratorCanWrapMethod()
    {
        var result = _engine.Evaluate("""
            function double(target, context) {
                return function(...args) {
                    return target.call(this, ...args) * 2;
                };
            }

            class C {
                @double
                foo() { return 21; }
            }

            new C().foo();
            """);

        Assert.Equal(42, result.AsInteger());
    }

    [Fact]
    public void ClassDecoratorCanAddProperty()
    {
        var result = _engine.Evaluate("""
            function addProp(cls, context) {
                cls.prototype.added = true;
                return cls;
            }

            @addProp
            class C {}

            new C().added;
            """);

        Assert.True(result.AsBoolean());
    }

    [Fact]
    public void FieldDecoratorCanTransformInitialValue()
    {
        var result = _engine.Evaluate("""
            function multiplyBy10(value, context) {
                return function(initialValue) {
                    return initialValue * 10;
                };
            }

            class C {
                @multiplyBy10
                x = 5;
            }

            new C().x;
            """);

        Assert.Equal(50, result.AsInteger());
    }

    [Fact]
    public void AutoAccessorDecoratorCanAddLogging()
    {
        var result = _engine.Evaluate("""
            var logs = [];
            function logged(value, context) {
                return {
                    get() {
                        logs.push("get " + context.name);
                        return value.get.call(this);
                    },
                    set(v) {
                        logs.push("set " + context.name);
                        return value.set.call(this, v);
                    },
                    init(v) {
                        logs.push("init " + context.name);
                        return v;
                    }
                };
            }

            class C {
                @logged
                accessor x = 1;
            }

            var c = new C();
            var val = c.x;
            c.x = 2;
            JSON.stringify(logs);
            """);

        Assert.Equal("[\"init x\",\"get x\",\"set x\"]", result.AsString());
    }

    [Fact]
    public void AddInitializerCallbackRunsOnInstantiation()
    {
        var result = _engine.Evaluate("""
            function withInit(target, context) {
                context.addInitializer(function() {
                    this.fromInit = true;
                });
            }

            class C {
                @withInit
                foo() {}
            }

            new C().fromInit;
            """);

        Assert.True(result.AsBoolean());
    }

    [Fact]
    public void MultipleDecoratorsAppliedInCorrectOrder()
    {
        var result = _engine.Evaluate("""
            var evalOrder = [];
            function makeD(name) {
                evalOrder.push("eval_" + name);
                return function(target, context) {
                    evalOrder.push("apply_" + name);
                    return function(...args) {
                        return target.call(this, ...args) + "_" + name;
                    };
                };
            }

            class C {
                @makeD("first")
                @makeD("second")
                foo() { return "base"; }
            }

            JSON.stringify({
                result: new C().foo(),
                order: evalOrder
            });
            """);

        var json = result.AsString();
        // Evaluation order: top-to-bottom (first, second)
        // Application order: bottom-to-top (second applied first, then first)
        // Result: first(second(base)) = first("base_second") = "base_second_first"
        Assert.Contains("\"result\":\"base_second_first\"", json);
        Assert.Contains("\"order\":[\"eval_first\",\"eval_second\",\"apply_second\",\"apply_first\"]", json);
    }

    [Fact]
    public void DecoratorContextHasCorrectProperties()
    {
        var result = _engine.Evaluate("""
            var ctx;
            function capture(target, context) {
                ctx = context;
            }

            class C {
                @capture
                myMethod() {}
            }

            JSON.stringify({
                kind: ctx.kind,
                name: ctx.name,
                isStatic: ctx.static,
                isPrivate: ctx.private,
                hasAddInitializer: typeof ctx.addInitializer === 'function'
            });
            """);

        var json = result.AsString();
        Assert.Contains("\"kind\":\"method\"", json);
        Assert.Contains("\"name\":\"myMethod\"", json);
        Assert.Contains("\"isStatic\":false", json);
        Assert.Contains("\"isPrivate\":false", json);
        Assert.Contains("\"hasAddInitializer\":true", json);
    }

    [Fact]
    public void StaticMethodDecoratorContextHasStaticTrue()
    {
        var result = _engine.Evaluate("""
            var ctx;
            function capture(target, context) {
                ctx = context;
            }

            class C {
                @capture
                static bar() {}
            }

            ctx.static;
            """);

        Assert.True(result.AsBoolean());
    }

    [Fact]
    public void ClassDecoratorCanReplaceClass()
    {
        var result = _engine.Evaluate("""
            function addExtra(cls, context) {
                return class extends cls {
                    get extra() { return 99; }
                };
            }

            @addExtra
            class C {
                foo() { return 1; }
            }

            var c = new C();
            JSON.stringify({ foo: c.foo(), extra: c.extra });
            """);

        Assert.Contains("\"foo\":1", result.AsString());
        Assert.Contains("\"extra\":99", result.AsString());
    }

    [Fact]
    public void GetterDecoratorCanWrapGetter()
    {
        var result = _engine.Evaluate("""
            function doubleGetter(target, context) {
                return function() {
                    return target.call(this) * 2;
                };
            }

            class C {
                #val = 21;
                @doubleGetter
                get value() { return this.#val; }
            }

            new C().value;
            """);

        Assert.Equal(42, result.AsInteger());
    }

    [Fact]
    public void SetterDecoratorCanWrapSetter()
    {
        var result = _engine.Evaluate("""
            function doubleSetter(target, context) {
                return function(v) {
                    return target.call(this, v * 2);
                };
            }

            class C {
                #val = 0;
                @doubleSetter
                set value(v) { this.#val = v; }
                get value() { return this.#val; }
            }

            var c = new C();
            c.value = 5;
            c.value;
            """);

        Assert.Equal(10, result.AsInteger());
    }

    [Fact]
    public void DecoratorReturningUndefinedKeepsOriginal()
    {
        var result = _engine.Evaluate("""
            function noop(target, context) {
                // returns undefined
            }

            class C {
                @noop
                foo() { return 42; }
            }

            new C().foo();
            """);

        Assert.Equal(42, result.AsInteger());
    }

    [Fact]
    public void PublicAutoAccessorBasic()
    {
        _engine.Execute("""
            class C {
                accessor x = 1;
                accessor y;
            }
            var c = new C();
            """);

        Assert.Equal(1, _engine.Evaluate("c.x").AsInteger());
        Assert.True(_engine.Evaluate("c.y").IsUndefined());

        _engine.Execute("c.x = 42;");
        Assert.Equal(42, _engine.Evaluate("c.x").AsInteger());
    }

    [Fact]
    public void StaticAutoAccessor()
    {
        var result = _engine.Evaluate("""
            class C {
                static accessor x = 2;
            }
            var before = C.x;
            C.x = 99;
            JSON.stringify({ before: before, after: C.x });
            """);

        Assert.Contains("\"before\":2", result.AsString());
        Assert.Contains("\"after\":99", result.AsString());
    }

    [Fact]
    public void PrivateAutoAccessor()
    {
        var result = _engine.Evaluate("""
            class C {
                accessor #x = 5;
                getX() { return this.#x; }
                setX(v) { this.#x = v; }
            }
            var c = new C();
            var before = c.getX();
            c.setX(42);
            JSON.stringify({ before: before, after: c.getX() });
            """);

        Assert.Contains("\"before\":5", result.AsString());
        Assert.Contains("\"after\":42", result.AsString());
    }

    [Fact]
    public void StaticAutoAccessorDerivedClassThrows()
    {
        var result = _engine.Evaluate("""
            class C {
                static accessor x = 1;
            }
            class D extends C {}

            var threw = false;
            try { D.x; } catch(e) { threw = true; }
            threw;
            """);

        Assert.True(result.AsBoolean());
    }

    [Fact]
    public void FieldDecoratorWithMultipleDecorators()
    {
        var result = _engine.Evaluate("""
            function add1(value, context) {
                return function(v) { return v + 1; };
            }
            function times2(value, context) {
                return function(v) { return v * 2; };
            }

            class C {
                @add1
                @times2
                x = 3;
            }

            // times2 applied first (inner): 3 * 2 = 6
            // add1 applied second (outer): 6 + 1 = 7
            new C().x;
            """);

        Assert.Equal(7, result.AsInteger());
    }

    [Fact]
    public void StaticAddInitializerRunsOnClassConstruction()
    {
        var result = _engine.Evaluate("""
            function addStaticInit(target, context) {
                context.addInitializer(function() {
                    this.initialized = true;
                });
            }

            class C {
                @addStaticInit
                static foo() {}
            }

            C.initialized;
            """);

        Assert.True(result.AsBoolean());
    }
}
