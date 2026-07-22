using Jint.Runtime.Debugger;

namespace Jint.Tests.Runtime.Debugger;

public class StepFlowTests
{
    private List<Node> CollectStepNodes(string script)
    {
        var engine = new Engine(options => options
            .DebugMode()
            .InitialStepMode(StepMode.Into));

        var nodes = new List<Node>();
        engine.Debugger.Step += (sender, info) =>
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

        nodes.Should().SatisfyRespectively(
            node => node.Should().BeOfType<VariableDeclaration>(), // let x = 0;
            node => node.Should().BeOfType<WhileStatement>(),      // while ...
            node => node.Should().BeOfType<NonLogicalBinaryExpression>(),    // x < 2
            node => node.Should().BeOfType<NonSpecialExpressionStatement>(), // x++;
            node => node.Should().BeOfType<NonLogicalBinaryExpression>(),    // x < 2
            node => node.Should().BeOfType<NonSpecialExpressionStatement>(), // x++;
            node => node.Should().BeOfType<NonLogicalBinaryExpression>()     // x < 2 (false)
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

        nodes.Should().SatisfyRespectively(
            node => node.Should().BeOfType<VariableDeclaration>(), // let x = 0;
            node => node.Should().BeOfType<DoWhileStatement>(),    // do ...
            node => node.Should().BeOfType<NonSpecialExpressionStatement>(), // x++;
            node => node.Should().BeOfType<NonLogicalBinaryExpression>(),    // x < 2
            node => node.Should().BeOfType<NonSpecialExpressionStatement>(), // x++;
            node => node.Should().BeOfType<NonLogicalBinaryExpression>()     // x < 2 (false)
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

        nodes.Should().SatisfyRespectively(
            node => node.Should().BeOfType<ForStatement>(),        // for ...
            node => node.Should().BeOfType<VariableDeclaration>(), // let x = 0
            node => node.Should().BeOfType<NonLogicalBinaryExpression>(),    // x < 2
            node => node.IsLiteral("dummy").Should().BeTrue(),     // 'dummy';
            node => node.Should().BeOfType<UpdateExpression>(),    // x++;
            node => node.Should().BeOfType<NonLogicalBinaryExpression>(),    // x < 2
            node => node.IsLiteral("dummy").Should().BeTrue(),     // 'dummy';
            node => node.Should().BeOfType<UpdateExpression>(),    // x++;
            node => node.Should().BeOfType<NonLogicalBinaryExpression>()     // x < 2 (false)
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

        nodes.Should().SatisfyRespectively(
            node => node.Should().BeOfType<VariableDeclaration>(), // let arr = [1, 2];
            node => node.Should().BeOfType<ForOfStatement>(),      // for ...
            node => node.Should().BeOfType<VariableDeclaration>(), // item
            node => node.IsLiteral("dummy").Should().BeTrue(),     // 'dummy';
            node => node.Should().BeOfType<VariableDeclaration>(), // item
            node => node.IsLiteral("dummy").Should().BeTrue()      // 'dummy';
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

        nodes.Should().SatisfyRespectively(
            node => node.Should().BeOfType<VariableDeclaration>(), // let obj = { x: 1, y: 2 };
            node => node.Should().BeOfType<ForInStatement>(),      // for ...
            node => node.Should().BeOfType<VariableDeclaration>(), // key
            node => node.Should().BeOfType<NonSpecialExpressionStatement>(), // 'dummy';
            node => node.Should().BeOfType<VariableDeclaration>(), // key
            node => node.Should().BeOfType<NonSpecialExpressionStatement>()  // 'dummy';
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

        nodes.Should().SatisfyRespectively(
            node => node.Should().BeOfType<ClassDeclaration>(),          // class Test
            node => node.Should().BeOfType<NonSpecialExpressionStatement>(),       // new Test();
            node => node.IsLiteral("in constructor").Should().BeTrue(),  // 'in constructor()'
            node => node.Should().BeNull(),                              // return point
            node => node.IsLiteral("after construction").Should().BeTrue()
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

        nodes.Should().SatisfyRespectively(
            node => node.Should().BeOfType<FunctionDeclaration>(), // function(test) ...;
            node => node.Should().BeOfType<NonSpecialExpressionStatement>(), // test();
            node => node.IsLiteral("dummy").Should().BeTrue(),     // 'dummy';
            node => node.Should().BeNull()                         // return point
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
        nodes.Should().SatisfyRespectively(
            node => node.Should().BeOfType<ClassDeclaration>(),    // class Test
            node => node.Should().BeOfType<NonSpecialExpressionStatement>(), // new Test();
            node => node.IsLiteral("dummy").Should().BeTrue()      // 'dummy';
        );
    }

    [Fact]
    public void StepIntoNamedFunctionCalls()
    {
        var script = @"
function a( ) { return 2; }
function b(l) { return l + a(); }
function c( ) { return b(3) + a(); }
let res = c();
";

        var steps = StepIntoScript(script);
        steps.Should().SatisfyRespectively(
            step => step.Should().Be("function c( ) { »return b(3) + a(); }"),
            step => step.Should().Be("function b(l) { »return l + a(); }"),
            step => step.Should().Be("function a( ) { »return 2; }"),
            step => step.Should().Be("function a( ) { return 2; }»"),
            step => step.Should().Be("function b(l) { return l + a(); }»"),
            step => step.Should().Be("function a( ) { »return 2; }"),
            step => step.Should().Be("function a( ) { return 2; }»"),
            step => step.Should().Be("function c( ) { return b(3) + a(); }»"));
    }

    [Fact]
    public void StepIntoArrowFunctionCalls()
    {
        var script = @"
const a = ( ) => 2;
const b = (l) => l + a();
const c = ( ) => b(3) + a();
let res = c();
";

        var steps = StepIntoScript(script);
        steps.Should().SatisfyRespectively(
            step => step.Should().Be("const c = ( ) => »b(3) + a();"),
            step => step.Should().Be("const b = (l) => »l + a();"),
            step => step.Should().Be("const a = ( ) => »2;"),
            step => step.Should().Be("const a = ( ) => 2»;"),
            step => step.Should().Be("const b = (l) => l + a()»;"),
            step => step.Should().Be("const a = ( ) => »2;"),
            step => step.Should().Be("const a = ( ) => 2»;"),
            step => step.Should().Be("const c = ( ) => b(3) + a()»;"));
    }

    private List<string> StepIntoScript(string script)
    {
        var engine = new Engine(options => options
            .DebugMode()
            .InitialStepMode(StepMode.Into));

        var stepStatements = new List<string>();
        var scriptLines = script.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n');
        engine.Debugger.Step += (sender, information) =>
        {
            if (information.CurrentNode is not VariableDeclaration && information.CurrentNode is not FunctionDeclaration)
                OutputPosition(information.Location);
            return StepMode.Into;
        };

        engine.Execute(script);
        return stepStatements;

        void OutputPosition(in SourceLocation location)
        {
            var line = scriptLines[location.Start.Line - 1];
            var withPositionIndicator = string.Concat(line.Substring(0, location.Start.Column), "»", line.Substring(location.Start.Column));
            stepStatements.Add(withPositionIndicator.TrimEnd());
        }
    }
}