using Esprima.Ast;
using Jint.Runtime.Debugger;

namespace Jint.Tests.Runtime.Debugger
{
    public class StepFlowTests
    {
        private List<Node> CollectStepNodes(string script)
        {
            var engine = new Engine(options => options
                .DebugMode()
                .InitialStepMode(StepMode.Into));

            var nodes = new List<Node>();
            engine.DebugHandler.Step += (sender, info) =>
            {
                nodes.Add(info.CurrentNode);
                return StepMode.Into;
            };

            engine.Execute(script);

            return nodes;
        }

        [Fact]
        public void StepsThroughWhileLoop()
        {
            string script = @"
                let x = 0;
                while (x < 2)
                {
                    x++;
                }
            ";

            var nodes = CollectStepNodes(script);

            Assert.Collection(nodes,
                node => Assert.IsType<VariableDeclaration>(node), // let x = 0;
                node => Assert.IsType<WhileStatement>(node),      // while ...
                node => Assert.IsType<BinaryExpression>(node),    // x < 2
                node => Assert.IsType<ExpressionStatement>(node), // x++;
                node => Assert.IsType<BinaryExpression>(node),    // x < 2
                node => Assert.IsType<ExpressionStatement>(node), // x++;
                node => Assert.IsType<BinaryExpression>(node)     // x < 2 (false)
            );
        }

        [Fact]
        public void StepsThroughDoWhileLoop()
        {
            string script = @"
                let x = 0;
                do
                {
                    x++;
                }
                while (x < 2)
            ";

            var nodes = CollectStepNodes(script);

            Assert.Collection(nodes,
                node => Assert.IsType<VariableDeclaration>(node), // let x = 0;
                node => Assert.IsType<DoWhileStatement>(node),    // do ...
                node => Assert.IsType<ExpressionStatement>(node), // x++;
                node => Assert.IsType<BinaryExpression>(node),    // x < 2
                node => Assert.IsType<ExpressionStatement>(node), // x++;
                node => Assert.IsType<BinaryExpression>(node)     // x < 2 (false)
            );
        }

        [Fact]
        public void StepsThroughForLoop()
        {
            string script = @"
                for (let x = 0; x < 2; x++)
                {
                    'dummy';
                }
            ";

            var nodes = CollectStepNodes(script);

            Assert.Collection(nodes,
                node => Assert.IsType<ForStatement>(node),        // for ...
                node => Assert.IsType<VariableDeclaration>(node), // let x = 0
                node => Assert.IsType<BinaryExpression>(node),    // x < 2
                node => Assert.IsType<ExpressionStatement>(node), // 'dummy';
                node => Assert.IsType<UpdateExpression>(node),    // x++;
                node => Assert.IsType<BinaryExpression>(node),    // x < 2
                node => Assert.IsType<ExpressionStatement>(node), // 'dummy';
                node => Assert.IsType<UpdateExpression>(node),    // x++;
                node => Assert.IsType<BinaryExpression>(node)     // x < 2 (false)
            );
        }

        [Fact]
        public void StepsThroughForOfLoop()
        {
            string script = @"
                const arr = [1, 2];
                for (const item of arr)
                {
                    'dummy';
                }
            ";

            var nodes = CollectStepNodes(script);

            Assert.Collection(nodes,
                node => Assert.IsType<VariableDeclaration>(node), // let arr = [1, 2];
                node => Assert.IsType<ForOfStatement>(node),      // for ...
                node => Assert.IsType<VariableDeclaration>(node), // item
                node => Assert.IsType<ExpressionStatement>(node), // 'dummy';
                node => Assert.IsType<VariableDeclaration>(node), // item
                node => Assert.IsType<ExpressionStatement>(node)  // 'dummy';
            );
        }

        [Fact]
        public void StepsThroughForInLoop()
        {
            string script = @"
                const obj = { x: 1, y: 2 };
                for (const key in obj)
                {
                    'dummy';
                }
            ";

            var nodes = CollectStepNodes(script);

            Assert.Collection(nodes,
                node => Assert.IsType<VariableDeclaration>(node), // let obj = { x: 1, y: 2 };
                node => Assert.IsType<ForInStatement>(node),      // for ...
                node => Assert.IsType<VariableDeclaration>(node), // key
                node => Assert.IsType<ExpressionStatement>(node), // 'dummy';
                node => Assert.IsType<VariableDeclaration>(node), // key
                node => Assert.IsType<ExpressionStatement>(node)  // 'dummy';
            );
        }


        [Fact]
        public void SkipsFunctionBody()
        {
            string script = @"
                function test()
                {
                    'dummy';
                }
                test();
            ";

            var nodes = CollectStepNodes(script);

            Assert.Collection(nodes,
                node => Assert.IsType<FunctionDeclaration>(node), // function(test) ...;
                node => Assert.IsType<ExpressionStatement>(node), // test();
                node => Assert.IsType<Directive>(node),           // 'dummy';
                node => Assert.Null(node)                         // return point
            );
        }
    }
}
