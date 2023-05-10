using Esprima.Ast;

namespace Jint.Tests.Runtime;

public partial class EngineTests
{
    [Fact]
    public void ScriptPreparationAcceptsReturnOutsideOfFunctions()
    {
        var preparedScript = Engine.PrepareScript("return 1;");
        Assert.IsType<ReturnStatement>(preparedScript.Body[0]);
    }

    // TODO when folding will be part of preparation
    // [Fact]
    public void ScriptPreparationFoldsConstants()
    {
        var preparedScript = Engine.PrepareScript("return 1 + 2;");
        var returnStatement = Assert.IsType<ReturnStatement>(preparedScript.Body[0]);
    }
}
