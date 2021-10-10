namespace Jint.Tests.Runtime;

public class GeneratorTests
{
    private readonly Engine _engine;

    public GeneratorTests()
    {
        _engine = new Engine(options => options.ExperimentalFeatures = ExperimentalFeature.Generators);
    }

    [Fact]
    public void LoopYield()
    {
        const string Script = """
          const foo = function*() {
            yield 'a';
            yield 'b';
            yield 'c';
          };

          let str = '';
          for (const val of foo()) {
            str += val;
          }
          return str;
      """;

        Assert.Equal("abc", _engine.Evaluate(Script));
    }

    [Fact]
    public void ReturnDuringYield()
    {
        const string Script = """
          const foo = function*() {
            yield 'a';
            return;
            yield 'c';
          };

          let str = '';
          for (const val of foo()) {
            str += val;
          }
          return str;
      """;

        Assert.Equal("a", _engine.Evaluate(Script));
    }

    [Fact]
    public void LoneReturnInYield()
    {
        const string Script = """
          const foo = function*() {
            return;
          };

          let str = '';
          for (const val of foo()) {
            str += val;
          }
          return str;
      """;

        Assert.Equal("", _engine.Evaluate(Script));
    }

    [Fact]
    public void LoneReturnValueInYield()
    {
        const string Script = """
          const foo = function*() {
            return 'a';
          };

          let str = '';
          for (const val of foo()) {
            str += val;
          }
          return str;
      """;

        Assert.Equal("", _engine.Evaluate(Script));
    }

    [Fact]
    public void YieldUndefined()
    {
        const string Script = """
          const foo = function*() {
            yield undefined;
          };

          let str = '';
          for (const val of foo()) {
            str += val;
          }
          return str;
      """;

        Assert.Equal("undefined", _engine.Evaluate(Script));
    }

    [Fact]
    public void ReturnUndefined()
    {
        const string Script = """
          const foo = function*() {
            return undefined;
          };

          let str = '';
          for (const val of foo()) {
            str += val;
          }
          return str;
      """;

        Assert.Equal("", _engine.Evaluate(Script));
    }

    [Fact]
    public void Basic()
    {
        _engine.Execute("function * generator() { yield 5; yield 6; };");
        _engine.Execute("var iterator = generator(); var item = iterator.next();");
        Assert.Equal(5, _engine.Evaluate("item.value"));
        Assert.False(_engine.Evaluate("item.done").AsBoolean());
        _engine.Execute("item = iterator.next();");
        Assert.Equal(6, _engine.Evaluate("item.value"));
        Assert.False(_engine.Evaluate("item.done").AsBoolean());
        _engine.Execute("item = iterator.next();");
        Assert.True(_engine.Evaluate("item.value === void undefined").AsBoolean());
        Assert.True(_engine.Evaluate("item.done").AsBoolean());
    }

    [Fact]
    public void FunctionExpressions()
    {
        _engine.Execute("var generator = function * () { yield 5; yield 6; };");
        _engine.Execute("var iterator = generator(); var item = iterator.next();");
        Assert.Equal(5, _engine.Evaluate("item.value"));
        Assert.False(_engine.Evaluate("item.done").AsBoolean());
        _engine.Execute("item = iterator.next();");
        Assert.Equal(6, _engine.Evaluate("item.value"));
        Assert.False(_engine.Evaluate("item.done").AsBoolean());
        _engine.Execute("item = iterator.next();");
        Assert.True(_engine.Evaluate("item.value === void undefined").AsBoolean());
        Assert.True(_engine.Evaluate("item.done").AsBoolean());
    }

    [Fact]
    public void CorrectThisBinding()
    {
        _engine.Execute("var generator = function * () { yield 5; yield 6; };");
        _engine.Execute("var iterator = { g: generator, x: 5, y: 6 }.g(); var item = iterator.next();");
        Assert.Equal(5, _engine.Evaluate("item.value"));
        Assert.False(_engine.Evaluate("item.done").AsBoolean());
        _engine.Execute("item = iterator.next();");
        Assert.Equal(6, _engine.Evaluate("item.value"));
        Assert.False(_engine.Evaluate("item.done").AsBoolean());
        _engine.Execute("item = iterator.next();");
        Assert.True(_engine.Evaluate("item.value === void undefined").AsBoolean());
        Assert.True(_engine.Evaluate("item.done").AsBoolean());
    }

    [Fact(Skip = "TODO es6-generators")]
    public void Sending()
    {
        const string Script = """
          var sent;
          function * generator() {
            sent = [yield 5, yield 6];
          };
          var iterator = generator();
          iterator.next();
          iterator.next("foo");
          iterator.next("bar");
        """;

        _engine.Execute(Script);

        Assert.Equal("foo", _engine.Evaluate("sent[0]"));
        Assert.Equal("bar", _engine.Evaluate("sent[1]"));
    }

    [Fact(Skip = "TODO es6-generators")]
    public void Sending2()
    {
        const string Script = """
        function* counter(value) {
          while (true) {
            const step = yield value++;
        
            if (step) {
              value += step;
            }
          }
        }
        
        const generatorFunc = counter(0);
        """;

        _engine.Execute(Script);

        Assert.Equal(0, _engine.Evaluate("generatorFunc.next().value")); // 0
        Assert.Equal(1, _engine.Evaluate("generatorFunc.next().value")); // 1
        Assert.Equal(2, _engine.Evaluate("generatorFunc.next().value")); // 2
        Assert.Equal(3, _engine.Evaluate("generatorFunc.next().value")); // 3
        Assert.Equal(14, _engine.Evaluate("generatorFunc.next(10).value")); // 14
        Assert.Equal(15, _engine.Evaluate("generatorFunc.next().value")); // 15
        Assert.Equal(26, _engine.Evaluate("generatorFunc.next(10).value")); // 26
    }

    [Fact(Skip = "TODO es6-generators")]
    public void Fibonacci()
    {
        const string Script = """
            function* fibonacci() {
              let current = 0;
              let next = 1;
              while (true) {
                const reset = yield current;
                [current, next] = [next, next + current];
                if (reset) {
                  current = 0;
                  next = 1;
                }
              }
            }
            
            const sequence = fibonacci();
        """;

        _engine.Execute(Script);

        Assert.Equal(0, _engine.Evaluate("sequence.next().value"));
        Assert.Equal(1, _engine.Evaluate("sequence.next().value"));
        Assert.Equal(1, _engine.Evaluate("sequence.next().value"));
        Assert.Equal(2, _engine.Evaluate("sequence.next().value"));
        Assert.Equal(3, _engine.Evaluate("sequence.next().value"));
        Assert.Equal(5, _engine.Evaluate("sequence.next().value"));
        Assert.Equal(9, _engine.Evaluate("sequence.next().value"));
        Assert.Equal(0, _engine.Evaluate("sequence.next(true).value"));
        Assert.Equal(1, _engine.Evaluate("sequence.next().value)"));
        Assert.Equal(1, _engine.Evaluate("sequence.next().value)"));
        Assert.Equal(2, _engine.Evaluate("sequence.next().value)"));
    }
}
