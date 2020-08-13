using System;
using Xunit;

namespace Jint.Tests.Runtime
{
    public class ConstTests
    {
        private readonly Engine _engine;

        public ConstTests()
        {
            _engine = new Engine()
                .SetValue("log", new Action<object>(Console.WriteLine))
                .SetValue("assert", new Action<bool>(Assert.True))
                .SetValue("equal", new Action<object, object>(Assert.Equal));
        }

        [Fact]
        public void ConstInsideIife()
        {
            _engine.Execute(@"
                (function(){
                    const testVariable = 'test';
                    function render() {
                        log(testVariable);
                    }
                    render();
                })();
            ");
        }

        [Fact]
        public void ConstDestructuring()
        {
            _engine.Execute(@"
                let obj = {};
                for (var i = 0; i < 1; i++) {
                    const { subElement } = obj;
                }
            ");
        }
    }
}
