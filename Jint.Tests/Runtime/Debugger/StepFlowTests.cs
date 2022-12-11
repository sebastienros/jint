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
            var script = @"
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
            var script = @"
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
            var script = @"
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
                node => Assert.True(node.IsLiteral("dummy")),     // 'dummy';
                node => Assert.IsType<UpdateExpression>(node),    // x++;
                node => Assert.IsType<BinaryExpression>(node),    // x < 2
                node => Assert.True(node.IsLiteral("dummy")),     // 'dummy';
                node => Assert.IsType<UpdateExpression>(node),    // x++;
                node => Assert.IsType<BinaryExpression>(node)     // x < 2 (false)
            );
        }

        [Fact]
        public void StepsThroughForOfLoop()
        {
            var script = @"
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
                node => Assert.True(node.IsLiteral("dummy")),     // 'dummy';
                node => Assert.IsType<VariableDeclaration>(node), // item
                node => Assert.True(node.IsLiteral("dummy"))      // 'dummy';
            );
        }

        [Fact]
        public void StepsThroughForInLoop()
        {
            var script = @"
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
        public void StepsThroughConstructor()
        {
            var script = @"
                class Test
                {
                    constructor()
                    {
                        'in constructor';
                    }
                }
                new Test();
                'after construction';
            ";

            var nodes = CollectStepNodes(script);

            Assert.Collection(nodes,
                node => Assert.IsType<ClassDeclaration>(node),          // class Test
                node => Assert.IsType<ExpressionStatement>(node),       // new Test();
                node => Assert.True(node.IsLiteral("in constructor")),  // 'in constructor()'
                node => Assert.Null(node),                              // return point
                node => Assert.True(node.IsLiteral("after construction"))
            );
        }

        [Fact]
        public void SkipsFunctionBody()
        {
            var script = @"
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
                node => Assert.True(node.IsLiteral("dummy")),     // 'dummy';
                node => Assert.Null(node)                         // return point
            );
        }

        [Fact]
        public void SkipsReturnPointOfImplicitConstructor()
        {
            var script = @"
                class Test
                {
                }
                new Test();
                'dummy';
            ";

            var nodes = CollectStepNodes(script);
            Assert.Collection(nodes,
                node => Assert.IsType<ClassDeclaration>(node),    // class Test
                node => Assert.IsType<ExpressionStatement>(node), // new Test();
                node => Assert.True(node.IsLiteral("dummy"))      // 'dummy';
            );
        }
    }
}
