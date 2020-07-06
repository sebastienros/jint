using System;
using FluentAssertions;
using Jint.Native;
using Jint.Runtime;
using Xunit;

namespace Jint.Tests.Runtime
{
    public class IsolatedContextTests
    {
        [Fact]
        public void IsolatedContextCanBeUsedAndDisposed()
        {
            var engineFactory = new DefaultEngineFactory(new Options().Strict());
            var engine = engineFactory.Build();
            engine.SetValue("assert", new Action<bool>(Assert.True));

            // Set outer variable in global scope
            engine.SetValue("outer", 123);
            engine.SetValue("outerClrArray", new[] { "a", "b", "c" });
            engine.Evaluate("var outerJsArray = [ 'a', 'b', 'c' ];");
            engine.Evaluate("assert(outer === 123)");
            engine.Evaluate("outer").ToObject().Should().Be(123);

            // outer scope functions and trusted, they can break things, so be aware
            engine.Evaluate("function outerBreaker() { var m = Math.max; delete Math.max; assert(typeof Math.max === 'undefined');  Math.max = m; }");

            // Enter new execution context
            using (engine.ActivateIsolatedContext())
            {
                // Can see global scope
                engine.Evaluate("assert(outer === 123)");

                // Can modify global scope
                engine.Evaluate("outer = 321");
                engine.Evaluate("outer").ToObject().Should().Be(321);

                // Create variable in new context
                engine.Evaluate("var inner = 456");
                engine.Evaluate("assert(inner === 456)");

                engine.Evaluate("function innerBreaker() { Math.max = null; }");

                engine.Evaluate("var m = Math.max; outerBreaker();");
                engine.Evaluate("Math.max").Should().BeAssignableTo<ICallable>();
                engine.Evaluate("m === Math.max").AsBoolean().Should().BeTrue();
                
                // cannot break anything
                Assert.Throws<NotSupportedException>(() => engine.Evaluate("innerBreaker();"));
                Assert.Throws<NotSupportedException>(() => engine.Evaluate("Math.max = null;"));
                Assert.Throws<NotSupportedException>(() => engine.Evaluate("outerJsArray[0] = null;"));
                Assert.Throws<NotSupportedException>(() => engine.Evaluate("outerClrArray[0] = null;"));
                
                // but we can redeclare the whole math thing and make it "better" in this context
                engine.Evaluate("var Math = ({});");
                engine.Evaluate("Math.max = () => 42;");

                engine.Evaluate("outerJsArray = [ 'c' ];");
                engine.Evaluate("outerJsArray[0]").AsString().Should().Be("c");

                var maxInner = engine.Evaluate("Math.max(1, 2);").AsNumber();
                maxInner.Should().Be(42);
            }

            // The new variable is no longer visible
            engine.Evaluate("assert(typeof inner === 'undefined')");

            // The new variable is not in the global scope
            var ex = Assert.Throws<JavaScriptException>(()  => engine.Evaluate("inner"));
            ex.Message.Should().Be("inner is not defined");

            var max = engine.Evaluate("Math.max");
            max.ToObject().Should().NotBeNull();
            
            // and we should again get reasonable results from max
            var maxOuter = engine.Evaluate("Math.max(1, 2);").AsNumber();
            maxOuter.Should().Be(2);

            // array back to itself
            engine.Evaluate("outerJsArray[0]").AsString().Should().Be("a");

            // but can again break things
            engine.Evaluate("Math.max = null;");
            engine.Evaluate("Math.max").ToObject().Should().BeNull();

            engine.Evaluate("outerBreaker();");
            engine.Evaluate("Math.max").ToObject().Should().BeNull();
        }
    }
}