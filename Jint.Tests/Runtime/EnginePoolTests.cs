using FluentAssertions;
using Jint.Pooling;
using Jint.Runtime;
using Xunit;

namespace Jint.Tests.Runtime
{
    public class EnginePoolTests
    {
        [Fact]
        public void LimitsShouldNotBeActiveDuringInitialization()
        {
            var options = new Options()
                .Strict()
                .MaxStatements(2);
            
            // we cannot run three statements with this configuration
            const string script = "Math.max(1, 2); Math.max(1, 2); Math.max(1, 2);";

            var pool = new IsolatedEngineFactory(options, e =>
            {
                // unless we are initializing the engine
                e.Evaluate(script);
            });

            using var engine = pool.Build();
            
            // no longer allowed for many statements 
            var ex = Assert.Throws<StatementsCountOverflowException>(() => engine.Evaluate(script));
            ex.Message.Should().Be("The maximum number of statements executed has reached allowed limit (2).");
            
            // allowed for less statements
            engine.Evaluate("Math.max(1, 2); Math.max(1, 2);");
        }
    }
}