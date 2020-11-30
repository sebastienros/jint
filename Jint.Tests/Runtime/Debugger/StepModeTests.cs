using Esprima.Ast;
using Jint.Runtime.Debugger;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Jint.Tests.Runtime.Debugger
{
    public class StepModeTests
    {
        /// <summary>
        /// Helper method to keep tests independent of line numbers, columns or other arbitrary assertions on
        /// the current statement. Steps through script with StepMode.Into until it reaches literal statement
        /// (or directive) 'source'. Then counts the steps needed to reach 'target' using the indicated StepMode.
        /// </summary>
        /// <param name="script">Script used as basis for test</param>
        /// <param name="stepMode">StepMode to use from source to target</param>
        /// <returns>Number of steps from source to target</returns>
        private int StepsFromSourceToTarget(string script, StepMode stepMode)
        {
            bool ReachedLiteral(DebugInformation info, string requiredValue)
            {
                switch (info.CurrentStatement)
                {
                    case Directive directive:
                        return directive.Directiv == requiredValue;
                    case ExpressionStatement expr:
                        return expr.Expression is Literal literal && literal.StringValue == requiredValue;
                }

                return false;
            }

            var engine = new Engine(options => options
                .DebugMode()
                .DebuggerStatementHandling(DebuggerStatementHandling.Jint));

            int steps = 0;
            bool sourceReached = false;
            bool targetReached = false;
            engine.Step += (sender, info) =>
            {
                if (sourceReached)
                {
                    steps++;
                    if (ReachedLiteral(info, "target"))
                    {
                        // Stop stepping
                        targetReached = true;
                        return StepMode.None;
                    }
                    return stepMode;
                }
                else if (ReachedLiteral(info, "source"))
                {
                    sourceReached = true;
                    return stepMode;
                }
                return StepMode.Into;
            };

            engine.Execute(script);
            
            // Make sure we actually reached the target
            Assert.True(targetReached);

            return steps;
        }

        [Fact]
        public void StepsIntoRegularFunctionCall()
        {
            var script = @"
                'source';
                test();
                function test()
                {
                    'target';
                }";

            Assert.Equal(3, StepsFromSourceToTarget(script, StepMode.Into));
        }

        [Fact]
        public void StepsOverRegularFunctionCall()
        {
            var script = @"
                'source';
                test();
                'target';
                function test()
                {
                    'dummy';
                }";

            Assert.Equal(2, StepsFromSourceToTarget(script, StepMode.Over));
        }

        [Fact]
        public void StepsOutOfRegularFunctionCall()
        {
            var script = @"
                test();
                'target';

                function test()
                {
                    'source';
                    'dummy';
                }";

            Assert.Equal(1, StepsFromSourceToTarget(script, StepMode.Out));
        }

        [Fact]
        public void StepsIntoMemberFunctionCall()
        {
            var script = @"
                const obj = {
                    test()
                    {
                        'target';
                    }
                };
                'source';
                obj.test();";

            Assert.Equal(3, StepsFromSourceToTarget(script, StepMode.Into));
        }

        [Fact]
        public void StepsOverMemberFunctionCall()
        {
            var script = @"
                const obj = {
                    test()
                    {
                        'dummy';
                    }
                };
                'source';
                obj.test();
                'target';";

            Assert.Equal(2, StepsFromSourceToTarget(script, StepMode.Over));
        }

        [Fact]
        public void StepsOutOfMemberFunctionCall()
        {
            var script = @"
                const obj = {
                    test()
                    {
                        'source';
                        'dummy';
                    }
                };
                obj.test();
                'target';";

            Assert.Equal(1, StepsFromSourceToTarget(script, StepMode.Out));
        }

        [Fact]
        public void StepsIntoCallExpression()
        {
            var script = @"
                function test()
                {
                    'target';
                    return 42;
                }
                'source';
                const x = test();";

            Assert.Equal(3, StepsFromSourceToTarget(script, StepMode.Into));
        }

        [Fact]
        public void StepsOverCallExpression()
        {
            var script = @"
                function test()
                {
                    'dummy';
                    return 42;
                }
                'source';
                const x = test();
                'target';";

            Assert.Equal(2, StepsFromSourceToTarget(script, StepMode.Over));
        }

        [Fact]
        public void StepsOutOfCallExpression()
        {
            var script = @"
                function test()
                {
                    'source';
                    'dummy';
                    return 42;
                }
                const x = test();
                'target';";

            Assert.Equal(1, StepsFromSourceToTarget(script, StepMode.Out));
        }

        [Fact]
        public void StepsIntoGetAccessor()
        {
            var script = @"
                const obj = {
                    get test()
                    {
                        'target';
                        return 144;
                    }
                };
                'source';
                const x = obj.test;";

            Assert.Equal(3, StepsFromSourceToTarget(script, StepMode.Into));
        }

        [Fact(Skip = "Debugger has no accessor awareness yet")]
        public void StepsOverGetAccessor()
        {
            var script = @"
                const obj = {
                    get test()
                    {
                        return 144;
                    }
                };
                'source';
                const x = obj.test;
                'target';";

            Assert.Equal(2, StepsFromSourceToTarget(script, StepMode.Over));
        }

        [Fact(Skip = "Debugger has no accessor awareness yet")]
        public void StepsOutOfGetAccessor()
        {
            var script = @"
                const obj = {
                    get test()
                    {
                        'source';
                        'dummy';
                        return 144;
                    }
                };
                const x = obj.test;
                'target';";

            Assert.Equal(1, StepsFromSourceToTarget(script, StepMode.Out));
        }

        [Fact]
        public void StepsIntoSetAccessor()
        {
            var script = @"
                const obj = {
                    set test(value)
                    {
                        'target';
                        this.value = value;
                    }
                };
                'source';
                obj.test = 37;";

            Assert.Equal(3, StepsFromSourceToTarget(script, StepMode.Into));
        }

        [Fact(Skip = "Debugger has no accessor awareness yet")]
        public void StepsOverSetAccessor()
        {
            var script = @"
                const obj = {
                    set test(value)
                    {
                        this.value = value;
                    }
                };
                'source';
                obj.test = 37;
                'target';";

            Assert.Equal(3, StepsFromSourceToTarget(script, StepMode.Over));
        }

        [Fact(Skip = "Debugger has no accessor awareness yet")]
        public void StepsOutOfSetAccessor()
        {
            var script = @"
                const obj = {
                    set test(value)
                    {
                        'source';
                        'dummy';
                        this.value = value;
                    }
                };
                obj.test = 37;
                'target';";

            Assert.Equal(1, StepsFromSourceToTarget(script, StepMode.Out));
        }
    }
}
